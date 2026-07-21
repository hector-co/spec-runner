using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IRepositoryConnectionTester
{
    Task<RepositoryConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
