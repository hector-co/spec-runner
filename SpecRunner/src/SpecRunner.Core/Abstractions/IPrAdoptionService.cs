using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IPrAdoptionService
{
    Task<PrAdoptionResult> TryAdoptAsync(int prNumber, string headBranch, CancellationToken cancellationToken = default);
}
