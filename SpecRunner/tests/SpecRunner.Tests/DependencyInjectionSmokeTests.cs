using Microsoft.Extensions.DependencyInjection;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.State;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class DependencyInjectionSmokeTests
{
    [Fact]
    public void ContainerResolvesAllCoreServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpecNameResolver, SpecNameResolver>();
        services.AddSingleton<IGitService, FakeGitService>();
        services.AddSingleton<IGitHubService, FakeGitHubService>();
        services.AddSingleton<IStateStore>(_ => new JsonFileStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "state.json")));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IStateStore>());
        Assert.NotNull(provider.GetRequiredService<ISpecNameResolver>());
        Assert.NotNull(provider.GetRequiredService<IGitService>());
        Assert.NotNull(provider.GetRequiredService<IGitHubService>());
    }
}
