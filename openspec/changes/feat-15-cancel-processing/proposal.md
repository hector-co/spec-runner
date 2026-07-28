## Why

There is currently no way to stop an in-flight `/propose`, `/implement`, `/update`, or
`/finalize` run once it has started. A long or stuck CLI-agent session can only be
killed by restarting the whole SpecRunner process, and even then the local clone is
left with whatever half-finished changes the agent made. Issue #15 asks for a
comment-triggered `/cancel` mechanism that stops the currently running work for that
issue/PR and discards any uncommitted changes, so a maintainer can recover from a
runaway or unwanted run without operator intervention.

## What Changes

- Add a new `/cancel` comment trigger, usable on an open issue (targets an in-flight
  `/propose` run) or an open pull request (targets an in-flight `/implement`,
  `/update`, or `/finalize` run), following the same trigger-token, authorization, and
  already-handled-reaction conventions as the existing workflows.
- Add an in-memory active-run registry that the four existing workflow runners
  register into while processing an eligible comment (the per-comment cancellation
  token source and the running `ICliAgentSession`, keyed by issue number or PR
  number), and that the new cancel workflow looks up to find what to stop.
- Add a `CancelWorkflowRunner` that, for an eligible and authorized `/cancel` comment:
  reacts `eyes` to indicate it started, requests cancellation of the matching active
  run (stopping its CLI-agent session and cancelling its processing), waits for that
  run to actually unwind, discards all uncommitted/untracked changes via
  `IGitService.ResetHardAsync("HEAD")`, and reports the outcome with a completion
  reaction and a reply comment.
- Run the `/cancel` scan concurrently with the existing sequential propose →
  implement → update → finalize scan pass (rather than as a fifth sequential step),
  since a comment cannot be discovered by the existing polling loop until the
  in-flight `RunOnceAsync` call it would need to interrupt has already returned.
- Modify `ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
  and `FinalizeWorkflowRunner` to register/deregister with the active-run registry
  around their existing per-comment processing, and to distinguish an externally
  requested cancellation from a `TaskTimeout` expiry so a cancelled run exits quietly
  instead of also posting its own timeout/error report (the cancel workflow owns all
  reporting for a cancelled run).
- Add a `Canceled` value to `CommentStatus` so a cancelled comment's state-store
  status is recorded distinctly from `Error`.

## Capabilities

### New Capabilities
- `cancel-workflow`: the `/cancel` comment-triggered workflow — trigger detection,
  authorization, locating the matching active run via the active-run registry,
  requesting cooperative cancellation of its CLI-agent session and processing,
  discarding uncommitted changes via a hard git reset, and reporting the outcome via
  reactions, a reply comment, and the state store.

### Modified Capabilities
- `propose-workflow`: registers the in-flight per-comment cancellation token source
  and CLI-agent session with the active-run registry (keyed by issue number) while
  processing an eligible comment, and skips its own timeout/error reporting when the
  cancellation was externally requested rather than a `TaskTimeout` expiry.
- `implement-workflow`: same registration/reporting change, keyed by PR number.
- `update-workflow`: same registration/reporting change, keyed by PR number.
- `finalize-workflow`: same registration/reporting change, keyed by PR number.
- `state-store-schema`: `CommentStatus` gains a `Canceled` value, used instead of
  `Error` to record a comment whose processing was stopped by `/cancel`.

## Impact

- `SpecRunner.Core`: new `IActiveRunRegistry` abstraction and `ActiveRun` model, new
  `ICancelWorkflowRunner` interface, `CommentStatus.Canceled` enum value.
- `SpecRunner.Console`: new `CancelWorkflowRunner` implementation, new in-memory
  `IActiveRunRegistry` implementation, changes to `ProposeWorkflowRunner`,
  `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, `FinalizeWorkflowRunner` to
  register/deregister active runs and to distinguish cancellation from timeout, and a
  change to `Program.cs`/`PollingLoop` to run the cancel scan concurrently with the
  main scan-and-sleep loop.
- `SpecRunner.Tests`: new tests for `CancelWorkflowRunner`, the active-run registry,
  and the modified cancellation-vs-timeout handling in the four existing runners.
- No changes to `SpecRunner.Git`, `SpecRunner.GitHub`, or `SpecRunner.State` beyond
  the additive `CommentStatus.Canceled` value (no schema migration needed since
  status is persisted as its enum name string).

## Assumptions

- `/cancel` targets whatever issue or PR it is posted on (no issue/PR number argument
  needed), mirroring how `/update` always targets its own PR.
- `/cancel` is scanned on open issues' issue comments and open PRs' conversation
  comments only, not file-anchored PR review comments, since cancellation is a
  control action rather than a code-location-specific instruction.
- `/cancel` requires the same authorization check (`CommentAuthorization.IsAuthorized`)
  as every other trigger, so an unauthorized commenter cannot stop someone else's run.
- Because the local clone is single and shared, `/cancel` only performs the hard
  reset once it has confirmed (by awaiting the deregistration signal, with a bounded
  grace period) that the run it stopped has actually finished touching git; if
  nothing is currently registered as active for the target issue/PR, `/cancel` still
  performs the reset as a manual recovery action, but only when the registry has no
  other active run at all (to avoid corrupting a different in-flight run's clone
  state).
- GitHub's reaction set has no literal "canceled" icon. Reusing the existing
  `eyes`/completion-reaction convention on the `/cancel` comment itself is sufficient
  to satisfy the "start/complete" icon requirement; no new reaction type is invented.
- The cancel scan reuses `SpecRunnerOptions.PollingInterval` for its own interval
  rather than introducing a separate configuration value, since polling is already
  scoped short (10s default) and a second knob would add configuration surface for a
  cross-cutting concern that doesn't need independent tuning.
