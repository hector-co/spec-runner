## Context

`ImplementWorkflowRunner`, `UpdateWorkflowRunner`, and `FinalizeWorkflowRunner`
each scan open PRs for trigger comments, then call
`IStateStore.FindByPrNumberAsync(prNumber)`. If that returns `null`, the PR is
treated as "untracked" and the comment is refused with a generic message
("no associated spec/change was found... only works on a PR opened by
`/propose`"). The only writer of `TrackedIssues` rows today is
`ProposeWorkflowRunner`, so any PR whose branch and spec/change folder were
created outside spec-runner is permanently unreachable by the other three
workflows.

The `TrackedIssues` schema currently makes `IssueNumber` the non-nullable,
unique anchor of a tracked record (`SqliteStateStore`'s
`IssueNumber INTEGER NOT NULL UNIQUE`), and every downstream string
(commit messages, PR title rewriting, the finalize "Closes #N" body suffix)
is built by interpolating that issue number directly. That's fine for records
`/propose` created — an issue always exists by construction — but doesn't
hold for a PR that was never linked to an issue at all.

## Goals / Non-Goals

**Goals:**
- Let `/implement`, `/update`, and `/finalize` succeed on a PR spec-runner
  didn't create, whether or not it has a linked GitHub issue, by adopting it
  into the state store on first eligible-comment processing.
- Discover everything adoption needs (branch, spec folder, optional issue)
  from GitHub and git state rather than any naming convention, since an
  externally-created PR has no obligation to follow spec-runner's own
  `feature/{issue}` / `feat-{issue}-{slug}` conventions.
- Keep every existing `/propose`-created record's behavior byte-for-byte
  unchanged.

**Non-Goals:**
- No new user-facing trigger (no `/adopt` command). Adoption is an invisible
  step inside the three existing workflows.
- No change to `/propose` itself.
- No attempt to resolve ambiguity automatically (multiple candidate spec
  folders or multiple linked issues) — those cases still refuse, just with a
  more specific message than today's generic one.
- No support for a PR whose base branch differs from
  `SpecRunnerOptions.BaseBranchName` — see Risks below.

## Decisions

### Identity anchor: `PrNumber`, not a repurposed `IssueNumber`

`TrackedIssue.IssueNumber` becomes `int?`; the SQLite column drops
`NOT NULL` and its unique index becomes "unique where not null" (the same
pattern the `PrNumber` column already uses). `PrNumber` — already present and
unique on every record that reaches adoption, since all three workflows only
ever process comments on existing PRs — becomes the anchor for comment
bookkeeping (`UpsertCommentAsync`) instead of issue number.

Alternative considered: store the PR number in the `IssueNumber` column for
issue-less records, and use PR data purely for display. Rejected: GitHub
issues and PRs share one number sequence per repository, so an adopted PR
numbered, say, 57 could collide with a genuinely unrelated issue #57.
`FindByIssueNumberAsync` — which `/propose` uses to check "does this issue
already have an active PR" — would then misreport that unrelated issue as
already handled. Keeping `IssueNumber` genuinely absent avoids the collision
entirely.

### Spec-folder discovery: diff against base, not name reconstruction

Adoption discovers the spec/change folder by comparing the set of top-level
directories under `openspec/changes/` on the PR's head branch against the
same set on the configured base branch, after fetching the head branch
locally. A folder present on the head branch but not the base is a candidate.
This requires `IGitService` to gain an operation for that comparison (e.g.
`ListAddedSpecFolderNamesAsync(baseBranch, headBranch)`), implemented as a
`git diff --name-only <base>...<head> -- openspec/changes` (or equivalent)
restricted to top-level directory names.

Alternative considered: regenerate the expected name via
`ISpecNameResolver`/`ISpecFolderResolver`'s existing `feat-{issue}-` prefix
fallback. Rejected outright for the no-issue case (there's no issue number to
key off), and unreliable even when an issue is found, since an externally
created proposal has no obligation to follow that naming convention. Diffing
works uniformly for both scenarios and doesn't depend on any naming
convention at all.

- Exactly one added folder → adopt using that folder name as `SpecName`.
- Zero added folders → refuse; reply explains no OpenSpec change folder was
  found on the branch, so there's nothing to adopt.
- More than one added folder → refuse; reply lists the candidate folder
  names and states the ambiguity, rather than guessing.

### Issue discovery: GraphQL `closingIssuesReferences`

`IGitHubService` gains `ListClosingIssueNumbersAsync(prNumber)`, implemented
as a GraphQL query for `pullRequest.closingIssuesReferences.nodes.number`
(mirroring the existing GraphQL plumbing `MarkPrReadyForReviewAsync` already
uses for `markPullRequestReadyForReview`). This is GitHub's own canonical
signal for "which issue(s) does this PR close" — it captures both
keyword-linked issues (`closes #N` in the PR body) and issues linked purely
through the PR sidebar's "Development" UI, which a body-text regex would
miss.

- Zero results → valid outcome; adopt without an issue number.
- Exactly one result → adopt with that issue number; the record and all
  downstream behavior become indistinguishable from one `/propose` created.
- More than one result → refuse; reply lists the candidate issue numbers.

### Branch name: read directly, never reconstructed

`GitHubPullRequest.HeadBranch` (already returned by
`ListOpenPullRequestsAsync`) is used as-is. No branch-name derivation is
needed for adoption in either scenario — only the spec folder and the issue
number are actually unknown.

### No-issue formatting substitutions

For a tracked record with `IssueNumber is null`, each call site that
currently interpolates an issue number is given a PR-number-based
alternative instead of a shared "name" abstraction, to keep each workflow's
existing string-building code legible:

| Call site | With issue | Without issue |
|---|---|---|
| Implement commit | `"applying specs for #{issue}"` | `"applying specs for PR #{pr}"` |
| Update commit | `"updating specs for #{issue}"` | `"updating specs for PR #{pr}"` |
| Finalize commit | `"finalizing specs for #{issue}"` | `"finalizing specs for PR #{pr}"` |
| Implement/Finalize title rewrite | `PullRequestTitles.ExtractIssueName` finds `"#{issue}: "` marker | no marker to find; the PR's current title is used as `{issue-name}` unchanged (same fallback `ExtractIssueName` already uses when the marker is absent) |
| Finalize PR body | appends `"\n\nCloses #{issue}"` | line omitted entirely |

`PullRequestTitles.ExtractIssueName` already falls back to the whole title
when its `#{issueNumber}: ` marker isn't found, so the no-issue title case
needs no new code there — only the callers' title-building format strings
need an issue-number-present/absent branch.

### Adoption is attempted automatically, gated on unambiguous discovery

The existing `if (trackedIssue is null) { refuse; return; }` branch in each
of the three workflows becomes: attempt discovery (folder diff, then issue
lookup); if discovery is unambiguous (one folder, zero-or-one issue), upsert
a tracked record and fall through to the existing tracked-PR path; otherwise
refuse with the specific message for whichever step failed. This keeps the
refuse-and-explain behavior as the safe default for anything that doesn't
look like a real OpenSpec-driven PR (e.g. an unrelated PR that happens to get
an `/implement`-shaped comment), while unblocking the two scenarios this
change targets.

## Risks / Trade-offs

- **Base branch mismatch** — discovery diffs against
  `SpecRunnerOptions.BaseBranchName`. If an adopted PR's actual GitHub base
  branch differs from that configured value, the diff is against the wrong
  ref and folder discovery can misbehave (missing or spurious candidates).
  → Mitigation: none in this change; documented as a constraint. A future
  change could read the PR's actual `base.ref` instead if this proves
  necessary.
- **`IssueNumber` becoming nullable is a breaking model/schema change** —
  any code (in this repo or a consumer) that assumes `TrackedIssue.IssueNumber`
  is always present will no longer compile or will need a null check.
  → Mitigation: the change is scoped to this codebase; the compiler will
  surface every affected call site as part of `tasks.md`'s implementation
  work.
- **`MarkPrReadyForReviewAsync` on an externally-created PR that's already
  ready for review** — `finalize-workflow`'s existing behavior calls this
  unconditionally; if the adopted PR was never a draft, the GraphQL mutation
  may no-op or return a GraphQL error depending on GitHub's behavior for a
  non-draft PR.
  → Mitigation: out of scope for this change (pre-existing behavior, not
  introduced by adoption); noted here in case it surfaces during
  implementation.
- **A PR that happens to contain an `openspec/changes/` folder from an
  unrelated, already-merged change** (e.g. a base branch that's behind) could
  cause a spurious "multiple folders" ambiguity refusal.
  → Mitigation: acceptable — refusing with an explanatory message is the
  safe outcome; no data is written and no destructive action taken.

## Migration Plan

- Ship as a normal application update; the SQLite migration
  (`EnsureBranchNameColumnAsync`-style in-place `ALTER TABLE`) drops the
  `NOT NULL` constraint. SQLite can't drop a column constraint in place, so
  this requires the same pattern already used for schema evolution
  (`SqliteStateStore.EnsureSchemaAsync`/`EnsureBranchNameColumnAsync`):
  rebuild the table with the new constraint set inside a single migration
  step for any pre-existing database file, preserving all existing rows.
- No rollback concern beyond redeploying the prior binary against the same
  database file; existing rows (which all have a non-null `IssueNumber`)
  remain valid under either schema version.

## Open Questions

- Should adoption's "unambiguous" bar (exactly one folder, zero-or-one issue)
  be revisited later to allow a human to disambiguate via a PR comment
  reply, instead of always refusing? Left for a future change if the refusal
  message proves too limiting in practice.
