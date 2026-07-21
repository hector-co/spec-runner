# SpecRunner

## Project layout

```
SpecRunner/
  SpecRunner.sln
  Directory.Build.props    - sets TargetFramework (net10.0) for every project
  Directory.Build.targets  - sets ImplicitUsings/Nullable for every project
  Directory.Packages.props - central NuGet package version management
  src/
    SpecRunner.Core     - domain models and service abstractions (no project deps)
    SpecRunner.Git      - git operations against the local clone (shells out to `git`)
    SpecRunner.GitHub   - GitHub REST API operations
    SpecRunner.State    - SQLite-backed local state store
    SpecRunner.Cli      - CLI-agent process execution (Claude Code CLI today)
    SpecRunner.Console  - entry point; composes the above via generic host + DI
  tests/
    SpecRunner.Tests    - xUnit tests for Core, GitHub, State, and Cli
```

`SpecRunner.Console` references `SpecRunner.Core`, `SpecRunner.Git`,
`SpecRunner.GitHub`, `SpecRunner.State`, and `SpecRunner.Cli`. `SpecRunner.Git`,
`SpecRunner.GitHub`, `SpecRunner.State`, and `SpecRunner.Cli` each reference only
`SpecRunner.Core` and do not reference each other.

## Configuration

`SpecRunner.Console` binds `SpecRunnerOptions` from configuration (`appsettings.json`,
environment variables, and user secrets in Development) under the `SpecRunner` section:

- `GitHubToken` - GitHub personal access token. Do not commit a real value; set it via
  an environment variable or `dotnet user-secrets set SpecRunner:GitHubToken <token>`
  from `src/SpecRunner.Console`.
- `RepositoryUrl` - the target GitHub repository as an HTTPS URL, e.g.
  `https://github.com/owner/repo` (a trailing `.git` is also accepted). Owner/repo are
  derived from this URL; SSH URLs and non-GitHub hosts are not supported.
- `LocalRepositoryPath` - local clone path used to derive the default state file
  location (`<LocalRepositoryPath>/.specrunner/state.db`).
- `BaseBranchName` - base branch used for PRs (defaults to `main`).
- `TaskTimeout` - per-task timeout, e.g. `"00:10:00"` for 10 minutes.
- `PollingInterval` - delay between poll cycles (each running one `propose-workflow` scan
  pass then one `implement-workflow` scan pass), e.g. `"00:00:10"` for 10 seconds (the
  default).

`SpecRunner.Console` also binds `CliAgentOptions` under the `CliAgent` section:

- `Executable` - the CLI-based coding agent executable to launch (defaults to `"claude"`,
  the Claude Code CLI).
- `Arguments` - extra command-line arguments passed to the executable, in addition to the
  stream-json protocol flags SpecRunner.Cli always appends.
- `WorkingDirectory` - working directory for the launched process. Falls back to
  `SpecRunner:LocalRepositoryPath` when unset.

## CLI agent execution

`SpecRunner.Cli` provides the process-execution primitive that runs a CLI-based coding
agent as a child process: `ICliAgentSessionFactory` (registered as a singleton) creates a
new `ICliAgentSession` per conversation via `CreateSession()`. Each session wraps one
child process and its stdin/stdout for the session's lifetime and exposes:

- `StartAsync(prompt)` - launches the configured executable and sends the initial prompt
  as the first user turn; transitions `NotStarted` → `Running`.
- `ReadEventsAsync()` - an `IAsyncEnumerable<CliAgentEvent>` streamed as the process
  produces output (`AssistantMessage`, `ToolUse`, `ToolResult`, `SystemInfo`, `Error`,
  `ResultCompleted`), not only after the process exits. Unparseable or unrecognized
  stream-json lines surface as `Error` events instead of throwing or being dropped.
- `SendCommandAsync(text)` - sends a follow-up user turn into a `Running` session
  (multi-turn), without restarting the process.
- `CancelCurrentRequestAsync()` - sends an interrupt control message to stop the current
  in-flight turn while leaving the process and session `Running`.
- `CloseInputAsync()` - closes the underlying process's standard input while leaving the
  process running and the session in state `Running`, so a caller that has finished
  sending turns can signal end-of-input and let the process notice stdin closed and exit
  on its own. Unlike `StopAsync()`, this does not wait for exit, force-kill the process, or
  change `State`; the session still reaches `Completed`/`Failed` through the normal
  process-exit path. Valid only in state `Running`; throws `InvalidOperationException`
  otherwise.
- `StopAsync()` - closes stdin, waits briefly for a graceful exit, then force-kills the
  process if needed, transitioning to `Stopped`. `ICliAgentSession` implements
  `IAsyncDisposable` and stops the process on disposal if not already in a terminal
  state (`Completed`, `Failed`, `Stopped`).

`ClaudeCliAgentSession` (the default, Claude-CLI-specific implementation) launches the
executable with `--print --verbose --input-format stream-json --output-format
stream-json --dangerously-skip-permissions` so a single long-lived process exchanges
newline-delimited JSON on stdin/stdout across multiple turns without ever blocking on a
permission prompt - SpecRunner runs unattended, so there is no human available to answer
one.

This is a process-execution primitive only: it decides neither when to start a session
nor what prompt to send - that's the `propose-workflow`'s job below.

## Propose workflow

After a successful repository connection test, `SpecRunner.Console` repeatedly runs scan
passes (`IProposeWorkflowRunner.RunOnceAsync`, implemented by `ProposeWorkflowRunner`) over
the configured repository's open issues (see Status below for the polling loop):

1. It lists open issues and their comments, and treats a comment as an eligible trigger
   when its body, trimmed of leading/trailing whitespace, is exactly `/propose` or starts
   with `/propose` followed by whitespace (so `/proposed` or `/propose` used mid-sentence
   does not trigger).
2. A comment already carrying an `eyes`, `rocket`, or `confused` reaction from the
   authenticated bot identity is skipped, so re-running the console app never reprocesses
   a comment that is already in-progress, done, or errored.
3. Each remaining eligible comment is processed in turn (never concurrently, since all
   comments share the one local clone). Processing starts by adding an `eyes` reaction,
   then either:
   - **Issue already has a PR** (per the state store): posts a reply pointing at the
     existing PR and adds a `rocket` reaction - no branch or CLI-agent session is created.
   - **Fresh proposal**: pulls and hard-resets the local clone to `BaseBranchName`,
     creates/switches to `feature/{issue-number}`, resolves the spec name via
     `ISpecNameResolver`, and runs the CLI coding agent with an
     `/opsx-propose {spec-name}\n{issue-body}` prompt, closing the session's input
     immediately after starting it since this is a one-shot prompt with no follow-up
     turns. On success it commits, pushes, and opens a draft PR, then adds a `rocket`
     reaction and a reply with the new PR number.
4. Any error, or exceeding `SpecRunnerOptions.TaskTimeout` for the whole per-comment cycle
   (stopping any in-flight CLI-agent session), adds a `confused` reaction and a
   human-readable reply instead of a raw exception, and processing moves on to the next
   eligible comment rather than aborting the scan pass.

Every outcome is also recorded in the SQLite state store (issue number, resolved spec
name, PR number if any, and the triggering comment's status), which is what the
already-has-a-PR check consults on future runs. This change only handles the `/propose`
trigger; `/update`, `/archive`, and PR review (inline code) comments are out of scope.

## Implement workflow

After the `propose-workflow` scan pass, `SpecRunner.Console` runs an `implement-workflow`
scan pass (`IImplementWorkflowRunner.RunOnceAsync`, implemented by `ImplementWorkflowRunner`)
over the configured repository's open pull requests, driving further work on a PR that
`/propose` already opened:

1. It lists open PRs and, for each, reads its general conversation comments via
   `ReadPrCommentsAsync`, treating a comment as an eligible trigger when its body, trimmed
   of leading/trailing whitespace, is exactly `/implement` or starts with `/implement`
   followed by whitespace (so `/implemented` or `/implement` used mid-sentence does not
   trigger). The text after the token (and its separating whitespace) becomes that
   comment's instructions.
2. A comment already carrying an `eyes`, `+1`, or `confused` reaction from the
   authenticated bot identity is skipped, so re-running the console app never reprocesses
   a comment that is already in-progress, done, or errored (including a previously-errored
   comment - reprocessing it forever on every poll would otherwise be the alternative).
3. Each remaining eligible comment is processed in turn (never concurrently, and never
   concurrently with `propose-workflow`, since all comments share the one local clone).
   Processing starts by adding an `eyes` reaction, then resolves the PR's associated spec
   via `IStateStore.FindByPrNumberAsync`:
   - **Untracked PR** (no record - the PR wasn't opened by `/propose`, or its record was
     lost): posts a reply explaining that no associated spec/change was found and adds a
     `confused` reaction. No git operation, CLI-agent session, or state-store write
     happens for this comment.
   - **Tracked PR**: fetches, switches to, and hard-resets the PR's existing head branch
     to `origin/{branch}` (never `BaseBranchName` - the branch already exists), then runs
     the CLI coding agent with an `/opsx-apply {spec-name} {instructions}` prompt using the
     tracked record's spec name. On success it commits with message
     `"implementing #{issue-number}"` (the issue number from the tracked record) and
     pushes the existing branch - no new branch or PR is created - then adds a `+1`
     reaction (the closest available GitHub reaction to a checkmark) and a reply
     confirming the push.
4. Any error, or exceeding `SpecRunnerOptions.TaskTimeout` for the whole per-comment cycle
   (stopping any in-flight CLI-agent session), adds a `confused` reaction and a
   human-readable reply instead of a raw exception, and processing moves on to the next
   eligible comment rather than aborting the scan pass.

A successful or errored outcome on a tracked PR is also recorded in the SQLite state store
under the tracked record's issue number. An untracked PR's outcome is not recorded in the
state store (there is no issue number to key it under) - the `confused` reaction on the
comment itself is what prevents it from being reprocessed on the next scan pass. This
change only handles general PR conversation comments; `/update`-style follow-up comments
that aren't a `/implement` trigger, marking a PR ready for review, and PR review (inline
code) comments are out of scope.

## Status

`SpecRunner.Console` starts the host and tests whether the configured `RepositoryUrl` +
`GitHubToken` can reach the target repository via the GitHub REST API
(`IRepositoryConnectionTester`). It logs and prints the resulting status (`NotConfigured`,
`InvalidRepositoryUrl`, `Connected`, `AuthenticationFailed`, `RepositoryNotFound`, or
`NetworkError`) and a message. If the status is anything other than `Connected`, it exits
`1` without running any scan pass. `GitHubToken` is never included in printed or logged
output.

If the status is `Connected`, the process enters a polling loop: it runs one
`propose-workflow` scan pass to completion, then one `implement-workflow` scan pass to
completion, waits `SpecRunnerOptions.PollingInterval`, and repeats, indefinitely - it no
longer exits after a single pass. The two scan passes always run sequentially, never
concurrently, since they share the same local clone. An unhandled exception from either
scan pass is logged and does not stop the process or prevent the other scan pass from
running that same cycle; the loop simply waits out `PollingInterval` and tries again.
Sending `SIGINT` (Ctrl+C) or `SIGTERM` requests a graceful shutdown: an in-progress scan
pass is allowed to finish (no scan pass is cancelled mid-flight), no further
`propose-workflow` or `implement-workflow` pass is started, and the process then exits
with code `0`. A shutdown signal received while waiting out `PollingInterval` ends the
wait immediately instead of waiting out the full interval. Because the process now runs
continuously, running it expects an external supervisor (service manager, container
restart policy) rather than a one-shot invocation.

## Logging

`SpecRunner.Console` logs through Serilog (`ILogger`/`ILogger<T>` with structured
message-template calls, e.g. `logger.LogInformation("... {RepositoryUrl}", url)`), wired
into the generic host via `UseSerilog`/`ReadFrom.Configuration`. By default, sinks and
levels come from the `Serilog` section of `appsettings.json` and include:

- Console - all log output also goes to stdout.
- Rolling file - written under `logs/` (relative to the working directory), rolling to a
  new file once the current one reaches 1 MB (`fileSizeLimitBytes: 1048576`,
  `rollOnFileSizeLimit: true`). Older log files are not deleted automatically.

## Build and test

```
dotnet build SpecRunner/SpecRunner.sln
dotnet test SpecRunner/SpecRunner.sln
```
