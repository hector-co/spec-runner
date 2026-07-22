## ADDED Requirements

### Requirement: GitHub service marks a pull request ready for review
`IGitHubService`'s `MarkPrReadyForReviewAsync` member SHALL be
implemented for real (previously a `NotImplementedException` placeholder):
given a PR number, it SHALL convert that pull request from draft to
ready-for-review using the GitHub GraphQL `markPullRequestReadyForReview`
mutation, since the GitHub REST API has no equivalent endpoint. The
implementation SHALL first resolve the PR's GraphQL node id from its REST
number via the GitHub REST API, then submit that node id to the GraphQL
endpoint as the mutation's input.

#### Scenario: Marking a PR ready for review converts it out of draft
- **WHEN** `MarkPrReadyForReviewAsync` is called with PR number `12`,
  which is currently a draft pull request
- **THEN** the PR's GraphQL node id SHALL be resolved via the GitHub REST
  API, the `markPullRequestReadyForReview` GraphQL mutation SHALL be
  called with that node id, and PR `12` SHALL no longer be a draft

#### Scenario: A failing GraphQL call is reported, not left to crash the caller
- **WHEN** the `markPullRequestReadyForReview` GraphQL mutation fails
  (non-2xx response or a GraphQL error payload)
- **THEN** `MarkPrReadyForReviewAsync` SHALL report the failure through
  its return value or a specific exception type, without an
  unhandled/unstructured exception escaping the call
