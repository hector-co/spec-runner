using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Console;

public class TasksFileReader : ITasksFileReader
{
    private readonly SpecRunnerOptions _options;

    public TasksFileReader(IOptions<SpecRunnerOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string?> ReadCurrentAsync(string specName, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.LocalRepositoryPath, "openspec", "changes", specName, "tasks.md");
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public async Task<string?> ReadArchivedAsync(string specName, CancellationToken cancellationToken = default)
    {
        var archiveRoot = Path.Combine(_options.LocalRepositoryPath, "openspec", "changes", "archive");
        if (!Directory.Exists(archiveRoot))
        {
            return null;
        }

        var suffix = $"-{specName}";
        string? bestTasksPath = null;
        var bestModifiedUtc = DateTime.MinValue;

        foreach (var directory in Directory.EnumerateDirectories(archiveRoot))
        {
            if (!Path.GetFileName(directory).EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var tasksPath = Path.Combine(directory, "tasks.md");
            if (!File.Exists(tasksPath))
            {
                continue;
            }

            var modifiedUtc = File.GetLastWriteTimeUtc(tasksPath);
            if (bestTasksPath is null || modifiedUtc > bestModifiedUtc)
            {
                bestTasksPath = tasksPath;
                bestModifiedUtc = modifiedUtc;
            }
        }

        return bestTasksPath is null
            ? null
            : await File.ReadAllTextAsync(bestTasksPath, cancellationToken).ConfigureAwait(false);
    }
}
