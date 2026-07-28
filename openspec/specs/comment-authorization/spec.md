# comment-authorization

## Purpose

TBD - defines the shared authorization decision used by every
comment-triggered workflow to determine whether a triggering comment's
author is permitted to cause bot action, and requires each such workflow to
apply that decision before doing any work for a comment.

## Requirements

### Requirement: A shared helper decides whether a comment's author is authorized to trigger bot action
`SpecRunner.Core` SHALL define a static `CommentAuthorization.IsAuthorized(string author, string authorAssociation, SpecRunnerOptions options)` operation. It SHALL return `true` when either the given `author` case-insensitively matches an entry in `options.AllowedTriggerUsers`, or the given `authorAssociation` case-insensitively matches an entry in `options.AllowedAuthorAssociations`; otherwise it SHALL return `false`. This single operation SHALL be the sole authorization decision point used by every comment-triggered workflow runner.

#### Scenario: Author matching the association allowlist is authorized
- **WHEN** `IsAuthorized` is called with `authorAssociation` set to `"OWNER"` and `options.AllowedAuthorAssociations` containing `"OWNER"`
- **THEN** the result SHALL be `true`

#### Scenario: Author matching neither allowlist is not authorized
- **WHEN** `IsAuthorized` is called with `author` set to `"rando"`, `authorAssociation` set to `"NONE"`, `options.AllowedAuthorAssociations` set to its default (`"OWNER"`, `"MEMBER"`, `"COLLABORATOR"`), and `options.AllowedTriggerUsers` empty
- **THEN** the result SHALL be `false`

#### Scenario: Explicit username allowlist authorizes a non-collaborator
- **WHEN** `IsAuthorized` is called with `author` set to `"trusted-external"`, `authorAssociation` set to `"NONE"`, and `options.AllowedTriggerUsers` containing `"trusted-external"`
- **THEN** the result SHALL be `true`, even though `authorAssociation` does not match `AllowedAuthorAssociations`

#### Scenario: Both comparisons are case-insensitive
- **WHEN** `IsAuthorized` is called with `author` set to `"Trusted-External"` and `options.AllowedTriggerUsers` containing `"trusted-external"`, or with `authorAssociation` set to `"owner"` and `options.AllowedAuthorAssociations` containing `"OWNER"`
- **THEN** the result SHALL be `true` in both cases

### Requirement: Each comment-triggered workflow filters out unauthorized triggering comments before any bot action
Each of `ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, and `FinalizeWorkflowRunner` SHALL, when building its list of eligible comments for a scan pass, call `CommentAuthorization.IsAuthorized` with the triggering comment's author and author association. A comment whose trigger token matches but whose author is not authorized SHALL NOT be added to the eligible-comments list, SHALL NOT receive any GitHub reaction or reply, and SHALL NOT cause any git, CLI-agent, or GitHub-write operation. A single `LogWarning` entry identifying the comment id, the issue or PR number, and the author SHALL be emitted for each such skipped comment.

#### Scenario: An unauthorized trigger comment is skipped without any GitHub-visible trace
- **WHEN** a scan pass finds a comment whose trimmed body is exactly `/propose` (or `/implement`, `/update`, `/finalize`), posted by an author whose `author_association` is `"NONE"` and who is not present in `AllowedTriggerUsers`
- **THEN** that comment SHALL NOT be added to the eligible-comments list, no reaction SHALL be added to it, no reply SHALL be posted, and no git/CLI-agent/GitHub-write operation SHALL occur for it, while a warning log entry recording the comment id, issue/PR number, and author SHALL be emitted

#### Scenario: An authorized trigger comment is processed exactly as before this change
- **WHEN** a scan pass finds an eligible trigger comment posted by an author whose `author_association` is `"OWNER"`
- **THEN** that comment SHALL be added to the eligible-comments list and processed following the same sequence of steps (reaction, git, CLI-agent, GitHub writes) as prior to this change
