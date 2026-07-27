namespace SpecRunner.Core.Models;

public enum PrAdoptionFailureReason
{
    NoSpecFolderFound,
    MultipleSpecFoldersFound,
    MultipleIssuesFound
}

public record PrAdoptionResult
{
    public TrackedIssue? TrackedIssue { get; private init; }

    public PrAdoptionFailureReason? FailureReason { get; private init; }

    public IReadOnlyList<string> CandidateSpecFolderNames { get; private init; } = Array.Empty<string>();

    public IReadOnlyList<int> CandidateIssueNumbers { get; private init; } = Array.Empty<int>();

    public bool Succeeded => TrackedIssue is not null;

    public string FailureMessage => FailureReason switch
    {
        PrAdoptionFailureReason.NoSpecFolderFound =>
            "No OpenSpec change folder was found on this branch, so there's nothing to adopt.",
        PrAdoptionFailureReason.MultipleSpecFoldersFound =>
            $"Multiple candidate OpenSpec change folders were found on this branch ({string.Join(", ", CandidateSpecFolderNames)}), so which one applies can't be determined.",
        PrAdoptionFailureReason.MultipleIssuesFound =>
            $"Multiple linked issues were found for this PR ({string.Join(", ", CandidateIssueNumbers.Select(number => $"#{number}"))}), so which one applies can't be determined.",
        _ => "This PR could not be adopted."
    };

    public static PrAdoptionResult Success(TrackedIssue trackedIssue) => new() { TrackedIssue = trackedIssue };

    public static PrAdoptionResult NoSpecFolderFound() => new() { FailureReason = PrAdoptionFailureReason.NoSpecFolderFound };

    public static PrAdoptionResult MultipleSpecFoldersFound(IReadOnlyList<string> candidateNames) => new()
    {
        FailureReason = PrAdoptionFailureReason.MultipleSpecFoldersFound,
        CandidateSpecFolderNames = candidateNames
    };

    public static PrAdoptionResult MultipleIssuesFound(IReadOnlyList<int> candidateIssueNumbers) => new()
    {
        FailureReason = PrAdoptionFailureReason.MultipleIssuesFound,
        CandidateIssueNumbers = candidateIssueNumbers
    };
}
