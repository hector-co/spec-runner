## 1. Configuration model

- [x] 1.1 Replace `RepositoryOwner`/`RepositoryName` with a single
      `RepositoryUrl` string property on `SpecRunnerOptions`
      (`SpecRunner.Core`).
- [x] 1.2 Update `SpecRunner.Console/appsettings.json`: remove
      `RepositoryOwner`/`RepositoryName`, add `RepositoryUrl` (empty
      placeholder, no secrets committed).

## 2. Repository connection abstraction

- [x] 2.1 Add `RepositoryConnectionStatus` enum to `SpecRunner.Core`
      (`NotConfigured`, `InvalidRepositoryUrl`, `Connected`,
      `AuthenticationFailed`, `RepositoryNotFound`, `NetworkError`).
- [x] 2.2 Add `RepositoryConnectionResult` record to `SpecRunner.Core`
      carrying a `RepositoryConnectionStatus` and a `Message` string.
- [x] 2.3 Add `IRepositoryConnectionTester` interface to `SpecRunner.Core`
      with `Task<RepositoryConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)`.
- [x] 2.4 Add an internal helper in `SpecRunner.Core` (or the
      `SpecRunner.GitHub` implementation) that parses a
      `https://github.com/{owner}/{repo}` URL (with or without a trailing
      `.git`) into owner/repo segments, returning failure for any other
      shape (SSH URLs, non-GitHub hosts, malformed URLs).

## 3. Repository connection implementation

- [x] 3.1 Add an HTTP-based `IRepositoryConnectionTester` implementation to
      `SpecRunner.GitHub` that: returns `NotConfigured` when
      `RepositoryUrl` or `GitHubToken` is empty; returns
      `InvalidRepositoryUrl` when `RepositoryUrl` doesn't parse per task
      2.4; otherwise calls `GET https://api.github.com/repos/{owner}/{repo}`
      with the PAT as a bearer credential.
- [x] 3.2 Map the HTTP response to a status: `200` → `Connected`; `401`/
      `403` → `AuthenticationFailed`; `404` → `RepositoryNotFound`; any
      transport-level exception (no HTTP response) → `NetworkError`.
- [x] 3.3 Ensure the implementation never includes `GitHubToken` in the
      `Message` of any `RepositoryConnectionResult` it returns.
- [x] 3.4 Register `IRepositoryConnectionTester` (and the `HttpClient` it
      needs, via `AddHttpClient`) in `SpecRunner.Console`'s DI container.

## 4. Serilog logging

- [x] 4.1 Add `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console`,
      `Serilog.Sinks.File`, and `Serilog.Settings.Configuration` package
      versions to `SpecRunner/Directory.Packages.props`, and reference them
      (without `Version` attributes) from `SpecRunner.Console.csproj`.
- [x] 4.2 Wire `UseSerilog` into `Host.CreateApplicationBuilder` in
      `Program.cs`, reading sink/level configuration from the `Serilog`
      section via `ReadFrom.Configuration`.
- [x] 4.3 Add a `Serilog` section to `appsettings.json` with a console sink
      and a rolling file sink configured for `fileSizeLimitBytes: 1048576`
      and `rollOnFileSizeLimit: true`.
- [x] 4.4 Replace any remaining ad hoc console output in `Program.cs` with
      `ILogger`/`ILogger<T>` calls using structured message templates.

## 5. Console entry point wiring

- [ ] 5.1 After building the host, resolve `IRepositoryConnectionTester`
      and call `TestConnectionAsync` once.
- [ ] 5.2 Log the resulting status and message, and print a short summary
      of the connection state to the console.
- [ ] 5.3 Return exit code `0` when the status is `Connected`, and `1` for
      every other status.

## 6. Tests

- [ ] 6.1 Add unit tests for the `RepositoryUrl` parsing helper: valid
      `https://github.com/{owner}/{repo}` URL, URL with trailing `.git`,
      SSH URL, non-GitHub host, empty string.
- [ ] 6.2 Add unit tests for the `IRepositoryConnectionTester`
      implementation covering each status (`NotConfigured`,
      `InvalidRepositoryUrl`, `Connected`, `AuthenticationFailed`,
      `RepositoryNotFound`, `NetworkError`), using a fake/mocked HTTP
      message handler rather than real network calls.
- [ ] 6.3 Add/update the DI smoke test to confirm
      `IRepositoryConnectionTester` resolves from the container.
- [ ] 6.4 Add a test asserting no `RepositoryConnectionResult.Message`
      value produced by the tests in 6.2 contains a literal token/secret
      value used in the fake responses.
- [ ] 6.5 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 7. Documentation

- [ ] 7.1 Update `SpecRunner/README.md`: replace `RepositoryOwner`/
      `RepositoryName` documentation with `RepositoryUrl`, and describe the
      startup connection test and its exit-code behavior.
- [ ] 7.2 Add a short note to `SpecRunner/README.md` about the default
      Serilog sinks (console + 1 MB rolling file) and where log files are
      written.
- [ ] 7.3 Update `openspec/config.yaml` project `context` to note that
      logging goes through Serilog using structured message-template
      conventions, with console + size-limited rolling-file sinks by
      default.

## 8. Verification

- [ ] 8.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [ ] 8.2 Run `SpecRunner.Console` locally with a valid `RepositoryUrl` and
      PAT and confirm it logs `Connected` and exits `0`.
- [ ] 8.3 Run `SpecRunner.Console` locally with a missing/invalid PAT or
      URL and confirm it logs the corresponding failure status and exits
      non-zero.
- [ ] 8.4 Confirm a rolling log file appears under the configured log
      directory after a run.
