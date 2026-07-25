## Why

The four workflow runners (`ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
`UpdateWorkflowRunner`, `FinalizeWorkflowRunner`) currently only log warnings
on timeout and errors on failure. There is no visibility into when a step
flow starts, which issue/PR it is working on, whether a long-running CLI
agent session is still alive, or which individual step is currently
executing. This makes it hard to tell, from the logs alone, whether the
application is healthy, stuck, or idle while a task run is in progress.

## What Changes

- Log an Information-level message when a step flow begins processing an
  eligible comment, including the issue number (propose workflow) or PR
  number (implement/update/finalize workflows).
- While a step flow is waiting on its CLI agent session (the only
  unbounded-duration step, bounded only by `TaskTimeout`), log an
  Information-level "still in progress" indicator every 5 seconds until the
  session reaches a terminal state.
- Add Debug-level messages marking the start and completion of each
  individual step within a step flow (e.g. git sync, prompt rendering, CLI
  agent session, commit/push, GitHub updates), distinct from and not
  duplicating the Information-level flow-start/progress messages.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `structured-logging`: adds requirements for Information-level step-flow
  start and in-progress indicators, and Debug-level per-step start/finish
  detail, on top of the existing Serilog/sink/secret-redaction requirements.

## Impact

- Affected code: `SpecRunner.Console/ProposeWorkflowRunner.cs`,
  `ImplementWorkflowRunner.cs`, `UpdateWorkflowRunner.cs`,
  `FinalizeWorkflowRunner.cs` (each `ProcessCommentAsync` method and its
  step sequence).
- Introduces a small shared helper for the periodic "still in progress"
  indicator so the 5-second loop isn't duplicated across the four runners.
- No changes to public interfaces, configuration schema, or GitHub-facing
  behavior; purely additive logging.

## Assumptions

- "Each step flow" refers to one invocation of a workflow runner's
  `ProcessCommentAsync` (one pass through git sync → CLI agent session →
  git commit/push → GitHub updates for a single eligible comment), since
  that is the unit that already carries an issue or PR number end-to-end.
- The "every 5 seconds, still in progress" indicator applies specifically
  while awaiting the CLI agent session's event stream
  (`session.ReadEventsAsync`), since that is the only step without a fixed,
  short duration and the one actually gated by `TaskTimeout`. Other steps
  (git commands, GitHub API calls) are not individually wrapped with this
  indicator.
- "Notify steps initialization... indicating the issue or PR number" is
  interpreted as one Information-level message at the start of
  `ProcessCommentAsync`, not one per individual step, to avoid Information-level
  noise; per-step detail is what the new Debug-level messages are for.
- Debug-level start/finish messages are added around each existing step call
  (git operations, prompt rendering, CLI agent session lifecycle,
  commit/push, GitHub description/title/ready-for-review updates) without
  changing their control flow, error handling, or the existing
  Warning/Error logging on timeout/failure.
