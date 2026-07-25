## 1. Core abstractions and models

- [ ] 1.1 Add `ToolAvailabilityStatus` enum (`Available`, `NotFound`,
      `LaunchFailed`) to `SpecRunner.Core.Models`
- [ ] 1.2 Add `ToolAvailabilityResult` model (`ToolAvailabilityStatus`,
      `string Message`) to `SpecRunner.Core.Models`
- [ ] 1.3 Add `ICliToolAvailabilityChecker` interface (single async
      `CheckAsync(string executable, CancellationToken)` method) to
      `SpecRunner.Core.Abstractions`
- [ ] 1.4 Add `DependencyCheckResult` model (`string Name`, `bool
      IsSuccessful`, `string Message`) to `SpecRunner.Core.Models`
- [ ] 1.5 Add `IStartupDependencyChecker` interface (single async
      `CheckAllAsync(CancellationToken)` returning
      `IReadOnlyList<DependencyCheckResult>`) to
      `SpecRunner.Core.Abstractions`
- [ ] 1.6 Add `OpenSpecCliOptions` (`SectionName = "OpenSpecCli"`,
      `Executable` defaulting to `"openspec"`) to
      `SpecRunner.Core.Configuration`

## 2. Claude/OpenSpec CLI availability check (SpecRunner.Cli)

- [ ] 2.1 Implement `ProcessCliToolAvailabilityChecker : ICliToolAvailabilityChecker`
      in `SpecRunner.Cli`, using the existing internal
      `IChildProcessFactory`/`IChildProcess` to launch
      `<executable> --version`
- [ ] 2.2 Map a zero exit code to `Available`, a non-zero exit code to
      `LaunchFailed` (message includes the exit code), and a process-start
      failure (executable not found) to `NotFound` (message describes the
      failure)
- [ ] 2.3 Add unit tests for `ProcessCliToolAvailabilityChecker` covering
      the available, launch-failed, and not-found cases, using the same
      fake process infrastructure already used to test
      `ClaudeCliAgentSession`

## 3. Startup dependency checker (SpecRunner.Console)

- [ ] 3.1 Implement `StartupDependencyChecker : IStartupDependencyChecker`
      in `SpecRunner.Console`, composing `ICliToolAvailabilityChecker`
      (called with `CliAgentOptions.Executable` for "Claude CLI" and
      `OpenSpecCliOptions.Executable` for "OpenSpec CLI") and the existing
      `IRepositoryConnectionTester` (for "GitHub connection"), returning
      results in that order
- [ ] 3.2 Ensure a failure in one check does not prevent the remaining
      checks from running
- [ ] 3.3 Add unit tests for `StartupDependencyChecker` covering: all
      checks succeed, one check fails but all three still run, and result
      ordering

## 4. Wiring and Program.cs

- [ ] 4.1 Register `IOptions<OpenSpecCliOptions>` bound to the
      `OpenSpecCli` configuration section in `Program.cs`
- [ ] 4.2 Register `ICliToolAvailabilityChecker` and
      `IStartupDependencyChecker` in the DI container in `Program.cs`
- [ ] 4.3 Replace the existing direct `IRepositoryConnectionTester` call in
      `Program.cs` with a call to
      `IStartupDependencyChecker.CheckAllAsync()`
- [ ] 4.4 Log each dependency result individually (`LogInformation` on
      success, `LogError` on failure) and print a corresponding line to the
      console for each, without ever including `GitHubToken`
- [ ] 4.5 If any result is unsuccessful, log a final error summarizing the
      failed dependency/dependencies and return a non-zero exit code before
      constructing the shutdown/polling-loop machinery; otherwise proceed
      to start the polling loop as today

## 5. Configuration and docs

- [ ] 5.1 Add a default `OpenSpecCli` section (with `Executable`:
      `"openspec"`) to `appsettings.json`
- [ ] 5.2 Update `SpecRunner/README.md` (if it documents startup/config
      behavior) to mention the three startup dependency checks

## 6. Verification

- [ ] 6.1 Run the full test suite and confirm it passes
- [ ] 6.2 Manually run the console app with a valid environment and confirm
      all three dependency lines are printed and the app proceeds to the
      polling loop
- [ ] 6.3 Manually run the console app with an invalid/missing CLI
      executable configured and confirm the failure is reported and the
      app exits non-zero without starting the polling loop
