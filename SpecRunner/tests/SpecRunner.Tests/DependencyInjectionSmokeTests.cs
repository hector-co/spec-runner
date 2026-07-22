using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpecRunner.Cli;
using SpecRunner.Console;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.GitHub;
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
        services.AddSingleton<IStateStore>(_ => new SqliteStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "state.db")));
        services.AddOptions<SpecRunnerOptions>();
        services.AddOptions<CliAgentOptions>();
        services.AddHttpClient<IRepositoryConnectionTester, HttpRepositoryConnectionTester>();
        services.AddSingleton<ICliAgentSessionFactory, ClaudeCliAgentSessionFactory>();
        services.AddSingleton<IProposeWorkflowRunner, ProposeWorkflowRunner>();
        services.AddSingleton<IImplementWorkflowRunner, ImplementWorkflowRunner>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IStateStore>());
        Assert.NotNull(provider.GetRequiredService<ISpecNameResolver>());
        Assert.NotNull(provider.GetRequiredService<IGitService>());
        Assert.NotNull(provider.GetRequiredService<IGitHubService>());
        Assert.NotNull(provider.GetRequiredService<IRepositoryConnectionTester>());
        Assert.NotNull(provider.GetRequiredService<ICliAgentSessionFactory>());
        Assert.NotNull(provider.GetRequiredService<IProposeWorkflowRunner>());
        Assert.NotNull(provider.GetRequiredService<IImplementWorkflowRunner>());
    }
}
