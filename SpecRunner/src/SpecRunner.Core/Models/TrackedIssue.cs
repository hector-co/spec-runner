namespace SpecRunner.Core.Models;

public record TrackedIssue(int? IssueNumber, string SpecName)
{
    public string BranchName { get; init; } = string.Empty;

    public int? PrNumber { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public List<TrackedComment> Comments { get; init; } = new();
}
