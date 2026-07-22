using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class FakeTasksFileReader : ITasksFileReader
{
    public Dictionary<string, string> CurrentContentBySpecName { get; } = new();

    public Dictionary<string, string> ArchivedContentBySpecName { get; } = new();

    public Task<string?> ReadCurrentAsync(string specName, CancellationToken cancellationToken = default)
        => Task.FromResult(CurrentContentBySpecName.TryGetValue(specName, out var content) ? content : null);

    public Task<string?> ReadArchivedAsync(string specName, CancellationToken cancellationToken = default)
        => Task.FromResult(ArchivedContentBySpecName.TryGetValue(specName, out var content) ? content : null);
}
