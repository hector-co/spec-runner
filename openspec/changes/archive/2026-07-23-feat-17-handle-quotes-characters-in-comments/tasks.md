## 1. Escape substituted values in the template renderer

- [x] 1.1 In `SpecRunner/src/SpecRunner.Console/CommandTemplateRenderer.cs`,
      add a private helper that escapes a single value by first replacing
      `\` with `\\`, then replacing `"` with `\"`.
- [x] 1.2 Apply that helper to the matched value inside the
      `TokenRegex().Replace` callback in `RenderAsync`, so every
      substituted placeholder value is escaped before being written into
      the rendered template (values without `"`/`\` are unchanged).

## 2. Test coverage

- [x] 2.1 In `SpecRunner/tests/SpecRunner.Tests/CommandTemplateRendererTests.cs`,
      add a test rendering a template with a placeholder value containing
      a double quote (e.g. `instructions` set to
      `` also handle the "edge case" comment ``) and assert the rendered
      output contains the quote characters escaped (`\"`), not stripped.
- [x] 2.2 Add a test with a placeholder value containing a backslash
      immediately followed by a double quote and assert it renders as
      `\\\"` (doubled backslash then escaped quote), confirming
      backslash-escaping happens before quote-escaping.
- [x] 2.3 Add a test confirming a placeholder value with no `"` or `\`
      characters renders unchanged (regression guard against
      over-escaping).

## 3. Verify

- [x] 3.1 Run `dotnet test` for `SpecRunner.Tests` and confirm all tests,
      including the existing `CommandTemplateRendererTests` and the four
      workflow-runner test suites, still pass unchanged.
