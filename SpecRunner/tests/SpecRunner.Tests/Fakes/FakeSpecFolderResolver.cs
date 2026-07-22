using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class FakeSpecFolderResolver : ISpecFolderResolver
{
    public Dictionary<string, string> ActualNameByExpectedName { get; } = new();

    public Exception? ThrowOnResolve { get; set; }

    public Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken cancellationToken = default)
    {
        if (ThrowOnResolve is not null)
        {
            throw ThrowOnResolve;
        }

        var resolved = ActualNameByExpectedName.TryGetValue(expectedSpecName, out var actual) ? actual : expectedSpecName;
        return Task.FromResult(resolved);
    }
}
