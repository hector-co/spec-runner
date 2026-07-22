namespace SpecRunner.Core.Abstractions;

public interface ISpecFolderResolver
{
    Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken cancellationToken = default);
}
