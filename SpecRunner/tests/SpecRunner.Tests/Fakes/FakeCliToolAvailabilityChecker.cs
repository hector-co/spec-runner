using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class FakeCliToolAvailabilityChecker : ICliToolAvailabilityChecker
{
    private readonly Dictionary<string, ToolAvailabilityResult> _resultsByExecutable = new();

    public ToolAvailabilityResult DefaultResult { get; set; } =
        new(ToolAvailabilityStatus.Available, "available");

    public List<string> CheckedExecutables { get; } = new();

    public void SetResult(string executable, ToolAvailabilityResult result) => _resultsByExecutable[executable] = result;

    public Task<ToolAvailabilityResult> CheckAsync(string executable, CancellationToken cancellationToken = default)
    {
        CheckedExecutables.Add(executable);
        var result = _resultsByExecutable.TryGetValue(executable, out var configured) ? configured : DefaultResult;
        return Task.FromResult(result);
    }
}
