## Context

`SpecRunner.Console` is the sole executable entry point in the solution
(see `solution-layout`). There is currently no scripted build/publish
process; contributors would run `dotnet publish` by hand with whatever
flags they remember. The repository root already contains a git-ignored
`.specrunner` folder used at runtime by the app's SQLite state store
(`.specrunner/state.db`, see `state-store-schema`). The build script must
not collide with or disturb that file.

## Goals / Non-Goals

**Goals:**
- Provide a single PowerShell entry point (`build/build.ps1`) that
  publishes `SpecRunner.Console.csproj` in one or more of three named
  configurations: framework-dependent, x86, single-file.
- Write publish output under `.specrunner` so generated artifacts stay
  git-ignored without touching the root `.gitignore`.
- Keep each configuration's output isolated in its own subfolder so
  running one configuration doesn't overwrite another, and none of them
  touch `.specrunner/state.db`.
- Make the script runnable with no required parameters (defaults to
  building all three configurations) and runnable for a single
  configuration via a parameter.

**Non-Goals:**
- No CI/CD pipeline wiring (GitHub Actions, etc.) — script only.
- No packaging/zipping, installer generation, or version stamping beyond
  what `dotnet publish` does by default.
- No changes to `SpecRunner.Console.csproj` or other project files.
- No cross-platform shell scripts (bash/sh) — PowerShell only, per the
  request.

## Decisions

- **Script location and name**: `build/build.ps1` at the repository root.
  A dedicated top-level `build` folder (as requested) keeps build tooling
  separate from the `SpecRunner` solution folder and `openspec` planning
  folder.

- **Output root**: `<repo-root>/.specrunner/publish/<configuration>/`.
  Reusing `.specrunner` (rather than inventing a new git-ignored folder)
  matches the literal instruction to put generated resources in a folder
  named `.specrunner`. A `publish/<configuration>` subfolder layout keeps
  build output from colliding with `.specrunner/state.db` and lets all
  three configurations coexist without overwriting one another.
  Alternative considered: publish straight into `.specrunner/` root —
  rejected because `dotnet publish` output could shadow or be deleted
  alongside `state.db`, and running more than one configuration would
  overwrite the previous one's files.

- **Configuration → `dotnet publish` mapping**:
  - `FrameworkDependent`: `dotnet publish <csproj> -c Release -o <out>`
    (no `-r`, `--self-contained false` implicit default) — portable IL,
    requires the .NET 10 runtime on the target machine.
  - `X86`: `dotnet publish <csproj> -c Release -r win-x86
    --self-contained false -o <out>` — framework-dependent but targeting
    the 32-bit Windows runtime, matching the existing `win-x86` build
    output already observed under `bin/Debug/net10.0/win-x86`.
  - `SingleFile`: `dotnet publish <csproj> -c Release -r win-x64
    --self-contained true -p:PublishSingleFile=true -o <out>` —
    self-contained single executable; single-file publishing requires
    both a runtime identifier and self-contained mode, so `win-x64` is
    used as the default 64-bit Windows target.
  Each configuration publishes to its own `<out>` = `.specrunner/publish/<name>`.

- **Parameterization**: a single `-Configuration` parameter accepting
  `FrameworkDependent`, `X86`, `SingleFile`, or `All` (default `All`),
  validated with `[ValidateSet]`. Keeps the script simple (one switch,
  no combinatorial flag parsing) while still letting a caller build just
  one variant.

- **Failure behavior**: the script sets `$ErrorActionPreference = 'Stop'`
  and checks `$LASTEXITCODE` after each `dotnet publish` invocation,
  exiting non-zero immediately on the first failure rather than
  continuing to the next configuration. Rationale: a partial/broken build
  silently left in `.specrunner/publish` is worse than stopping fast and
  surfacing the failing configuration clearly.

## Risks / Trade-offs

- [Reusing `.specrunner` for both runtime state and build output could
  confuse contributors who expect `.specrunner` to be purely runtime
  state] → Mitigated by scoping build output to a distinct
  `.specrunner/publish/` subfolder and documenting the layout via
  script comments/README note, not by mixing files at the same level.
- [`win-x86`/`win-x64` runtime identifiers are Windows-specific] →
  Acceptable per explicit request for PowerShell/x86; not a design
  concern for this change since no cross-platform requirement was given.
- [No cleanup step for stale publish output between runs] → `dotnet
  publish` overwrites files in place per configuration folder; acceptable
  for a first version, and not required by the request.
