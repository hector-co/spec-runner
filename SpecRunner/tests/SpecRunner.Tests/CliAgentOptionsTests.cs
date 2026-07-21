using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Tests;

public class CliAgentOptionsTests
{
    [Fact]
    public void BindsExplicitValuesFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CliAgent:Executable"] = "custom-agent",
                ["CliAgent:Arguments:0"] = "--flag",
                ["CliAgent:Arguments:1"] = "value",
                ["CliAgent:WorkingDirectory"] = "/tmp/workdir"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<CliAgentOptions>().Bind(configuration.GetSection(CliAgentOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CliAgentOptions>>().Value;

        Assert.Equal("custom-agent", options.Executable);
        Assert.Equal(new[] { "--flag", "value" }, options.Arguments);
        Assert.Equal("/tmp/workdir", options.WorkingDirectory);
    }

    [Fact]
    public void ExecutableDefaultsToClaudeWhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var services = new ServiceCollection();
        services.AddOptions<CliAgentOptions>().Bind(configuration.GetSection(CliAgentOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CliAgentOptions>>().Value;

        Assert.Equal("claude", options.Executable);
        Assert.Empty(options.Arguments);
        Assert.Null(options.WorkingDirectory);
    }
}
