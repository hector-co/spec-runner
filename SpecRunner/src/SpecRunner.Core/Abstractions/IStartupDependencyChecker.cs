using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IStartupDependencyChecker
{
    Task<IReadOnlyList<DependencyCheckResult>> CheckAllAsync(CancellationToken cancellationToken = default);
}
