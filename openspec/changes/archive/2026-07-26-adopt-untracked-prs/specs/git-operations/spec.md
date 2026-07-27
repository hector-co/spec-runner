## ADDED Requirements

### Requirement: Git service lists spec/change folders added on a branch relative to base
`IGitService` SHALL provide a `ListAddedSpecFolderNamesAsync` operation
that, given a base branch name and a head branch name (both already fetched
into the local clone), returns the top-level directory names under
`openspec/changes/` that exist on the head branch but not on the base
branch, without checking out or otherwise modifying the currently checked
out branch. This lets a caller discover which spec/change folder a branch
introduces without assuming any naming convention.

#### Scenario: One added folder is returned
- **WHEN** `ListAddedSpecFolderNamesAsync` is called with base branch
  `"main"` and head branch `"contributor/csv-export"`, and
  `openspec/changes/add-csv-export/` exists on `"contributor/csv-export"`
  but not on `"main"`
- **THEN** the result SHALL contain exactly `"add-csv-export"`

#### Scenario: No added folders is a valid, empty result
- **WHEN** `ListAddedSpecFolderNamesAsync` is called for a head branch that
  adds no directory under `openspec/changes/` relative to the base branch
- **THEN** the result SHALL be empty, without an error

#### Scenario: Multiple added folders are all returned
- **WHEN** `ListAddedSpecFolderNamesAsync` is called for a head branch that
  adds two directories under `openspec/changes/` relative to the base
  branch
- **THEN** the result SHALL contain both directory names

#### Scenario: The currently checked out branch is left unchanged
- **WHEN** `ListAddedSpecFolderNamesAsync` is called while a different
  branch is currently checked out in the local clone
- **THEN** the currently checked out branch SHALL remain checked out after
  the call returns

#### Scenario: A failing git command is reported, not left to crash the caller
- **WHEN** the underlying git command for `ListAddedSpecFolderNamesAsync`
  exits with a non-zero code
- **THEN** the operation SHALL report the failure (including the command's
  captured stderr) through its return value or a specific exception type,
  without an unhandled/unstructured exception escaping the call
