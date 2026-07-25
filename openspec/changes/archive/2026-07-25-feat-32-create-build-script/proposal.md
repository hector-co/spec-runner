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
- Published output is written directly into a `.specrunner` folder
  created inside the `build/` folder (`build/.specrunner`), not the
  repository root (no per-configuration subfolders).
- The script accepts a parameter selecting which publish configuration(s)
  to run, defaulting to all three.

## Capabilities

### New Capabilities
- `build-script`: A PowerShell script under `/build` that publishes
  `SpecRunner.Console` in framework-dependent, x86, and single-file
  configurations, writing output directly into `build/.specrunner`.

### Modified Capabilities
(none)

## Impact

- Affected code: `build/build.ps1`, plus a `.gitignore` addition for the
  new nested output path; no application source changes.
- Affected paths: writes generated output directly into
  `build/.specrunner/`. The existing root `.gitignore` entry
  (`/.specrunner`) only matches the repository-root folder, so a new
  `/build/.specrunner` entry is added to keep the build output
  git-ignored.
- No impact on existing specs, runtime behavior, or the state store:
  moving build output into `build/.specrunner` puts it in a different
  folder than the repository-root `.specrunner/state.db` used by the
  SQLite state store, which removes the file-naming-collision risk
  previously noted for this change.

## Assumptions

- "using powershell syntax create a build script" is read as: write the
  script itself in PowerShell (`build/build.ps1`), not merely a script
  that happens to invoke PowerShell.
- Published output is written directly into `build/.specrunner/` with no
  per-configuration subfolders, so the three publish configurations
  share one output directory: when running `All`, files with the same
  name across configurations (e.g. the main executable/DLLs) are
  overwritten by whichever configuration publishes last, rather than
  coexisting — this is accepted as the intended behavior of the explicit
  "no subfolders" instruction, not a defect.
- Update (this revision): the `.specrunner` output folder is now created
  inside `build/` (`build/.specrunner`) instead of at the repository
  root. The repository root also has its own separate `.specrunner`
  folder used at runtime for the SQLite state store (`state.db`), which
  this script never touches now that build output lives under `build/`
  instead of sharing that root folder. Since the existing root
  `.gitignore` entry (`/.specrunner`) does not match the new nested
  `build/.specrunner` path, a `/build/.specrunner` entry is added to
  `.gitignore` so the relocated build output stays git-ignored.
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
- During verification, an internal helper function originally took a
  parameter named `$Args`. PowerShell parameter names are case-insensitive
  and `$args` is a reserved automatic variable, so this silently broke
  argument forwarding to `dotnet publish` (the `-r`/`--self-contained`
  flags were dropped for the `X86` step in some invocations, publishing
  the wrong architecture). Fixed by renaming the parameter to
  `$PublishArgs`. Verified afterward that `-Configuration X86` and
  `-Configuration SingleFile` each produce correctly-architected output
  (PE32/Intel i386 for X86, a single ~76 MB self-contained PE32+ exe with
  no sibling managed DLLs for SingleFile), and that `.specrunner/state.db`
  (and `-shm`/`-wal`) stay byte-for-byte unchanged across all runs.
- This revision also reworded the "Build failure stops the script"
  requirement's opening sentence (moving `SHALL` earlier) so `openspec
  validate --strict` recognizes it as a proper requirement statement —
  a pre-existing formatting issue unrelated to the `.specrunner`
  relocation, fixed while re-validating this change.
- Archival (unattended run): all artifacts and tasks were already marked
  complete, so no tasks needed to be checked off. `build-script` is a new
  capability (no existing main spec to conflict with) and its delta spec
  is purely `ADDED Requirements`, so the delta spec was synced into
  `openspec/specs/build-script/spec.md` before archiving, per the
  skill's recommended default.
