## ADDED Requirements

### Requirement: A resolver confirms the expected spec folder exists on disk
`SpecRunner.Core` SHALL define an `ISpecFolderResolver` with a
`ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken)`
operation. `SpecRunner.Console` SHALL provide an implementation that, given
an expected spec/change name, checks whether
`{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/{expectedSpecName}`
exists as a directory; if it does, `ResolveAsync` SHALL return
`expectedSpecName` unchanged.

#### Scenario: Expected folder exists
- **WHEN** `ResolveAsync` is called with expected spec name
  `"feat-45-add-login-page"` and issue number `45`, and
  `openspec/changes/feat-45-add-login-page/` exists in the local working
  tree
- **THEN** `ResolveAsync` SHALL return `"feat-45-add-login-page"`

### Requirement: A resolver falls back to a prefix match keyed by issue number
When the expected spec folder does not exist, `ISpecFolderResolver` SHALL
look for a directory directly under
`{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/` whose name
starts with `feat-{issueNumber}-`, and, if one is found, SHALL return that
directory's name (instead of the originally expected name) as the resolved
spec name. If more than one directory matches the prefix, the first one
encountered SHALL be used.

#### Scenario: Expected folder missing, fallback prefix match found
- **WHEN** `ResolveAsync` is called with expected spec name
  `"feat-45-add-login-page"` and issue number `45`,
  `openspec/changes/feat-45-add-login-page/` does not exist, but
  `openspec/changes/feat-45-login-page/` does exist
- **THEN** `ResolveAsync` SHALL return `"feat-45-login-page"`

### Requirement: A resolver reports an error when no matching folder exists
When neither the expected spec folder nor any directory matching the
`feat-{issueNumber}-` prefix exists under
`{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/`,
`ResolveAsync` SHALL throw, rather than returning a name that doesn't
correspond to any folder on disk.

#### Scenario: No matching folder exists at all
- **WHEN** `ResolveAsync` is called with expected spec name
  `"feat-45-add-login-page"` and issue number `45`, and no directory under
  `openspec/changes/` matches either the expected name or the
  `feat-45-` prefix
- **THEN** `ResolveAsync` SHALL throw an exception rather than returning a
  value
