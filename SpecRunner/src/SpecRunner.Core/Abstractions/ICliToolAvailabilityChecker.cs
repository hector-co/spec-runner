using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface ICliToolAvailabilityChecker
{
    Task<ToolAvailabilityResult> CheckAsync(string executable, CancellationToken cancellationToken = default);
}
