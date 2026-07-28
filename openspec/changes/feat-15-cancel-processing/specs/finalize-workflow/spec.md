## ADDED Requirements

### Requirement: The in-flight comment is registered with the active-run registry
The workflow SHALL, immediately after creating its per-comment cancellation
token source, register an `ActiveRun` for the triggering comment's PR
number via `IActiveRunRegistry`, exposing that cancellation token source's
externally-triggered component and a completion task for the current
comment-processing call. Once a CLI-agent session is created for the
comment, the workflow SHALL update the registered `ActiveRun` to expose that
session. The workflow SHALL deregister the run for that PR number once
processing finishes, after disposing the session.

#### Scenario: A run is registered before the CLI agent starts
- **WHEN** the workflow begins processing an eligible `/finalize` comment
  for PR `12`
- **THEN** `IActiveRunRegistry` SHALL expose an `ActiveRun` for PR `12`
  before the CLI agent session is created, and that `ActiveRun` SHALL be
  updated to expose the session once one is created

#### Scenario: A run is deregistered once processing finishes
- **WHEN** the workflow finishes processing an eligible `/finalize` comment
  for PR `12`, whether by success, error, or cancellation
- **THEN** `IActiveRunRegistry` SHALL no longer expose an `ActiveRun` for
  PR `12`

## MODIFIED Requirements

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment,
post a reply comment with a short, human-readable summary of the failure
(not a raw stack trace or exception dump), and, for a comment on a tracked
PR, upsert the state store recording the comment's processing status as
`error` under the tracked record's PR number, whenever any step of
processing an eligible comment throws or the whole per-comment cycle
exceeds `SpecRunnerOptions.TaskTimeout` (in the timeout case, any
in-flight CLI agent session SHALL also be stopped via `StopAsync`).
Processing SHALL then continue to the next eligible comment in the scan
pass rather than aborting the whole run.

When the per-comment cancellation instead results from an externally
requested cancellation (the run's registered cancellation token source
having been cancelled by `cancel-workflow`, as opposed to
`SpecRunnerOptions.TaskTimeout` expiring), the workflow SHALL stop any
in-flight CLI agent session via `StopAsync` but SHALL NOT add a reaction,
post a reply, or upsert the state store for that outcome, since
`cancel-workflow` owns all reporting for a cancelled run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible
  comment on a tracked PR with PR number `12`
- **THEN** that comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, its state-store status
  SHALL be `error` under PR `12`, and the scan pass SHALL continue
  processing any remaining eligible comments

#### Scenario: Exceeding the task timeout stops the agent and reports a timeout
- **WHEN** processing an eligible comment exceeds
  `SpecRunnerOptions.TaskTimeout` while a CLI agent session is running
- **THEN** the session SHALL be stopped via `StopAsync`, the comment SHALL
  receive a `confused` reaction, and the reply comment SHALL indicate that
  processing timed out

#### Scenario: An externally requested cancellation stops the session without the workflow reporting it
- **WHEN** `cancel-workflow` cancels the registered cancellation token
  source for PR `12` while its CLI agent session is running, before
  `SpecRunnerOptions.TaskTimeout` has elapsed
- **THEN** the session SHALL be stopped via `StopAsync`, but no reaction
  SHALL be added, no reply SHALL be posted, and no state-store upsert SHALL
  be made by `FinalizeWorkflowRunner` for that outcome
