## ADDED Requirements

### Requirement: GitHub service updates an existing pull request's description
`IGitHubService` SHALL provide an `UpdatePullRequestDescriptionAsync`
operation that, given a PR number and a new body, updates that pull
request's description via the GitHub REST API (`PATCH
/repos/{owner}/{repo}/pulls/{prNumber}` with `{ "body": body }`), replacing
its existing description entirely.

#### Scenario: Updating a PR's description replaces its body via the GitHub REST API
- **WHEN** `UpdatePullRequestDescriptionAsync` is called with PR number
  `12` and a new body
- **THEN** the GitHub REST API SHALL be called to set PR `12`'s description
  to that new body, replacing whatever description it had before

#### Scenario: A failing update call is reported, not left to crash the caller
- **WHEN** the underlying GitHub REST API call for
  `UpdatePullRequestDescriptionAsync` fails (non-2xx response or network
  error)
- **THEN** the operation SHALL report the failure through its return value
  or a specific exception type, without an unhandled/unstructured
  exception escaping the call
