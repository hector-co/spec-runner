namespace SpecRunner.Core.Models;

public record EligibleFinalizeComment(int PrNumber, string PrHeadBranch, string PrTitle, long CommentId, string Instructions, string Author, string AuthorAssociation);
