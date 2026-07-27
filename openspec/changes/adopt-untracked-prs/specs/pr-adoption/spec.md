## ADDED Requirements

### Requirement: An untracked PR triggers an adoption attempt before refusal
`implement-workflow`, `update-workflow`, and `finalize-workflow` SHALL
attempt to adopt a PR — discovering its spec/change folder and, optionally,
an associated issue — before refusing an eligible trigger comment whose PR
has no `IStateStore` record (`FindByPrNumberAsync` returns `null`).
Adoption SHALL only ever read from GitHub and the local git clone and
upsert a new tracked record; it SHALL NOT create or modify any branch, pull
request, or GitHub issue.

#### Scenario: Untracked PR now attempts adoption instead of refusing immediately
- **WHEN** an eligible trigger comment is processed on PR `12` and the state
  store has no record with PR number `12`
- **THEN** the workflow SHALL run spec-folder discovery and issue discovery
  for PR `12` before deciding whether to proceed or refuse

### Requirement: Spec folder discovery diffs `openspec/changes/` between base and head branch
Adoption SHALL discover the PR's spec/change folder by comparing the
top-level directory names under `openspec/changes/` on the PR's head branch
against the same set on `SpecRunnerOptions.BaseBranchName`, after fetching
the head branch. A directory present on the head branch but not on the base
branch is a candidate spec folder. This discovery SHALL NOT depend on any
issue number, issue title, or branch-naming convention.

#### Scenario: Exactly one candidate folder is found
- **WHEN** spec folder discovery runs for a PR whose head branch adds exactly
  one new directory under `openspec/changes/` relative to the base branch,
  named `"add-csv-export"`
- **THEN** discovery SHALL resolve `"add-csv-export"` as the spec name for
  that PR

#### Scenario: No candidate folder is found
- **WHEN** spec folder discovery runs for a PR whose head branch adds no
  directory under `openspec/changes/` relative to the base branch
- **THEN** discovery SHALL report that no spec folder was found, without
  resolving any spec name

#### Scenario: Multiple candidate folders are found
- **WHEN** spec folder discovery runs for a PR whose head branch adds two or
  more directories under `openspec/changes/` relative to the base branch
- **THEN** discovery SHALL report all candidate directory names as
  ambiguous, without resolving any single spec name

### Requirement: Issue discovery uses the PR's closing-issue references
Adoption SHALL discover an optional associated issue number via
`IGitHubService.ListClosingIssueNumbersAsync`, which reflects GitHub's own
`closingIssuesReferences` for the pull request. Finding zero linked issues
is a valid outcome, not a failure, since a PR is not required to have an
associated issue.

#### Scenario: No linked issue is a valid outcome
- **WHEN** issue discovery runs for a PR with zero closing-issue references
- **THEN** discovery SHALL report no issue number, and adoption SHALL
  continue rather than treating this as a failure

#### Scenario: Exactly one linked issue is adopted
- **WHEN** issue discovery runs for a PR with exactly one closing-issue
  reference, issue number `45`
- **THEN** discovery SHALL resolve issue number `45` as the PR's associated
  issue

#### Scenario: Multiple linked issues are ambiguous
- **WHEN** issue discovery runs for a PR with two or more closing-issue
  references
- **THEN** discovery SHALL report all candidate issue numbers as ambiguous,
  without resolving any single issue number

### Requirement: Unambiguous discovery adopts the PR and upserts a tracked record
The workflow SHALL adopt the PR and upsert a tracked record via
`IStateStore.UpsertTrackedIssueAsync` when spec folder discovery resolves
exactly one candidate and issue discovery resolves zero or exactly one
candidate. The record SHALL use the PR's existing head branch
(`GitHubPullRequest.HeadBranch`, read directly, never reconstructed) as the
branch name, the discovered folder as the spec name, the PR number, and the
discovered issue number if one was found (otherwise the record's issue
number SHALL be absent). Processing of the triggering comment SHALL then
continue exactly as it does for a PR that was already tracked.

#### Scenario: Adoption with a discovered issue behaves like a `/propose`-created record
- **WHEN** adoption succeeds for PR `12` with spec name `"add-csv-export"`,
  branch `"contributor/csv-export"`, and discovered issue number `45`
- **THEN** a tracked record SHALL be upserted with PR number `12`, spec name
  `"add-csv-export"`, branch name `"contributor/csv-export"`, and issue
  number `45`, and the workflow SHALL proceed with that record exactly as it
  would for a tracked PR

#### Scenario: Adoption with no discovered issue proceeds without one
- **WHEN** adoption succeeds for PR `12` with spec name `"add-csv-export"`
  and branch `"contributor/csv-export"`, and issue discovery found no linked
  issue
- **THEN** a tracked record SHALL be upserted with PR number `12`, spec name
  `"add-csv-export"`, branch name `"contributor/csv-export"`, and no issue
  number, and the workflow SHALL proceed with that record

### Requirement: Ambiguous or missing discovery refuses with a specific explanation
Adoption SHALL fail when spec folder discovery finds zero or multiple
candidates, or issue discovery finds multiple candidates. On failure, the
workflow SHALL reply on the PR with a message identifying which discovery
step failed and, for the ambiguous cases, listing the candidate names or
numbers, add a `confused` reaction to the triggering comment, and SHALL NOT
perform any git operation, CLI-agent session, or state-store write for that
comment — the same consequence as today's generic untracked-PR refusal, but
with a more specific explanation.

#### Scenario: No spec folder found refuses with a specific message
- **WHEN** adoption is attempted for PR `12` and spec folder discovery finds
  no candidate folder
- **THEN** a reply comment SHALL be posted stating that no OpenSpec change
  folder was found on the branch, the triggering comment SHALL receive a
  `confused` reaction, and no git operation, CLI-agent session, or
  state-store write SHALL occur

#### Scenario: Multiple spec folders found refuses with the candidate list
- **WHEN** adoption is attempted for PR `12` and spec folder discovery finds
  candidate folders `"add-csv-export"` and `"add-pdf-export"`
- **THEN** a reply comment SHALL be posted naming both candidate folders and
  stating that which one applies can't be determined, the triggering comment
  SHALL receive a `confused` reaction, and no git operation, CLI-agent
  session, or state-store write SHALL occur

#### Scenario: Multiple linked issues found refuses with the candidate list
- **WHEN** adoption is attempted for PR `12`, spec folder discovery resolves
  a single candidate, and issue discovery finds closing-issue references
  `45` and `46`
- **THEN** a reply comment SHALL be posted naming both candidate issue
  numbers and stating that which one applies can't be determined, the
  triggering comment SHALL receive a `confused` reaction, and no git
  operation, CLI-agent session, or state-store write SHALL occur
