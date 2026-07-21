namespace SpecRunner.Core.Models;

public record TrackedIssue(int IssueNumber, string SpecName)
{
    public int? PrNumber { get; set; }

    public List<TrackedComment> Comments { get; init; } = new();
}
