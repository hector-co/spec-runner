## ADDED Requirements

### Requirement: A reader resolves a change's current tasks.md content
`SpecRunner.Core` SHALL define an `ITasksFileReader` with a
`ReadCurrentAsync` operation that, given a resolved spec/change name, reads
`{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/{spec-name}/tasks.md`
from the local working tree and returns its content, or `null` if that file
does not exist.

#### Scenario: Existing tasks.md content is returned
- **WHEN** `ReadCurrentAsync` is called with spec name
  `"45-add-login-page"` and
  `openspec/changes/45-add-login-page/tasks.md` exists in the local
  working tree
- **THEN** that file's content SHALL be returned

#### Scenario: Missing tasks.md returns null
- **WHEN** `ReadCurrentAsync` is called with a spec name for which no
  `openspec/changes/{spec-name}/tasks.md` file exists
- **THEN** `null` SHALL be returned

### Requirement: A reader resolves a change's archived tasks.md content
`ITasksFileReader` SHALL provide a `ReadArchivedAsync` operation that,
given a resolved spec/change name, locates a directory under
`{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/archive/` whose
name ends with `-{spec-name}` and reads that directory's `tasks.md`
content, returning `null` if no such directory (or no `tasks.md` within
it) exists. If more than one matching directory exists, the one whose
`tasks.md` was most recently modified SHALL be used.

#### Scenario: Archived tasks.md content is returned by suffix match
- **WHEN** `ReadArchivedAsync` is called with spec name
  `"45-add-login-page"` and
  `openspec/changes/archive/2026-07-21-45-add-login-page/tasks.md` exists
- **THEN** that file's content SHALL be returned

#### Scenario: No matching archive directory returns null
- **WHEN** `ReadArchivedAsync` is called with a spec name for which no
  `openspec/changes/archive/*-{spec-name}/` directory exists
- **THEN** `null` SHALL be returned

#### Scenario: Multiple matching archive directories resolve to the most recently modified
- **WHEN** `ReadArchivedAsync` is called with a spec name for which more
  than one `openspec/changes/archive/*-{spec-name}/tasks.md` file exists
- **THEN** the content of the most recently modified matching `tasks.md`
  SHALL be returned
