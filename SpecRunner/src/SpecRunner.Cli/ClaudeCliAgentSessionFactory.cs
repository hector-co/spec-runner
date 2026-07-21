using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Cli;

public class ClaudeCliAgentSessionFactory : ICliAgentSessionFactory
{
    private readonly IOptions<CliAgentOptions> _cliAgentOptions;
    private readonly IOptions<SpecRunnerOptions> _specRunnerOptions;

    public ClaudeCliAgentSessionFactory(IOptions<CliAgentOptions> cliAgentOptions, IOptions<SpecRunnerOptions> specRunnerOptions)
    {
        _cliAgentOptions = cliAgentOptions;
        _specRunnerOptions = specRunnerOptions;
    }

    public ICliAgentSession CreateSession()
        => new ClaudeCliAgentSession(_cliAgentOptions.Value, _specRunnerOptions.Value);
}
