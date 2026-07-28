## MODIFIED Requirements

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
