## Context

`SpecRunner.Console` is the sole executable entry point in the solution
(see `solution-layout`). There is currently no scripted build/publish
process; contributors would run `dotnet publish` by hand with whatever
flags they remember. The repository root already contains a git-ignored
`.specrunner` folder used at runtime by the app's SQLite state store
(`.specrunner/state.db`, see `state-store-schema`). The build script
writes its output into a separate `.specrunner` folder nested inside
`build/` (`build/.specrunner`), so it never shares a directory with, and
cannot collide with or disturb, the repository-root runtime state
folder.

## Goals / Non-Goals

**Goals:**
- Provide a single PowerShell entry point (`build/build.ps1`) that
  publishes `SpecRunner.Console.csproj` in one or more of three named
  configurations: framework-dependent, x86, single-file.
- Write publish output directly into a `.specrunner` folder created
  inside `build/` (`build/.specrunner`, no per-configuration subfolders)
  so generated artifacts are clearly build-tool-owned and stay git-ignored
  via a dedicated `.gitignore` entry.
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

- **Output root**: `<repo-root>/build/.specrunner/`. Per explicit
  instruction, the `.specrunner` output folder is created inside `build/`
  (not at the repository root), and all publish configurations write
  directly into it with no per-configuration subfolders. This supersedes
  both the earlier `.specrunner/publish/<configuration>/` layout and the
  interim repository-root `.specrunner/` layout. Consequence: running
  more than one configuration in the same invocation (e.g. the default
  `All`) means later configurations' output files can overwrite earlier
  configurations' files of the same name in `build/.specrunner/` —
  accepted as intended given the explicit "no subfolders" requirement.
  Because this output root is nested under `build/` rather than the
  repository root, it is a distinct folder from the repository-root
  `.specrunner/state.db` used by the SQLite state store — the two no
  longer share a directory at all, so the earlier collision risk is moot.
  The script resolves this path via `$PSScriptRoot` (the `build/` folder
  the script lives in), so it works regardless of the caller's working
  directory.

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
  Every configuration publishes to the same `<out>` = `build/.specrunner/`.

- **Parameterization**: a single `-Configuration` parameter accepting
  `FrameworkDependent`, `X86`, `SingleFile`, or `All` (default `All`),
  validated with `[ValidateSet]`. Keeps the script simple (one switch,
  no combinatorial flag parsing) while still letting a caller build just
  one variant.

- **Failure behavior**: the script sets `$ErrorActionPreference = 'Stop'`
  and checks `$LASTEXITCODE` after each `dotnet publish` invocation,
  exiting non-zero immediately on the first failure rather than
  continuing to the next configuration. Rationale: a partial/broken build
  silently left in `build/.specrunner` is worse than stopping fast and
  surfacing the failing configuration clearly.

## Risks / Trade-offs

- [Nesting `.specrunner` inside `build/` adds a second, differently-scoped
  `.specrunner` folder to the repository (root one for runtime state,
  `build/.specrunner` for publish output), which could read as
  duplication] → Accepted: this actually removes the earlier risk of the
  two purposes sharing one directory, at the cost of two folders with
  the same base name in the tree. The existing root `.gitignore` entry
  (`/.specrunner`) does not match the nested path, so a separate
  `/build/.specrunner` entry is added to `.gitignore`.
- [Publishing all three configurations into the same flat
  `build/.specrunner` folder means files with matching names across
  configurations overwrite each other, so after an `All` run only the
  last-published configuration's copies of shared filenames remain] →
  Accepted as the direct consequence of the explicit "no subfolders"
  requirement; a caller who needs all three outputs preserved
  simultaneously must run each configuration separately and move/rename
  the output between runs, which is outside this script's scope.
- [`win-x86`/`win-x64` runtime identifiers are Windows-specific] →
  Acceptable per explicit request for PowerShell/x86; not a design
  concern for this change since no cross-platform requirement was given.
- [No cleanup step for stale publish output between runs] → `dotnet
  publish` overwrites files in place; acceptable for a first version,
  and not required by the request.
