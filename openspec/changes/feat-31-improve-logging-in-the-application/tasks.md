## 1. Shared progress-indicator helper

- [ ] 1.1 Add `ProgressIndicator` static helper to `SpecRunner.Console`
      (e.g. `SpecRunner.Console/ProgressIndicator.cs`) with a `RunAsync(ILogger
      logger, string message, CancellationToken cancellationToken)` method
      that logs `message` at Information level every 5 seconds until
      cancelled, swallowing the resulting `OperationCanceledException`.
- [ ] 1.2 Add a unit test (`SpecRunner.Tests`) covering
      `ProgressIndicator.RunAsync`: verify it logs at least once when left
      running past 5 seconds and logs nothing once its token is cancelled.

## 2. ProposeWorkflowRunner logging

- [ ] 2.1 Log an Information-level "starting /propose flow" message at the
      top of `ProcessCommentAsync`, including `comment.IssueNumber`.
- [ ] 2.2 Wrap the `session.ReadEventsAsync` drain loop with
      `ProgressIndicator.RunAsync` (started just before the loop, linked to
      `timeoutCts.Token`, cancelled and awaited in `finally` alongside the
      existing `session` disposal) logging an Information-level "still in
      progress" message that includes `comment.IssueNumber`.
- [ ] 2.3 Add Debug-level start/finish log pairs around each existing step
      call in `ProcessCommentAsync`: `ResetHardAsync`, `SwitchBranchAsync`
      (base branch), `PullAsync`, branch-existence loop /
      `CreateBranchAsync` / `SwitchBranchAsync` (new branch), prompt
      `RenderAsync`, `session.StartAsync`/`CloseInputAsync`,
      `_specFolderResolver.ResolveAsync`, `CommitAsync`, `PushAsync`,
      `_tasksFileReader.ReadCurrentAsync`, `CreateDraftPullRequestAsync`.
- [ ] 2.4 Confirm none of the new Debug-level messages duplicate the
      Information-level start/progress messages added in 2.1/2.2.

## 3. ImplementWorkflowRunner logging

- [ ] 3.1 Log an Information-level "starting /implement flow" message at
      the top of `ProcessCommentAsync`, including `comment.PrNumber`.
- [ ] 3.2 Wrap the `session.ReadEventsAsync` drain loop with
      `ProgressIndicator.RunAsync` following the same pattern as 2.2,
      including `comment.PrNumber` in the message.
- [ ] 3.3 Add Debug-level start/finish log pairs around each existing step
      call: `ResetHardAsync`, `FetchAsync`, `SwitchBranchAsync`,
      `ResetHardAsync` (origin), prompt `RenderAsync`,
      `session.StartAsync`/`CloseInputAsync`, `CommitAsync`, `PushAsync`,
      `_tasksFileReader.ReadCurrentAsync`,
      `UpdatePullRequestDescriptionAsync`, `UpdatePullRequestTitleAsync`.
- [ ] 3.4 Confirm none of the new Debug-level messages duplicate the
      Information-level start/progress messages added in 3.1/3.2.

## 4. UpdateWorkflowRunner logging

- [ ] 4.1 Log an Information-level "starting /update flow" message at the
      top of `ProcessCommentAsync`, including `comment.PrNumber`.
- [ ] 4.2 Wrap the `session.ReadEventsAsync` drain loop with
      `ProgressIndicator.RunAsync` following the same pattern as 2.2,
      including `comment.PrNumber` in the message.
- [ ] 4.3 Add Debug-level start/finish log pairs around each existing step
      call: `ResetHardAsync`, `FetchAsync`, `SwitchBranchAsync`,
      `ResetHardAsync` (origin), prompt `RenderAsync`,
      `session.StartAsync`/`CloseInputAsync`, `CommitAsync`, `PushAsync`,
      `_tasksFileReader.ReadCurrentAsync`,
      `UpdatePullRequestDescriptionAsync`.
- [ ] 4.4 Confirm none of the new Debug-level messages duplicate the
      Information-level start/progress messages added in 4.1/4.2.

## 5. FinalizeWorkflowRunner logging

- [ ] 5.1 Log an Information-level "starting /finalize flow" message at the
      top of `ProcessCommentAsync`, including `comment.PrNumber`.
- [ ] 5.2 Wrap the `session.ReadEventsAsync` drain loop with
      `ProgressIndicator.RunAsync` following the same pattern as 2.2,
      including `comment.PrNumber` in the message.
- [ ] 5.3 Add Debug-level start/finish log pairs around each existing step
      call: `ResetHardAsync`, `FetchAsync`, `SwitchBranchAsync`,
      `ResetHardAsync` (origin), prompt `RenderAsync`,
      `session.StartAsync`/`CloseInputAsync`, `CommitAsync`, `PushAsync`,
      `_tasksFileReader.ReadArchivedAsync`,
      `UpdatePullRequestDescriptionAsync`, `UpdatePullRequestTitleAsync`,
      `MarkPrReadyForReviewAsync`.
- [ ] 5.4 Confirm none of the new Debug-level messages duplicate the
      Information-level start/progress messages added in 5.1/5.2.

## 6. Verification

- [ ] 6.1 Run the full `SpecRunner.Tests` suite and confirm it passes.
- [ ] 6.2 Manually run `SpecRunner.Console` with `Serilog:MinimumLevel` set
      to `Debug` against a repo with at least one eligible comment, and
      confirm: one Information-level start line per step flow with the
      correct issue/PR number, periodic Information-level "still in
      progress" lines roughly every 5 seconds during the CLI agent session,
      and Debug-level start/finish lines for the other steps with no
      overlap against the Information-level lines.
- [ ] 6.3 Run `openspec validate feat-31-improve-logging-in-the-application`
      (or equivalent) to confirm the `structured-logging` delta spec under
      this change applies cleanly against
      `openspec/specs/structured-logging/spec.md` before archiving.
