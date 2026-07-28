# github-operations

## Purpose

TBD - defines the real `SpecRunner.GitHub` implementation of the
`IGitHubService` members the propose, implement, update, and finalize
workflows depend on: identity resolution, listing open issues with
comments, reactions, issue comments, draft PR creation, listing open pull
requests, reading/writing PR conversation comments, listing and replying
to PR file-anchored review comments and their reactions, updating a pull
request's description, and marking a PR ready for review, plus failure
reporting.

## Requirements

### Requirement: GitHub service resolves the authenticated bot identity
`SpecRunner.GitHub`'s `IGitHubService` implementation SHALL provide a way
to resolve the GitHub login associated with the configured
`SpecRunnerOptions.GitHubToken`, calling the GitHub REST API at most once
per `SpecRunner.Console` invocation and reusing the resolved login for the
remainder of that run.

#### Scenario: Bot identity is resolved once per run
- **WHEN** the authenticated login is requested more than once during a
  single `SpecRunner.Console` invocation
- **THEN** only one GitHub REST API call SHALL be made to resolve it, and
  every request SHALL receive the same login value

### Requirement: GitHub service lists open issues with their comments
`IGitHubService` SHALL provide an operation that lists open issues on the
configured repository together with each issue's comments (comment id,
author, author association, body, and creation time), so callers can scan
comment bodies and decide whether their author is authorized without a
separate call per issue.

#### Scenario: Listing returns comments alongside each open issue
- **WHEN** the operation is called against a repository with at least one
  open issue that has comments
- **THEN** the result SHALL include that issue's number, title, body, and
  its comments with each comment's id, author, author association, and body

#### Scenario: A comment with no reported author association defaults to NONE
- **WHEN** the GitHub REST API response for an issue's comments omits the
  `author_association` property on a comment
- **THEN** that comment's author association SHALL resolve to `"NONE"`

### Requirement: GitHub service reads and adds reactions on an issue comment
`IGitHubService` SHALL provide an operation that lists the reactions
present on a given issue comment (including each reaction's author login
and reaction type) and an operation that adds a reaction of a given type
(one of the reaction types supported by the GitHub REST API, e.g. `eyes`,
`rocket`, `confused`) to a given issue comment.

#### Scenario: Listing reactions includes the author
- **WHEN** reactions are listed for a comment that has an `eyes` reaction
  from the bot's own login
- **THEN** the result SHALL include that reaction's type (`eyes`) and its
  author login

#### Scenario: Adding a reaction posts it via the GitHub REST API
- **WHEN** a `rocket` reaction is added to an issue comment
- **THEN** the GitHub REST API SHALL be called to add a `rocket` reaction
  to that comment, attributed to the authenticated bot identity

### Requirement: GitHub service creates an issue comment
`IGitHubService` SHALL provide an operation that creates a new comment on
a given issue with a supplied body.

#### Scenario: Creating a comment posts a reply on the issue
- **WHEN** the operation is called with issue number `45` and body
  `"Created Draft PR #12 for this issue."`
- **THEN** a new comment with that body SHALL be created on issue `45`

### Requirement: GitHub service creates a draft pull request
`IGitHubService`'s `CreateDraftPullRequestAsync` member SHALL be
implemented for real (previously interface-only): given a head branch,
the configured `SpecRunnerOptions.BaseBranchName`, a title, and a body,
it SHALL create a pull request marked as a draft and return its PR
number.

#### Scenario: Draft PR is created against the configured base branch
- **WHEN** `CreateDraftPullRequestAsync` is called with head branch
  `"feature/45"`, a title, and a body, while `BaseBranchName` is
  `"main"`
- **THEN** a draft pull request from `"feature/45"` into `"main"` SHALL be
  created via the GitHub REST API and its PR number SHALL be returned

### Requirement: GitHub service lists open pull requests
`IGitHubService` SHALL provide an operation that lists open pull requests
on the configured repository, returning each pull request's number,
title, body, and head branch name, so callers can resolve which branch to
work against without a separate call per PR.

#### Scenario: Listing returns each open PR's head branch
- **WHEN** the operation is called against a repository with at least one
  open pull request
- **THEN** the result SHALL include that pull request's number, title,
  body, and head branch name

### Requirement: GitHub service reads and writes PR comments
`IGitHubService`'s `ReadPrCommentsAsync` and `WritePrCommentAsync` members SHALL be implemented for real (previously `NotImplementedException` placeholders): given a PR number, `ReadPrCommentsAsync` SHALL return that PR's general conversation comments (comment id, author, author association, body, and creation time), and `WritePrCommentAsync` SHALL create a new comment with a supplied body on that PR's conversation.

#### Scenario: Reading comments returns a PR's conversation comments
- **WHEN** `ReadPrCommentsAsync` is called with PR number `12`
- **THEN** the result SHALL include PR `12`'s conversation comments, each
  with its id, author, author association, body, and creation time

#### Scenario: A comment with no reported author association defaults to NONE
- **WHEN** the GitHub REST API response for a PR's comments omits the
  `author_association` property on a comment
- **THEN** that comment's author association SHALL resolve to `"NONE"`

#### Scenario: Writing a comment posts a reply on the PR
- **WHEN** `WritePrCommentAsync` is called with PR number `12` and body
  `"Pushed changes for this comment."`
- **THEN** a new comment with that body SHALL be created on PR `12`'s
  conversation

### Requirement: GitHub service lists a pull request's review comments
`IGitHubService` SHALL provide a `ListPrReviewCommentsAsync` operation that,
given a PR number, returns that pull request's file-anchored review
comments (comment id, file path, author, author association, body, and
creation time) via the GitHub REST API
(`GET /repos/{owner}/{repo}/pulls/{prNumber}/comments`), distinct from
`ReadPrCommentsAsync`'s general conversation comments.

#### Scenario: Listing returns each review comment's file path
- **WHEN** `ListPrReviewCommentsAsync` is called for a PR with at least one
  review comment anchored to file `"src/Login.cs"`
- **THEN** the result SHALL include that comment's id, file path
  (`"src/Login.cs"`), author, author association, body, and creation time

#### Scenario: A review comment with no reported author association defaults to NONE
- **WHEN** the GitHub REST API response for a PR's review comments omits
  the `author_association` property on a comment
- **THEN** that comment's author association SHALL resolve to `"NONE"`

### Requirement: GitHub service reads and adds reactions on a PR review comment
`IGitHubService` SHALL provide a `ListReviewCommentReactionsAsync`
operation that lists the reactions present on a given review comment
(including each reaction's author login and reaction type) and an
`AddReviewCommentReactionAsync` operation that adds a reaction of a given
type to a given review comment, both via the GitHub REST API's
`/pulls/comments/{commentId}/reactions` endpoint — distinct from the
`/issues/comments/{commentId}/reactions` endpoint used for conversation
comments, since review comments live in a separate id space.

#### Scenario: Listing reactions includes the author
- **WHEN** reactions are listed for a review comment that has an `eyes`
  reaction from the bot's own login
- **THEN** the result SHALL include that reaction's type (`eyes`) and its
  author login

#### Scenario: Adding a reaction posts it via the review-comment reactions endpoint
- **WHEN** a `+1` reaction is added to a review comment
- **THEN** the GitHub REST API's `/pulls/comments/{commentId}/reactions`
  endpoint SHALL be called to add a `+1` reaction to that comment,
  attributed to the authenticated bot identity, rather than the
  `/issues/comments/{commentId}/reactions` endpoint

### Requirement: GitHub service replies to a PR review comment
`IGitHubService` SHALL provide a `ReplyToReviewCommentAsync` operation
that, given a PR number, the id of an existing review comment, and a
reply body, creates a new review comment threaded under the given comment
via the GitHub REST API
(`POST /repos/{owner}/{repo}/pulls/{prNumber}/comments` with
`{ "body": body, "in_reply_to": commentId }`), without requiring a
`commit_id`, `path`, or `line`, since those are only required when
starting a new, non-reply review comment.

#### Scenario: Replying threads under the original review comment
- **WHEN** `ReplyToReviewCommentAsync` is called with PR number `12`,
  review comment id `9001`, and body `"Pushed changes for this comment."`
- **THEN** a new review comment with that body SHALL be created on PR
  `12`, threaded under comment `9001` via the `in_reply_to` parameter

#### Scenario: A failing reply call is reported, not left to crash the caller
- **WHEN** the underlying GitHub REST API call for
  `ReplyToReviewCommentAsync` fails (non-2xx response or network error)
- **THEN** the operation SHALL report the failure through its return value
  or a specific exception type, without an unhandled/unstructured
  exception escaping the call

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

### Requirement: GitHub operations report failures without throwing raw HTTP exceptions
Each newly implemented `IGitHubService` operation SHALL surface GitHub
REST API failures (non-2xx responses, network errors) as a typed result
or a specific, catchable exception distinguishable from a successful
outcome, rather than letting an unstructured `HttpRequestException`
propagate uncaught.

#### Scenario: A failing GitHub API call is reported, not left to crash the caller
- **WHEN** any newly implemented `IGitHubService` operation's underlying
  GitHub REST API call fails
- **THEN** the operation SHALL report the failure through its return
  value or a specific exception type, without an unhandled/unstructured
  exception escaping the call
