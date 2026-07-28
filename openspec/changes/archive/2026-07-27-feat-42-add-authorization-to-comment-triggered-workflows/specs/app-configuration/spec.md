## ADDED Requirements

### Requirement: Comment-author authorization allowlists are configurable
`SpecRunnerOptions` SHALL expose `AllowedAuthorAssociations` (a list of
strings, defaulting to `"OWNER"`, `"MEMBER"`, `"COLLABORATOR"`) and
`AllowedTriggerUsers` (a list of strings, defaulting to empty), both bound
from configuration via the standard `IOptions` pattern, used to decide which
comment authors are permitted to trigger comment-driven workflows.

#### Scenario: Association allowlist defaults when not configured
- **WHEN** no `AllowedAuthorAssociations` value is present in any
  configuration source
- **THEN** `SpecRunnerOptions.AllowedAuthorAssociations` SHALL resolve to
  `["OWNER", "MEMBER", "COLLABORATOR"]`

#### Scenario: Trigger user allowlist defaults to empty when not configured
- **WHEN** no `AllowedTriggerUsers` value is present in any configuration
  source
- **THEN** `SpecRunnerOptions.AllowedTriggerUsers` SHALL resolve to an empty
  list

#### Scenario: Both allowlists are overridable from configuration
- **WHEN** `appsettings.json` supplies `AllowedAuthorAssociations` set to
  `["OWNER"]` and `AllowedTriggerUsers` set to `["trusted-external"]`
- **THEN** `IOptions<SpecRunnerOptions>` resolved from the host SHALL expose
  those values unchanged
