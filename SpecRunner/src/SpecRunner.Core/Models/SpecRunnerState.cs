namespace SpecRunner.Core.Models;

public record SpecRunnerState
{
    public List<TrackedIssue> Issues { get; init; } = new();
}
