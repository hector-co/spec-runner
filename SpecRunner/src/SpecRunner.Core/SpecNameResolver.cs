using System.Text.RegularExpressions;
using SpecRunner.Core.Abstractions;

namespace SpecRunner.Core;

public partial class SpecNameResolver : ISpecNameResolver
{
    public string Resolve(int issueNumber, string issueTitle)
    {
        var lowered = issueTitle.ToLowerInvariant();
        var dashed = WhitespaceRunRegex().Replace(lowered, "-");
        var sanitized = InvalidCharacterRegex().Replace(dashed, string.Empty);
        var collapsed = RepeatedDashRegex().Replace(sanitized, "-").Trim('-');

        return $"{issueNumber}-{collapsed}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    [GeneratedRegex(@"[^a-z0-9-]")]
    private static partial Regex InvalidCharacterRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex RepeatedDashRegex();
}
