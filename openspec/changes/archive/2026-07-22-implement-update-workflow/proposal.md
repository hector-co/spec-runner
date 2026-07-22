## Why

`SpecRunner` can turn an issue into a draft PR via `/propose` and drive a
full implementation pass on an existing PR via `/implement`, but a
reviewer who just wants to steer an in-flight change with a new
requirement or piece of information has no comment-driven way to do
that — they have to edit the OpenSpec change by hand and re-run the CLI
agent themselves. A `/update` comment on the PR should trigger the same
watch-react-run-report cycle `/implement` already provides, but pass the
comment straight through as a natural-language instruction to amend the
OpenSpec change rather than running a fixed `/opsx-apply` task list.

## What Changes

- Add a new `update-workflow` orchestration: each poll pass scans open
  PRs for comments whose trimmed body is exactly `/update` or starts
  with `/update` followed by whitespace, skipping any comment that
  already carries an `eyes` or `+1` (the closest available reaction to a
  checkmark — GitHub has no literal checkmark reaction) reaction from the
  bot, mirroring `/implement`'s eligibility and skip rules.
- Mark a newly eligible comment with an `eyes` reaction before any other
  action, mirroring `/propose` and `/implement`'s in-progress signal.
- Resolve the spec/change name for the comment's PR via
  `IStateStore.FindByPrNumberAsync`; if the PR has no tracked record,
  report the comment as an error instead of guessing a spec name, same as
  `/implement`.
- Refresh the local clone to the PR's existing head branch (fetch/switch/
  hard-reset, reusing the primitives `/implement` added) and run the CLI
  coding agent with an initial prompt built as a plain natural-language
  instruction — not an `/opsx-*` slash command — of the form: `Update the
  OpenSpec change "<spec-name>" to reflect the following new
  requirement/information:\n<comment body>` where `<comment body>` is the
  triggering comment with the leading `/update` token and its separating
  whitespace removed, sent to the CLI agent as a single value wrapped in
  escaped double quotes (`\"...\"`), matching the existing prompt-quoting
  convention `/propose` and `/implement` already use.
- On a completed run, commit and push the changes to the PR's existing
  branch, then add a `+1` reaction to the triggering comment (standing in
  for a checkmark) and post a reply confirming the push.
- On an error or a `SpecRunnerOptions.TaskTimeout` timeout, add a
  `confused` reaction and post a human-readable failure summary,
  mirroring `/implement`'s error handling, then continue to the next
  eligible comment rather than aborting the scan pass.
- Extend the console entry point's poll loop to run an `update-workflow`
  scan pass every cycle, sequentially after `implement-workflow` (all
  three workflows share the one local clone, so they cannot run
  concurrently).
- No local state-store schema changes are required: `/update` comments
  reuse the existing `TrackedComments` shape (`CommentKind.PrIssueComment`,
  status `pending`/`working`/`done`/`error`) the same way `/implement`
  already does. This project is pre-release, so if implementation
  surfaces a genuine gap, the schema is adjusted directly without
  preserving backward compatibility with existing rows.

## Capabilities

### New Capabilities
- `update-workflow`: the `/update` PR-comment-triggered orchestration —
  scanning open PRs for eligible comments, resolving the associated spec
  via the state store, refreshing the PR's branch, running the CLI agent
  with a natural-language change-update instruction, committing/pushing,
  and reporting success/error outcomes on GitHub and in the state store.

### Modified Capabilities
- `solution-layout`: the console entry point's poll loop requirement
  changes from "run one `propose-workflow` scan pass, then one
  `implement-workflow` scan pass, per cycle" to "run `propose-workflow`,
  then `implement-workflow`, then `update-workflow`, per cycle."

## Impact

- `SpecRunner.Core`: new `IUpdateWorkflowRunner` abstraction and
  supporting model(s) for an eligible `/update` comment.
- `SpecRunner.Console`: new `UpdateWorkflowRunner`; `Program.cs` and the
  poll loop updated to run all three workflows sequentially each poll
  cycle; DI registration for the new runner.
- `SpecRunner.State`: no schema changes anticipated.
- Out of scope: `/archive` comment handling; PR review-comment (inline
  code comment) handling; changing `/propose` or `/implement`'s own
  behavior.
