using System.Text.Json;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.State;

public class JsonFileStateStore : IStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonFileStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<SpecRunnerState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SpecRunnerState();
        }

        await using var stream = File.OpenRead(_filePath);
        var state = await JsonSerializer.DeserializeAsync<SpecRunnerState>(stream, SerializerOptions, cancellationToken);
        return state ?? new SpecRunnerState();
    }

    public async Task SaveAsync(SpecRunnerState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    public async Task<TrackedIssue?> FindByIssueNumberAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Issues.FirstOrDefault(issue => issue.IssueNumber == issueNumber);
    }

    public async Task<TrackedIssue?> FindByPrNumberAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(cancellationToken);
        return state.Issues.FirstOrDefault(issue => issue.PrNumber == prNumber);
    }
}
