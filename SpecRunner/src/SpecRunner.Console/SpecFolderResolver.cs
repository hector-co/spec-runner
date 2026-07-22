using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Console;

public class SpecFolderResolver : ISpecFolderResolver
{
    private readonly SpecRunnerOptions _options;

    public SpecFolderResolver(IOptions<SpecRunnerOptions> options)
    {
        _options = options.Value;
    }

    public Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken cancellationToken = default)
    {
        var changesRoot = Path.Combine(_options.LocalRepositoryPath, "openspec", "changes");

        var expectedPath = Path.Combine(changesRoot, expectedSpecName);
        if (Directory.Exists(expectedPath))
        {
            return Task.FromResult(expectedSpecName);
        }

        if (Directory.Exists(changesRoot))
        {
            var prefix = $"feat-{issueNumber}-";
            foreach (var directory in Directory.EnumerateDirectories(changesRoot))
            {
                var name = Path.GetFileName(directory);
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return Task.FromResult(name);
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve a spec folder for issue #{issueNumber} (expected \"{expectedSpecName}\").");
    }
}
