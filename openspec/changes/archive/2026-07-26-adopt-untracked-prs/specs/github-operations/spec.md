## ADDED Requirements

### Requirement: GitHub service lists the issues a pull request would close
`IGitHubService` SHALL provide a `ListClosingIssueNumbersAsync` operation
that, given a PR number, returns the issue numbers GitHub's own
`closingIssuesReferences` GraphQL field reports for that pull request —
covering both issues linked via a closing keyword (e.g. `closes #45`) in the
PR body and issues linked purely through the PR's "Development" sidebar UI.
The implementation SHALL query the GitHub GraphQL endpoint for
`pullRequest(number: $number) { closingIssuesReferences { nodes { number } } }`
on the configured repository, mirroring the existing GraphQL request pattern
used by `MarkPrReadyForReviewAsync`.

#### Scenario: A PR with one closing keyword returns that issue
- **WHEN** `ListClosingIssueNumbersAsync` is called for PR number `12`,
  whose body contains `"closes #45"`
- **THEN** the result SHALL contain issue number `45`

#### Scenario: A PR with a UI-linked issue and no closing keyword still returns it
- **WHEN** `ListClosingIssueNumbersAsync` is called for PR number `12`,
  which has issue `45` linked via the PR sidebar but no closing keyword in
  its body
- **THEN** the result SHALL contain issue number `45`

#### Scenario: A PR with no linked issues returns an empty result
- **WHEN** `ListClosingIssueNumbersAsync` is called for PR number `12`,
  which has no closing-issue references
- **THEN** the result SHALL be empty, without an error

#### Scenario: A failing GraphQL call is reported, not left to crash the caller
- **WHEN** the underlying GraphQL query for `ListClosingIssueNumbersAsync`
  fails (non-2xx response or a GraphQL error payload)
- **THEN** the operation SHALL report the failure through its return value
  or a specific exception type, without an unhandled/unstructured exception
  escaping the call
