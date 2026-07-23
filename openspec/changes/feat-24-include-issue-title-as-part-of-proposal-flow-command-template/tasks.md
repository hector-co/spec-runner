## 1. Template

- [ ] 1.1 Add an `{{issue_title}}` placeholder to
  `SpecRunner.Console/CommandTemplates/propose.txt`, on its own line
  above the existing `{{issue_body}}` line.

## 2. Workflow runner

- [ ] 2.1 In `ProposeWorkflowRunner`, add `issue_title` set to
  `comment.IssueTitle` to the replacement dictionary passed to
  `ICommandTemplateRenderer.RenderAsync` for the `propose` template.

## 3. Tests

- [ ] 3.1 Update the `propose` template rendering test in
  `CommandTemplateRendererTests.cs` to supply `issue_title` and assert
  the rendered text includes the issue title line above the issue body
  line.
- [ ] 3.2 Update any `ProposeWorkflowRunner` test that asserts the exact
  rendered/initial prompt content (or the exact replacement values
  passed to `ICommandTemplateRenderer`) to include `issue_title`.

## 4. Verification

- [ ] 4.1 Run the `SpecRunner.Tests` test suite and confirm it passes.
