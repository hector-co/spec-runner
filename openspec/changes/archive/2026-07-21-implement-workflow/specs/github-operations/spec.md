## ADDED Requirements

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
