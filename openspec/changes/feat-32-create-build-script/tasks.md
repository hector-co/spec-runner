## 1. Script scaffolding

- [x] 1.1 Create the `build/` folder at the repository root.
- [x] 1.2 Create `build/build.ps1` with `[CmdletBinding()]`, a
  `-Configuration` parameter (`[ValidateSet('All','FrameworkDependent','X86','SingleFile')]`,
  default `All`), and `$ErrorActionPreference = 'Stop'`.
- [x] 1.3 Resolve the repository root, the `SpecRunner.Console.csproj`
  path (`SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj`),
  and the `.specrunner` output root relative to the script's own
  location so the script works regardless of the caller's working
  directory.

## 2. Publish configurations

- [x] 2.1 Implement the `FrameworkDependent` step: `dotnet publish
  <csproj> -c Release -o .specrunner` (no runtime identifier, not
  self-contained).
- [x] 2.2 Implement the `X86` step: `dotnet publish <csproj> -c Release
  -r win-x86 --self-contained false -o .specrunner`.
- [x] 2.3 Implement the `SingleFile` step: `dotnet publish <csproj> -c
  Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o
  .specrunner`.
- [x] 2.4 Wire `-Configuration` so `All` runs all three steps in order
  (`FrameworkDependent`, `X86`, `SingleFile`) and a specific value runs
  only that one step.

## 3. Failure handling

- [x] 3.1 After each `dotnet publish` invocation, check `$LASTEXITCODE`
  and stop the script with a non-zero exit code (`exit 1`) before running
  any subsequent configuration if it is non-zero.

## 4. Verification

- [x] 4.1 Run `build/build.ps1` with no arguments from the repository
  root and confirm all three configurations publish successfully,
  writing their output directly into `.specrunner/` with no
  per-configuration subfolders (last-published configuration's
  like-named files win).
- [x] 4.2 Run `build/build.ps1 -Configuration X86` on its own and confirm
  its output is written directly into `.specrunner/`.
- [x] 4.3 Confirm `.specrunner/state.db` (and `-shm`/`-wal` files) are
  unchanged after running the script.
- [x] 4.4 Run `build/build.ps1 -Configuration SingleFile` on its own and
  confirm the output directly in `.specrunner/` is a self-contained
  single executable (no separate managed DLL dependencies alongside it,
  aside from native runtime files).
