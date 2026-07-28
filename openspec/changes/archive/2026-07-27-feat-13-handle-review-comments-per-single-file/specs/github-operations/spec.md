## ADDED Requirements

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
