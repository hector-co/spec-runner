## ADDED Requirements

### Requirement: An eligible `/implement` comment is only processed if its author is authorized
The workflow SHALL call `CommentAuthorization.IsAuthorized` with the triggering comment's author and author association when building the list of eligible comments, in addition to trigger-token matching. A comment whose trigger token matches but whose author is not authorized SHALL NOT be added to the eligible-comments list; the workflow SHALL instead log a warning identifying the comment id, PR number, and author, and SHALL NOT add any reaction to the comment, post any reply, or perform any PR-adoption, git, CLI-agent, or GitHub-write operation for it.

#### Scenario: An `/implement` comment from an unauthorized author is silently skipped
- **WHEN** a scan pass finds a PR comment whose trimmed body is exactly
  `/implement`, posted by an author whose `author_association` is `"NONE"`
  and who is not present in `AllowedTriggerUsers`
- **THEN** that comment SHALL NOT be treated as an eligible trigger, no
  reaction or reply SHALL be posted for it, no PR-adoption attempt SHALL be
  made, and a warning log entry recording the comment id, PR number, and
  author SHALL be emitted

#### Scenario: An `/implement` comment from an authorized author is processed as before
- **WHEN** a scan pass finds a PR comment whose trimmed body is exactly
  `/implement`, posted by an author whose `author_association` is
  `"COLLABORATOR"`
- **THEN** that comment SHALL be treated as an eligible trigger and
  processed following the existing `/implement` workflow, unchanged by this
  requirement
