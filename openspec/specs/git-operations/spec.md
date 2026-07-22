# git-operations

## Purpose

TBD - defines the real `SpecRunner.Git` implementation of `IGitService`:
pulling, hard-resetting, branching, committing, and pushing against the
local clone, plus failure reporting.

## Requirements

### Requirement: Git service pulls the configured base branch
`SpecRunner.Git`'s `IGitService` implementation SHALL provide a
`PullAsync` operation that fetches and fast-forwards the local clone at
`SpecRunnerOptions.LocalRepositoryPath` to the current tip of
`SpecRunnerOptions.BaseBranchName` on `origin`.

#### Scenario: Pull fast-forwards the local base branch
- **WHEN** `PullAsync` is called against a local clone whose base branch is
  behind `origin`
- **THEN** the local base branch SHALL be fast-forwarded to match `origin`'s
  tip

### Requirement: Git service discards local changes via hard reset
`IGitService` SHALL provide a `ResetHardAsync` operation that discards all
uncommitted changes (staged, unstaged, and untracked files subject to
`git clean` semantics) in the local clone and resets the working tree to a
given ref, so each workflow run starts from a known-clean state regardless
of what a previous run left behind.

#### Scenario: Reset discards uncommitted changes
- **WHEN** `ResetHardAsync` is called against a local clone with
  uncommitted modifications and untracked files
- **THEN** the working tree SHALL match the target ref exactly, with no
  uncommitted modifications or leftover untracked files from before the
  call

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

### Requirement: Git service commits and pushes
`IGitService` SHALL provide `CommitAsync`, staging all pending changes in
the working tree and creating a commit with a supplied message, and
`PushAsync`, pushing the current branch to `origin` and setting it to
track the matching remote branch if it does not already.

#### Scenario: Commit stages and commits all pending changes
- **WHEN** `CommitAsync` is called with message `"adding specs for #45"`
  while the working tree has new and modified files
- **THEN** all pending changes SHALL be staged and committed with that
  message

#### Scenario: Push publishes the branch to origin
- **WHEN** `PushAsync` is called for a local branch that does not yet
  exist on `origin`
- **THEN** the branch SHALL be pushed to `origin` and set to track the
  newly created remote branch

### Requirement: Git operations report failures without throwing raw process exceptions
Each `IGitService` operation SHALL surface underlying git command failures
(non-zero exit code) as a typed result or a specific, catchable exception
distinguishable from a successful outcome, rather than letting an
unstructured process-exit exception propagate uncaught.

#### Scenario: A failing git command is reported, not left to crash the caller
- **WHEN** any `IGitService` operation's underlying git command exits with
  a non-zero code
- **THEN** the operation SHALL report the failure (including the
  command's captured stderr) through its return value or a specific
  exception type, without an unhandled/unstructured exception escaping the
  call
