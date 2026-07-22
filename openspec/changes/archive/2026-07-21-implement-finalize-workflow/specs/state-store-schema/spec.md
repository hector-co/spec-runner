## MODIFIED Requirements

### Requirement: Git and GitHub service contracts are implemented for the propose workflow
`SpecRunner.Core` SHALL define `IGitService` (covering create branch,
switch branch, commit, push, pull, and discarding local changes via a
hard reset) and `IGitHubService` (covering create PR, create draft PR,
read PR comments, write PR comments, mark PR ready for review, resolving
the authenticated bot identity, listing open issues with comments,
reading/adding comment reactions, and creating an issue comment) as
interfaces. `SpecRunner.Git` SHALL provide a real implementation of every
`IGitService` member (see `git-operations`). `SpecRunner.GitHub` SHALL
provide a real implementation of every `IGitHubService` member the
`propose-workflow`, `implement-workflow`, `update-workflow`, and
`finalize-workflow` capabilities depend on — authenticated identity
resolution, listing open issues with comments, reading/adding comment
reactions, creating an issue comment, creating a draft PR, listing open
pull requests, reading/writing PR comments, and marking a PR ready for
review (see `github-operations`) — while `CreatePullRequestAsync`
(non-draft) remains a `NotImplementedException` placeholder until a
future change needs it.

#### Scenario: Git service operations perform real git operations
- **WHEN** a method on the registered `IGitService` implementation is
  called
- **THEN** it SHALL perform the corresponding git operation against the
  local clone rather than throwing `NotImplementedException`

#### Scenario: Implemented GitHub service members perform real API calls
- **WHEN** `CreateDraftPullRequestAsync`, an issue-listing operation, a
  comment-reaction operation, `CreateIssueCommentAsync`, a PR-listing
  operation, a PR-comment read/write operation, or the
  mark-ready-for-review operation is called on the registered
  `IGitHubService` implementation
- **THEN** it SHALL perform the corresponding GitHub REST or GraphQL API
  call rather than throwing `NotImplementedException`

#### Scenario: Not-yet-needed GitHub service members remain placeholders
- **WHEN** `CreatePullRequestAsync` (non-draft) is called on the
  registered `IGitHubService` implementation
- **THEN** it SHALL still throw `NotImplementedException`, since no
  current capability depends on it yet
