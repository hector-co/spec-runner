# github-operations

## Purpose

TBD - defines the real `SpecRunner.GitHub` implementation of the
`IGitHubService` members the propose workflow depends on: identity
resolution, listing open issues with comments, reactions, issue comments,
and draft PR creation, plus failure reporting.

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
author, body, and creation time), so callers can scan comment bodies
without a separate call per issue.

#### Scenario: Listing returns comments alongside each open issue
- **WHEN** the operation is called against a repository with at least one
  open issue that has comments
- **THEN** the result SHALL include that issue's number, title, body, and
  its comments with each comment's id, author, and body

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
`IGitHubService`'s `ReadPrCommentsAsync` and `WritePrCommentAsync` members
SHALL be implemented for real (previously `NotImplementedException`
placeholders): given a PR number, `ReadPrCommentsAsync` SHALL return that
PR's general conversation comments (comment id, author, body, and
creation time), and `WritePrCommentAsync` SHALL create a new comment with
a supplied body on that PR's conversation.

#### Scenario: Reading comments returns a PR's conversation comments
- **WHEN** `ReadPrCommentsAsync` is called with PR number `12`
- **THEN** the result SHALL include PR `12`'s conversation comments, each
  with its id, author, body, and creation time

#### Scenario: Writing a comment posts a reply on the PR
- **WHEN** `WritePrCommentAsync` is called with PR number `12` and body
  `"Pushed changes for this comment."`
- **THEN** a new comment with that body SHALL be created on PR `12`'s
  conversation

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
