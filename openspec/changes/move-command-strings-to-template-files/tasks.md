## 1. Command template renderer

- [ ] 1.1 Add `ICommandTemplateRenderer` to `SpecRunner.Core.Abstractions`,
      with an operation that takes a template name and a set of named
      replacement values and returns the rendered text.
- [ ] 1.2 Implement `CommandTemplateRenderer` in `SpecRunner.Console`,
      reading `CommandTemplates/{name}.txt` relative to the app's base
      directory, replacing every `{{token_name}}` placeholder with the
      matching supplied value.
- [ ] 1.3 Throw a clear exception (including the resolved file path) when
      the requested template file does not exist.
- [ ] 1.4 Throw a clear exception (identifying the unresolved token name)
      when a template contains a `{{token_name}}` placeholder with no
      matching supplied value.
- [ ] 1.5 Register `ICommandTemplateRenderer` / `CommandTemplateRenderer`
      as a singleton in `Program.cs`, alongside the other core services.

## 2. Command template files

- [ ] 2.1 Add `SpecRunner.Console/CommandTemplates/propose.txt`
      containing the current `/opsx-propose {{spec_name}}\n{{issue_body}}`
      text plus the standing unattended-run instruction block.
- [ ] 2.2 Add `SpecRunner.Console/CommandTemplates/apply.txt` containing
      the current `/opsx-apply {{spec_name}} {{instructions}}` text plus
      the standing unattended-run instruction block.
- [ ] 2.3 Add `SpecRunner.Console/CommandTemplates/update.txt` containing
      the current `Update the OpenSpec change "{{spec_name}}" to reflect
      the following new requirement/information:\n{{instructions}}` text
      plus the standing unattended-run instruction block.
- [ ] 2.4 Add `SpecRunner.Console/CommandTemplates/archive.txt`
      containing the current `` Run `openspec archive "{{spec_name}}"
      --yes`. Mark missing tasks as completed and continue.\n{{instructions}}
      `` text plus the standing unattended-run instruction block.
- [ ] 2.5 Add `Content`/`CopyToOutputDirectory` entries for the
      `CommandTemplates/*.txt` files in `SpecRunner.Console.csproj`,
      matching the existing `appsettings.json` entry's convention.

## 3. Wire workflow runners to the renderer

- [ ] 3.1 Update `ProposeWorkflowRunner` to take `ICommandTemplateRenderer`
      as a constructor dependency and render the `propose` prompt from
      the `propose` template instead of an interpolated string, keeping
      the existing escaped-double-quote wrapping in the runner.
- [ ] 3.2 Update `ImplementWorkflowRunner` to render the `apply` prompt
      from the `apply` template the same way.
- [ ] 3.3 Update `UpdateWorkflowRunner` to render the update prompt from
      the `update` template the same way.
- [ ] 3.4 Update `FinalizeWorkflowRunner` to render the archive prompt
      from the `archive` template the same way.

## 4. Tests

- [ ] 4.1 Add unit tests for `CommandTemplateRenderer` covering:
      successful placeholder substitution, unknown template name, and an
      unresolved placeholder.
- [ ] 4.2 Update `ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
      `UpdateWorkflowRunner`, and `FinalizeWorkflowRunner` tests to
      supply an `ICommandTemplateRenderer` (fake or real) and assert the
      exact prompt handed to `StartAsync`, including the trailing
      unattended-run instruction block.
- [ ] 4.3 Update `DependencyInjectionSmokeTests` (or equivalent) to cover
      resolving `ICommandTemplateRenderer` from the DI container.

## 5. Verification

- [ ] 5.1 Build the solution and run the full test suite.
- [ ] 5.2 Manually inspect each rendered prompt (e.g. via a quick console
      run or test output) to confirm it matches the corresponding spec
      scenario exactly, including the unattended-run block.
