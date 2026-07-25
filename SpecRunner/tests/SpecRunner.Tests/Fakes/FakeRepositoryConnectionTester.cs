using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class FakeRepositoryConnectionTester : IRepositoryConnectionTester
{
    public RepositoryConnectionResult Result { get; set; } =
        new(RepositoryConnectionStatus.Connected, "connected");

    public bool WasCalled { get; private set; }

    public Task<RepositoryConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return Task.FromResult(Result);
    }
}
