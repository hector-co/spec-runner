## ADDED Requirements

### Requirement: Git service fetches an arbitrary remote branch
`IGitService` SHALL provide a `FetchAsync` operation that fetches a given
branch name from `origin` into the local clone at
`SpecRunnerOptions.LocalRepositoryPath`, without checking it out or
merging it, so a caller can refresh a branch other than
`SpecRunnerOptions.BaseBranchName` before switching to and resetting it.

#### Scenario: Fetch retrieves a named branch's current tip from origin
- **WHEN** `FetchAsync` is called with branch name `"feature/45"` while
  `origin/feature/45` has commits not yet present locally
- **THEN** those commits SHALL be fetched into the local clone as
  `origin/feature/45`, and the currently checked-out branch SHALL be
  unchanged
