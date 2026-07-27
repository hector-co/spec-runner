using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class FakePrAdoptionService : IPrAdoptionService
{
    public PrAdoptionResult Result { get; set; } = PrAdoptionResult.NoSpecFolderFound();

    public List<(int PrNumber, string HeadBranch)> Calls { get; } = new();

    public Task<PrAdoptionResult> TryAdoptAsync(int prNumber, string headBranch, CancellationToken cancellationToken = default)
    {
        Calls.Add((prNumber, headBranch));
        return Task.FromResult(Result);
    }
}
