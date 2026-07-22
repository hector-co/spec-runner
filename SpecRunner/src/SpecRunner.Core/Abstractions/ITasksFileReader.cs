namespace SpecRunner.Core.Abstractions;

public interface ITasksFileReader
{
    Task<string?> ReadCurrentAsync(string specName, CancellationToken cancellationToken = default);

    Task<string?> ReadArchivedAsync(string specName, CancellationToken cancellationToken = default);
}
