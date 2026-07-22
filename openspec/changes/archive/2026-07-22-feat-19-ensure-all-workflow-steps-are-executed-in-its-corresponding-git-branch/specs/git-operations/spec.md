## ADDED Requirements

### Requirement: Git service checks whether a branch name is already taken
`IGitService` SHALL provide a `BranchExistsAsync` operation that reports
whether a branch with a given name already exists, checking both local
branch refs in the clone and branch refs on `origin`, so a caller can
detect a name collision before creating a branch even if a prior run
pushed that branch without ever recording it locally.

#### Scenario: Existing local branch is reported
- **WHEN** `BranchExistsAsync` is called with `"feature/45"` and a local
  branch named `"feature/45"` already exists in the clone
- **THEN** the operation SHALL return `true`

#### Scenario: Existing remote-only branch is reported
- **WHEN** `BranchExistsAsync` is called with `"feature/45"`, no local
  branch by that name exists, but `origin` has a branch named
  `"feature/45"`
- **THEN** the operation SHALL return `true`

#### Scenario: Unused branch name is reported as available
- **WHEN** `BranchExistsAsync` is called with a branch name that exists
  neither locally nor on `origin`
- **THEN** the operation SHALL return `false`

## MODIFIED Requirements

### Requirement: Git service creates and switches branches
`IGitService` SHALL provide `CreateBranchAsync`, creating a new local
branch from the current `HEAD`, and `SwitchBranchAsync`, checking out an
existing local branch, as distinct operations. `CreateBranchAsync` SHALL
NOT overwrite an existing branch of the same name; if a branch with the
given name already exists locally, it SHALL report the failure through the
same failure-reporting contract as any other `IGitService` operation
rather than silently repointing the existing branch.

#### Scenario: Create branch from current HEAD
- **WHEN** `CreateBranchAsync` is called with branch name
  `"feature/45"` while `HEAD` is at the base branch
- **THEN** a new local branch `"feature/45"` SHALL be created pointing at
  the same commit as `HEAD`

#### Scenario: Switch to an existing branch
- **WHEN** `SwitchBranchAsync` is called with the name of a branch that
  already exists locally
- **THEN** the working tree SHALL be checked out to that branch

#### Scenario: Creating a branch that already exists locally fails instead of overwriting it
- **WHEN** `CreateBranchAsync` is called with branch name `"feature/45"`
  while a local branch named `"feature/45"` already exists and points at a
  different commit than `HEAD`
- **THEN** the existing `"feature/45"` branch's target commit SHALL be
  left unchanged, and the operation SHALL report the failure through the
  same failure-reporting contract as any other `IGitService` operation
