## Context

`SpecRunnerOptions` (in `SpecRunner.Core`) currently exposes
`RepositoryOwner`/`RepositoryName` as separate strings, and
`SpecRunner.Console`'s `Program.cs` only resolves services from DI to prove
the container is wired — it performs no git or GitHub operation and has no
logging beyond default console output. `IGitService`/`IGitHubService`
remain unimplemented placeholders (`NotImplementedException`) by design
from the prior change and stay that way here; this change does not
implement branch/commit/push/pull or PR operations.

This change adds the first real network operation the app performs: proving
that the configured repository URL and PAT actually grant access, on every
run. It also adds Serilog so that startup, the connection test outcome, and
future operations produce durable, structured logs instead of ad hoc
console output.

## Goals / Non-Goals

**Goals:**
- Configure a repository by URL (`RepositoryUrl`) instead of a split
  owner/name pair, alongside the existing PAT (`GitHubToken`).
- On every `SpecRunner.Console` run, test whether the configured URL + PAT
  can reach the target repository, and surface a typed result (not just a
  boolean) describing *why* it failed when it does.
- Reflect the connection outcome in the process exit code, so the check is
  usable from scripts/CI without parsing log output.
- Introduce Serilog as the one logging façade used everywhere, with a
  console sink and a size-limited rolling file sink enabled by default and
  configured via `appsettings.json`, not hardcoded.
- Never write the PAT itself to a log sink.

**Non-Goals:**
- Implementing `IGitService` or `IGitHubService` (branch/commit/push/pull,
  PR create/comment/ready-for-review) — still out of scope, per the prior
  change.
- Supporting non-GitHub git hosts, or SSH-based repository URLs — PAT-based
  auth is GitHub-specific and HTTPS-only for this change.
- Retry/backoff policies for the connection check — a single attempt per
  run is sufficient to report state.
- Log shipping/aggregation to an external system — console + local rolling
  file only.

## Decisions

- **`RepositoryUrl` replaces `RepositoryOwner`/`RepositoryName`.**
  A single HTTPS GitHub URL (e.g. `https://github.com/owner/repo` or
  `https://github.com/owner/repo.git`) is simpler to configure and is what
  a user actually copies from GitHub. Owner/repo are derived from it at
  connection-test time rather than stored separately, removing a place
  where the two could drift out of sync. Alternative considered: keep
  owner/name and add `RepositoryUrl` as a third, derived-or-independent
  field — rejected as redundant configuration surface for no behavioral
  benefit yet (nothing else consumes owner/name independently of the URL
  today).

- **Connection test calls the GitHub REST API directly, not `git`/LibGit2Sharp.**
  `GET https://api.github.com/repos/{owner}/{repo}` with the PAT as a
  bearer token both authenticates and confirms repository access in one
  round trip, and maps cleanly onto typed outcomes from the HTTP status
  code (200 → connected, 401/403 → auth failed, 404 → not found/not
  accessible). Alternative considered: shell out to `git ls-remote` or use
  LibGit2Sharp against the URL — rejected for this change because it adds a
  native/process dependency and a second auth-encoding scheme (git
  credential helper vs. bearer token) for a check that the REST API answers
  more simply; `IGitService`'s real implementation can still use
  LibGit2Sharp/`git` later without conflicting with this check.

- **New `IRepositoryConnectionTester` abstraction, not an addition to
  `IGitHubService`.** `IGitHubService` (and `IGitService`) are placeholder
  interfaces whose existing spec requires every member to throw
  `NotImplementedException` until a future change implements them. Adding a
  real, working member to that interface now would contradict that
  requirement. A separate interface — defined in `SpecRunner.Core`,
  implemented for real in `SpecRunner.GitHub` — keeps the placeholder
  contract intact and gives the connection test its own small, testable
  surface (`Task<RepositoryConnectionResult> TestConnectionAsync(...)`).

- **Typed result over boolean.** `RepositoryConnectionResult` carries a
  `RepositoryConnectionStatus` enum (`NotConfigured`,
  `InvalidRepositoryUrl`, `Connected`, `AuthenticationFailed`,
  `RepositoryNotFound`, `NetworkError`) plus a human-readable `Message`.
  Config-level problems (missing URL/token, malformed URL) are detected
  before any HTTP call and reported as `NotConfigured`/
  `InvalidRepositoryUrl` rather than surfacing as a generic network
  failure, since they need a different fix (edit config vs. check network).

- **Exit code reflects connection state.** `Program.cs` exits `0` only when
  the status is `Connected`; every other status exits `1`. This keeps the
  console app scriptable (e.g. a CI step can fail fast on bad
  configuration) without requiring log parsing. Non-goal: distinct exit
  codes per failure category — one non-zero code is enough to signal "check
  the logs," and the log line already carries the specific status.

- **Serilog wired via `UseSerilog`, configured from `appsettings.json`'s
  `Serilog` section using `Serilog.Settings.Configuration`.** This matches
  standard Serilog convention (sinks/levels driven by config, not code) and
  keeps `Directory.Build.props`/`Directory.Packages.props` centralization
  consistent with how the rest of the solution is configured. Default
  sinks: `Console` and `File` with `rollingInterval: Infinite`,
  `fileSizeLimitBytes: 1048576` (1 MB) and `rollOnFileSizeLimit: true`, so a
  new file starts once the current one hits 1 MB, per the requested
  default. Packages: `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console`,
  `Serilog.Sinks.File`, `Serilog.Settings.Configuration`.

- **PAT never logged.** The connection tester logs the derived
  owner/repo and the resulting status/message, never `SpecRunnerOptions`
  wholesale and never the `GitHubToken` value or the literal `Authorization`
  header, consistent with the existing `app-configuration` requirement that
  the token never appears in log output.

- **`openspec/config.yaml` context gains a logging-convention note.** So
  that future changes generating SpecRunner code know to log through
  Serilog's structured/message-template style rather than
  `Console.WriteLine` or plain `ILogger` string interpolation.

## Risks / Trade-offs

- [GitHub REST API rate limits: unauthenticated or low-scope PATs could hit
  rate limits on repeated runs] → A single authenticated request per run is
  well under GitHub's per-hour limit for authenticated requests; not a
  practical concern at this app's expected run frequency.
- [`RepositoryUrl` parsing only supports `https://github.com/...` — other
  hosts or SSH URLs report `InvalidRepositoryUrl`] → Acceptable per
  Non-Goals; documented in the spec and README so it's an explicit
  limitation, not a silent failure.
- [Breaking config change: existing `RepositoryOwner`/`RepositoryName`
  values stop binding] → `appsettings.json` and README are updated in the
  same change; this app has no external users/deployments yet (still
  pre-workflow-implementation), so there is no migration path to preserve.
- [1 MB rolling file limit could produce many small files under heavy
  logging] → No retention limit is set by default (existing files are kept,
  not deleted), matching "rolling file, max size 1 MB" as stated without
  guessing an unrequested retention/cleanup policy; can be revisited if log
  volume becomes a problem.
