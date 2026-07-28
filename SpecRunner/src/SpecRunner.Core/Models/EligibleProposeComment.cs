namespace SpecRunner.Core.Models;

public record EligibleProposeComment(int IssueNumber, string IssueTitle, string IssueBody, long CommentId, string Author, string AuthorAssociation);
