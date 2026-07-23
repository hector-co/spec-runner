## ADDED Requirements

### Requirement: GitHub service updates an existing pull request's title
`IGitHubService` SHALL provide an `UpdatePullRequestTitleAsync` operation
that, given a PR number and a new title, updates that pull request's title
via the GitHub REST API (`PATCH /repos/{owner}/{repo}/pulls/{prNumber}`
with `{ "title": title }`), replacing its existing title entirely, mirroring
the existing `UpdatePullRequestDescriptionAsync` operation.

#### Scenario: Updating a PR's title replaces it via the GitHub REST API
- **WHEN** `UpdatePullRequestTitleAsync` is called with PR number `12` and
  a new title
- **THEN** the GitHub REST API SHALL be called to set PR `12`'s title to
  that new title, replacing whatever title it had before

#### Scenario: A failing update call is reported, not left to crash the caller
- **WHEN** the underlying GitHub REST API call for
  `UpdatePullRequestTitleAsync` fails (non-2xx response or network error)
- **THEN** the operation SHALL report the failure through its return value
  or a specific exception type, without an unhandled/unstructured
  exception escaping the call
