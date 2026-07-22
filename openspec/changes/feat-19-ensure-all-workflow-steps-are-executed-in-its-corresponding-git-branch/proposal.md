## Why

Every workflow runner (`propose`, `implement`, `update`, `finalize`) drives the same single local clone, but none of them guarantee the working tree is clean and on the right branch before they start making changes. `propose` pulls and hard-resets the base branch without ever explicitly checking it out first, and force-creates the issue branch (`git branch -f`) without checking whether a branch of that name already exists locally or on `origin` — silently clobbering it if it does. `implement`, `update`, and `finalize` fetch/switch/reset the PR's branch, but only after re-deriving that branch name fresh from GitHub's live PR metadata each poll, with no locally durable record of it and no discard-only reset before the switch. If a previous run was killed mid-CLI-agent-session, the working tree can be left dirty on whatever branch it was last on; the next run's `checkout` can then fail outright, or, worse, a hard reset can silently discard the wrong branch's state. There is currently no local, durable record of which branch a given issue/PR is actually on.

## What Changes

- `IGitService` gains a `BranchExistsAsync` operation so callers can check whether a branch name is already taken (locally or on `origin`) before creating one, and `CreateBranchAsync`'s real implementation stops force-creating (`git branch -f` → `git branch`), so it fails loudly on a name collision instead of silently overwriting.
- Every workflow step now performs a discard-only `git reset --hard` against the current `HEAD` as its first action, before any branch switch, so a dirty working tree left over from an interrupted prior run never blocks getting onto the correct branch.
- `propose` explicitly switches to `SpecRunnerOptions.BaseBranchName` before pulling it (rather than relying on `PullAsync`'s internal checkout as the only place this happens), then generates the issue branch name as `feature/{issue-number}`, appending `-2`, `-3`, etc. if a branch by that name already exists, so `/propose` always creates a genuinely new branch.
- `TrackedIssue` gains a `BranchName` field, persisted as soon as the issue's branch is created (before the CLI agent runs), so the branch name survives a mid-run crash. `implement`, `update`, and `finalize` now read the branch to switch to from this stored value instead of re-deriving it from the PR's live head branch on every poll.
- `IStateStore.UpsertTrackedIssueAsync` is generalized to update `SpecName` and `BranchName` on conflict (today it only updates `PrNumber`), since `propose` now needs to record the branch before the final spec name is known and correct it once the CLI agent completes.

## Capabilities

### New Capabilities
(none — this change extends existing capabilities)

### Modified Capabilities
- `git-operations`: adds `BranchExistsAsync`; `CreateBranchAsync` no longer force-overwrites an existing branch of the same name.
- `state-store-schema`: `TrackedIssue` gains a `BranchName` field; `UpsertTrackedIssueAsync` updates `SpecName` and `BranchName` on conflict, not just `PrNumber`.
- `propose-workflow`: resets `HEAD` hard before switching to and pulling the base branch; generates a collision-safe, suffixed issue branch name; persists the branch name to the state store immediately after creating it, ahead of running the CLI agent.
- `implement-workflow`: resets `HEAD` hard before refreshing the tracked branch; switches to and pushes the branch recorded in the state store's `TrackedIssue.BranchName` instead of the PR's live head branch.
- `update-workflow`: same branch-safety and stored-branch-name changes as `implement-workflow`.
- `finalize-workflow`: same branch-safety and stored-branch-name changes as `implement-workflow`.

## Impact

- `SpecRunner.Core/Abstractions/IGitService.cs`, `SpecRunner.Git/GitService.cs`: new `BranchExistsAsync` member; `CreateBranchAsync` implementation change.
- `SpecRunner.Core/Abstractions/IStateStore.cs` is unchanged in shape, but `SpecRunner.Core/Models/TrackedIssue.cs` gains `BranchName`, and `SpecRunner.State/SqliteStateStore.cs` gains a schema column plus updated upsert SQL.
- `SpecRunner.Console/ProposeWorkflowRunner.cs`, `ImplementWorkflowRunner.cs`, `UpdateWorkflowRunner.cs`, `FinalizeWorkflowRunner.cs`: revised git call sequencing and branch-name source.
- Test doubles `SpecRunner.Tests/Fakes/FakeGitService.cs` and `RecordingGitService.cs` need the new `BranchExistsAsync` member; the four `*WorkflowRunnerTests` and `SqliteStateStoreTests` need coverage for the new sequencing, suffixing, and persistence behavior.

## Assumptions

- "A branch with the expected name already exists" is checked both locally and on `origin`, since a previous crashed run may have pushed a branch without ever recording it, and the check must catch that case too.
- The state store's `BranchName` becomes the source of truth for which branch `implement`/`update`/`finalize` check out, taking precedence over the PR's live head branch reported by GitHub on each poll. This is a deliberate trade-off: it satisfies the requirement to durably store and reuse the branch name, at the acceptable cost that a branch renamed on GitHub after PR creation (an unsupported, unexpected action) would go undetected until the next `/propose` run.
- "Perform a pull from origin" for the feature-branch steps is realized as the existing fetch + hard-reset-to-`origin/{branch}` sequence (already equivalent to a forced fast-forward), rather than introducing a literal `git pull` call for feature branches.
