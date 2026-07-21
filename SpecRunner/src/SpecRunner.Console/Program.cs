using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SpecRunner.Cli;
using SpecRunner.Console;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.Git;
using SpecRunner.GitHub;
using SpecRunner.State;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(loggerConfiguration => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration));

builder.Services
    .AddOptions<SpecRunnerOptions>()
    .Bind(builder.Configuration.GetSection(SpecRunnerOptions.SectionName));

builder.Services
    .AddOptions<CliAgentOptions>()
    .Bind(builder.Configuration.GetSection(CliAgentOptions.SectionName));

builder.Services.AddSingleton<ISpecNameResolver, SpecNameResolver>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddSingleton<ICliAgentSessionFactory, ClaudeCliAgentSessionFactory>();
builder.Services.AddHttpClient<IRepositoryConnectionTester, HttpRepositoryConnectionTester>();
builder.Services.AddSingleton<IProposeWorkflowRunner, ProposeWorkflowRunner>();
builder.Services.AddSingleton<IStateStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SpecRunnerOptions>>().Value;
    var filePath = Path.Combine(options.LocalRepositoryPath, ".specrunner", "state.db");
    return new SqliteStateStore(filePath);
});

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("SpecRunner host started");

var connectionTester = host.Services.GetRequiredService<IRepositoryConnectionTester>();
var connectionResult = await connectionTester.TestConnectionAsync();

logger.LogInformation(
    "Repository connection test result: {Status} - {Message}",
    connectionResult.Status,
    connectionResult.Message);
Console.WriteLine($"Repository connection: {connectionResult.Status} - {connectionResult.Message}");

if (connectionResult.Status != RepositoryConnectionStatus.Connected)
{
    return 1;
}

var proposeWorkflowRunner = host.Services.GetRequiredService<IProposeWorkflowRunner>();
await proposeWorkflowRunner.RunOnceAsync();

return 0;
