## 1. Script scaffolding

- [ ] 1.1 Create the `build/` folder at the repository root.
- [ ] 1.2 Create `build/build.ps1` with `[CmdletBinding()]`, a
  `-Configuration` parameter (`[ValidateSet('All','FrameworkDependent','X86','SingleFile')]`,
  default `All`), and `$ErrorActionPreference = 'Stop'`.
- [ ] 1.3 Resolve the repository root, the `SpecRunner.Console.csproj`
  path (`SpecRunner/src/SpecRunner.Console/SpecRunner.Console.csproj`),
  and the `.specrunner/publish` output root relative to the script's own
  location so the script works regardless of the caller's working
  directory.

## 2. Publish configurations

- [ ] 2.1 Implement the `FrameworkDependent` step: `dotnet publish
  <csproj> -c Release -o .specrunner/publish/FrameworkDependent` (no
  runtime identifier, not self-contained).
- [ ] 2.2 Implement the `X86` step: `dotnet publish <csproj> -c Release
  -r win-x86 --self-contained false -o .specrunner/publish/X86`.
- [ ] 2.3 Implement the `SingleFile` step: `dotnet publish <csproj> -c
  Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o
  .specrunner/publish/SingleFile`.
- [ ] 2.4 Wire `-Configuration` so `All` runs all three steps in order
  (`FrameworkDependent`, `X86`, `SingleFile`) and a specific value runs
  only that one step.

## 3. Failure handling

- [ ] 3.1 After each `dotnet publish` invocation, check `$LASTEXITCODE`
  and stop the script with a non-zero exit code (`exit 1`) before running
  any subsequent configuration if it is non-zero.

## 4. Verification

- [ ] 4.1 Run `build/build.ps1` with no arguments from the repository
  root and confirm all three configurations publish successfully into
  their respective `.specrunner/publish/<configuration>` subfolders.
- [ ] 4.2 Run `build/build.ps1 -Configuration X86` and confirm only the
  `X86` subfolder is written/updated.
- [ ] 4.3 Confirm `.specrunner/state.db` (and `-shm`/`-wal` files) are
  unchanged after running the script.
- [ ] 4.4 Confirm the single-file output under
  `.specrunner/publish/SingleFile` is a self-contained single executable
  (no separate managed DLL dependencies alongside it, aside from native
  runtime files).
