namespace SpecRunner.Core.Models;

public record EligibleUpdateComment(int PrNumber, string PrHeadBranch, long CommentId, string Instructions, string Author, string AuthorAssociation);
