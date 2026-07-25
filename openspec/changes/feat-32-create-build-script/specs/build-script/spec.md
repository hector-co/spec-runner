## ADDED Requirements

### Requirement: PowerShell build script location
The repository SHALL contain a PowerShell build script at
`build/build.ps1`, located under a top-level `build` folder.

#### Scenario: Build script exists at the expected path
- **WHEN** the repository is inspected
- **THEN** a file SHALL exist at `build/build.ps1`

### Requirement: Build script publishes SpecRunner.Console
The build script SHALL publish
`SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj` via `dotnet
publish`, and SHALL NOT target any other project in the solution.

#### Scenario: Script invokes dotnet publish against the console project
- **WHEN** `build/build.ps1` is run
- **THEN** it SHALL invoke `dotnet publish` with
  `SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj` as the
  target project for every publish configuration it runs

### Requirement: Three publish configurations
The build script SHALL support three named publish configurations:
`FrameworkDependent` (portable, no runtime identifier, not
self-contained), `X86` (framework-dependent, `win-x86` runtime
identifier), and `SingleFile` (self-contained, single-file, `win-x64`
runtime identifier). The script SHALL accept a parameter selecting which
configuration(s) to run, and SHALL run all three when the parameter is
omitted.

#### Scenario: Running without a configuration parameter builds all three
- **WHEN** `build/build.ps1` is run with no `-Configuration` argument
- **THEN** it SHALL publish `SpecRunner.Console` using the
  `FrameworkDependent`, `X86`, and `SingleFile` configurations

#### Scenario: Running with a specific configuration builds only that one
- **WHEN** `build/build.ps1` is run with `-Configuration X86`
- **THEN** it SHALL publish `SpecRunner.Console` using only the `X86`
  configuration, and SHALL NOT publish the `FrameworkDependent` or
  `SingleFile` configurations

#### Scenario: X86 configuration is framework-dependent and 32-bit
- **WHEN** the `X86` configuration is run
- **THEN** the `dotnet publish` invocation SHALL target runtime
  identifier `win-x86` and SHALL NOT set self-contained mode to `true`

#### Scenario: SingleFile configuration is self-contained and single-file
- **WHEN** the `SingleFile` configuration is run
- **THEN** the `dotnet publish` invocation SHALL set self-contained mode
  to `true` and SHALL set `PublishSingleFile` to `true`

### Requirement: Published output location
Each publish configuration SHALL write its output under the
repository-root `.specrunner` folder, in a subfolder unique to that
configuration, so that output from one configuration does not overwrite
another configuration's output and none of them write into or delete
`.specrunner/state.db`.

#### Scenario: FrameworkDependent output goes to its own subfolder
- **WHEN** the `FrameworkDependent` configuration is run
- **THEN** published output SHALL be written under
  `.specrunner/publish/FrameworkDependent`

#### Scenario: X86 output goes to its own subfolder
- **WHEN** the `X86` configuration is run
- **THEN** published output SHALL be written under
  `.specrunner/publish/X86`

#### Scenario: SingleFile output goes to its own subfolder
- **WHEN** the `SingleFile` configuration is run
- **THEN** published output SHALL be written under
  `.specrunner/publish/SingleFile`

#### Scenario: Running all configurations does not touch the state file
- **WHEN** `build/build.ps1` is run with no `-Configuration` argument
  while `.specrunner/state.db` exists
- **THEN** `.specrunner/state.db` SHALL remain unmodified and undeleted
  after the script completes

### Requirement: Build failure stops the script
If a `dotnet publish` invocation for any configuration fails, the build
script SHALL stop immediately with a non-zero exit code rather than
continuing to the next configuration.

#### Scenario: A failing publish step halts remaining configurations
- **WHEN** `build/build.ps1` is run with no `-Configuration` argument and
  the `dotnet publish` invocation for the `FrameworkDependent`
  configuration fails
- **THEN** the script SHALL exit with a non-zero exit code and SHALL NOT
  run the `X86` or `SingleFile` configurations
