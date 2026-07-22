## Context

`ImplementWorkflowRunner` (`SpecRunner.Console/ImplementWorkflowRunner.cs`)
already provides the exact shape `/update` needs: each `PollingLoop.RunAsync`
cycle calls its `RunOnceAsync`, which resolves the bot login, lists open PRs
via `IGitHubService.ListOpenPullRequestsAsync`, reads each PR's comments via
`ReadPrCommentsAsync`, filters to comments matching a hardcoded trigger via a
`TryGetInstructions` helper, skips any already carrying a bot
`eyes`/`+1`/`confused` reaction, and processes the rest sequentially: react
`eyes` → resolve the tracked issue via `IStateStore.FindByPrNumberAsync` (untracked
PR → `confused` reply, no further work) → `FetchAsync`/`SwitchBranchAsync`/
`ResetHardAsync` the PR's head branch → start a CLI agent session with a
quoted prompt → drain events to a terminal state → `CommitAsync`/`PushAsync`
→ `+1` reaction + reply + state-store upsert, or `confused` reaction + reply +
state-store upsert on error/timeout, all wrapped in `SpecRunnerOptions.TaskTimeout`.

`/update` reuses every one of those primitives unchanged — `IGitHubService`,
`IGitService`, and `IStateStore` already expose everything needed (confirmed
by inspecting `IGitHubService`, `IStateStore`, and the `TrackedComments`
SQLite schema: `CommentKind.PrIssueComment` plus the existing
pending/working/done/error status enum already cover a `/update` comment the
same way they cover an `/implement` comment). The only real difference is
*what* gets sent to the CLI agent: `/implement` runs the fixed `/opsx-apply
{spec} {instructions}` slash command, while `/update` sends a plain
natural-language instruction — `Update the OpenSpec change "{spec-name}" to
reflect the following new requirement/information:\n{instructions}` — with no
corresponding `opsx-update` skill to invoke. This is a deliberate, explicit
requirement from the person requesting this change, not a gap to fill with a
new skill.

## Goals / Non-Goals

**Goals:**
- A `/update` comment on a tracked PR sends the comment's content, verbatim
  minus the trigger token, to the CLI coding agent as a natural-language
  instruction to amend the associated OpenSpec change, and reports the
  outcome back on the comment with the same react-first/skip-if-already-
  reacted/sequential-processing guarantees `/propose` and `/implement`
  already provide.
- All three workflows (`propose`, `implement`, `update`) keep sharing one
  local clone safely — they never run concurrently.

**Non-Goals:**
- No new `opsx-update` Claude Code skill/slash-command — the prompt sent to
  the CLI agent is plain natural language, per this change's explicit
  requirement.
- No PR *review* comment (inline code comment) support — only general PR
  conversation comments, matching `/implement`'s existing scope.
- No state-store schema changes — see the decision below.
- No `MarkPrReadyForReviewAsync` call or new GitHub/git operations — every
  primitive `/update` needs already exists.

## Decisions

- **`UpdateWorkflowRunner` is a near-verbatim copy of `ImplementWorkflowRunner`'s
  structure** (same constructor dependencies, same scan/skip/react-first/
  timeout/error-handling shape), differing only in: the trigger token
  (`/update` vs `/implement`), the CLI-agent prompt template, the commit
  message, and the untracked-PR/success/error reply text. A shared base
  class or extracted helper was considered and rejected: the two runners'
  bodies are short and the shared shape is already documented as a
  convention (this design doc plus `/implement`'s own design.md) rather than
  enforced by inheritance; introducing an abstraction now for two callers
  repeats the same premature-generalization call already made and rejected
  for the poll loop in `/implement`'s design.

- **The CLI-agent prompt is a natural-language instruction, not an
  `/opsx-*` slash command.** Built as `Update the OpenSpec change
  "{spec-name}" to reflect the following new requirement/information:\n
  {instructions}`, where `{instructions}` is the triggering comment's
  trimmed body with the leading `/update` token and its separating
  whitespace removed (reusing `/implement`'s `TryGetInstructions` pattern
  unchanged), and the entire string is sent as a single value wrapped in
  escaped double quotes (`\"...\"`) to `ICliAgentSession.StartAsync`,
  matching `/propose` and `/implement`'s existing prompt-quoting
  convention. This is a literal requirement from the person requesting
  this change (exact wording, exact quoting style), not a design choice
  made for consistency — it's called out here because it's the one place
  `/update` deviates from the `/opsx-{verb} {spec} {instructions}` shape
  the other two workflows use.

- **No state-store schema changes.** `/update` comments are tracked the
  same way `/implement` comments are: a `TrackedComment` with
  `CommentKind.PrIssueComment` and status `pending`/`working`/`done`/
  `error`, upserted under the resolved `TrackedIssue.IssueNumber`. Nothing
  about `/update` needs to distinguish *which* workflow produced a given
  `TrackedComment` row — the GitHub reaction on the comment itself is
  already the record of what ran and how it ended, consistent with how
  `/propose` and `/implement` treat reactions (not the state store) as the
  source of truth for per-comment outcome. The proposal's permission to
  adjust the schema without preserving backward compatibility is noted for
  the record but not exercised, since inspection of `IStateStore` and the
  SQLite schema turned up no gap.

- **The bot's own-reaction skip list is `{eyes, +1, confused}`, identical
  to `/implement`'s.** `+1` stands in for a checkmark (GitHub's reaction
  set has no literal checkmark, the same constraint `/implement`'s design
  already documented), and `confused` is included in the skip list — not
  just `eyes`/`+1` as a literal reading of "without eyes or check icons"
  might suggest — so a permanently-failing `/update` comment (e.g. a typo'd
  spec reference on an untracked PR) doesn't get reprocessed, timed out,
  and re-reported on every single poll indefinitely. This mirrors
  `/implement`'s own rationale for the same choice.

- **Commit message is `"updating specs for #{issue-number}"`**, keyed off
  the issue number recorded in the resolved `TrackedIssue`, extending the
  existing per-workflow commit-message convention (`/propose`: "adding
  specs for #{n}", `/implement`: "applying specs for #{n}").

- **An untracked PR's `/update` comment is reported as an error, not
  guessed.** Same rationale as `/implement`: there is no reliable spec name
  to hand the CLI agent without a state-store record, so the workflow
  replies explaining no associated spec/change was found, reacts
  `confused`, and skips the git/CLI steps and state-store write entirely
  (no issue number to key a `TrackedComment` under).

- **`PollingLoop.RunAsync` and `Program.cs` are extended to a third
  runner, `IUpdateWorkflowRunner`, run after `implement-workflow` each
  cycle**, each independently wrapped in its own try/catch, exactly like
  the existing `propose` → `implement` sequencing. The "revisit a generic
  runner list if a third workflow needs it" note in `/implement`'s design
  is now moot in the sense that a third workflow *has* arrived, but the
  three call sites are still short and explicit enough (three parameters,
  three try/catch blocks) that introducing `IReadOnlyList<IWorkflowRunner>`
  is deferred again rather than done opportunistically in a change that
  isn't primarily about the poll loop's shape.

## Risks / Trade-offs

- [A natural-language prompt to the CLI agent is less predictable than a
  fixed `/opsx-apply` task list — the agent has to decide for itself how to
  amend proposal/design/specs/tasks] → Accepted as the explicit shape
  requested for this workflow; `/update`'s purpose is exactly to let a
  reviewer describe new information in their own words rather than
  drive a fixed command.
- [Reusing `ImplementWorkflowRunner`'s structure by copying rather than
  extracting a shared base risks the two drifting out of sync if one is
  changed without the other] → Consistent with the existing precedent
  (`/implement` was itself written as a structural copy of `/propose`'s
  shape, not a shared abstraction); acceptable at three total workflows,
  revisit if a fourth needs the same shape again.
