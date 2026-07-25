## Why

SpecRunner currently has no repeatable way to produce a distributable build
of `SpecRunner.Console`. Every publish today is a manual, ad hoc `dotnet
publish` invocation with hand-typed flags, which is easy to get wrong
(target project, runtime identifier, publish mode) and undocumented. A
single PowerShell build script gives contributors and CI a one-command,
consistent way to produce the executable in each supported publish
configuration.

## What Changes

- Add a `build/` folder at the repository root containing a PowerShell
  build script (`build.ps1`).
- The script builds/publishes
  `SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj`.
- The script supports three publish configurations: framework-dependent,
  x86 (framework-dependent, win-x86 runtime), and single-file
  (self-contained, single-file, win-x64 runtime).
- Published output is written directly into the repository-root
  `.specrunner` folder (no per-configuration subfolders).
- The script accepts a parameter selecting which publish configuration(s)
  to run, defaulting to all three.

## Capabilities

### New Capabilities
- `build-script`: A PowerShell script under `/build` that publishes
  `SpecRunner.Console` in framework-dependent, x86, and single-file
  configurations, writing output directly into `.specrunner`.

### Modified Capabilities
(none)

## Impact

- Affected code: new `build/build.ps1` file only; no application source
  changes.
- Affected paths: writes generated (git-ignored) output directly into
  `.specrunner/`, which is already excluded from source control by the
  root `.gitignore`.
- No impact on existing specs, runtime behavior, or the state store,
  beyond the file-naming-collision risk noted under Assumptions below.

## Assumptions

- "using powershell syntax create a build script" is read as: write the
  script itself in PowerShell (`build/build.ps1`), not merely a script
  that happens to invoke PowerShell.
- The repository root `.specrunner` folder already exists and is used at
  runtime for the SQLite state store (`state.db`). Per explicit
  instruction, published output is written directly into `.specrunner/`
  with no per-configuration subfolders. This means: (a) the three
  publish configurations share one output directory, so when running
  `All`, files with the same name across configurations (e.g. the main
  executable/DLLs) are overwritten by whichever configuration publishes
  last, rather than coexisting — this is accepted as the intended
  behavior of the explicit "no subfolders" instruction, not a defect;
  and (b) `dotnet publish` only ever writes/updates its own output
  files and does not touch unrelated files already present in the
  target folder, so `.specrunner/state.db` is not deleted or modified
  by the publish step, though it now sits alongside build output in the
  same folder.
- "framework dependant" is interpreted as the default `dotnet publish`
  mode (`--self-contained false`) with no runtime identifier, producing a
  portable, framework-dependent output.
- "x86" is interpreted as a framework-dependent publish targeting the
  `win-x86` runtime identifier (`-r win-x86 --self-contained false`),
  since no OS/family was specified and the rest of the project targets
  Windows-style paths.
- "single file" is interpreted as a self-contained, single-file publish
  (`--self-contained true -p:PublishSingleFile=true`) targeting the
  `win-x64` runtime identifier, since single-file publishing requires
  both a runtime identifier and self-contained mode.
- These three configurations are independent named modes the script can
  run individually or all together in one invocation; the script defaults
  to running all three when no configuration is specified.
