using System.Runtime.InteropServices;
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

builder.Services
    .AddOptions<OpenSpecCliOptions>()
    .Bind(builder.Configuration.GetSection(OpenSpecCliOptions.SectionName));

builder.Services.AddSingleton<ISpecNameResolver, SpecNameResolver>();
builder.Services.AddSingleton<ITasksFileReader, TasksFileReader>();
builder.Services.AddSingleton<ISpecFolderResolver, SpecFolderResolver>();
builder.Services.AddSingleton<ICommandTemplateRenderer, CommandTemplateRenderer>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddSingleton<ICliAgentSessionFactory, ClaudeCliAgentSessionFactory>();
builder.Services.AddHttpClient<IRepositoryConnectionTester, HttpRepositoryConnectionTester>();
builder.Services.AddSingleton<ICliToolAvailabilityChecker, ProcessCliToolAvailabilityChecker>();
builder.Services.AddSingleton<IStartupDependencyChecker, StartupDependencyChecker>();
builder.Services.AddSingleton<IPrAdoptionService, PrAdoptionService>();
builder.Services.AddSingleton<IProposeWorkflowRunner, ProposeWorkflowRunner>();
builder.Services.AddSingleton<IImplementWorkflowRunner, ImplementWorkflowRunner>();
builder.Services.AddSingleton<IUpdateWorkflowRunner, UpdateWorkflowRunner>();
builder.Services.AddSingleton<IFinalizeWorkflowRunner, FinalizeWorkflowRunner>();
builder.Services.AddSingleton<IStateStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SpecRunnerOptions>>().Value;
    var filePath = Path.Combine(options.LocalRepositoryPath, ".specrunner", "state.db");
    return new SqliteStateStore(filePath);
});

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("SpecRunner host started");

var startupDependencyChecker = host.Services.GetRequiredService<IStartupDependencyChecker>();
var dependencyResults = await startupDependencyChecker.CheckAllAsync();

foreach (var dependencyResult in dependencyResults)
{
    if (dependencyResult.IsSuccessful)
    {
        logger.LogInformation(
            "Startup dependency check: {Name} - {Message}",
            dependencyResult.Name,
            dependencyResult.Message);
    }
    else
    {
        logger.LogError(
            "Startup dependency check: {Name} - {Message}",
            dependencyResult.Name,
            dependencyResult.Message);
    }

    Console.WriteLine($"{dependencyResult.Name}: {(dependencyResult.IsSuccessful ? "OK" : "FAILED")} - {dependencyResult.Message}");
}

var failedDependencies = dependencyResults.Where(r => !r.IsSuccessful).ToList();
if (failedDependencies.Count > 0)
{
    logger.LogError(
        "Startup dependency check failed for: {FailedDependencies}",
        string.Join(", ", failedDependencies.Select(r => r.Name)));
    return 1;
}

using var shutdownCts = new CancellationTokenSource();

void HandleShutdownSignal(PosixSignalContext context)
{
    context.Cancel = true;
    shutdownCts.Cancel();
}

using var sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, HandleShutdownSignal);
using var sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleShutdownSignal);

var proposeWorkflowRunner = host.Services.GetRequiredService<IProposeWorkflowRunner>();
var implementWorkflowRunner = host.Services.GetRequiredService<IImplementWorkflowRunner>();
var updateWorkflowRunner = host.Services.GetRequiredService<IUpdateWorkflowRunner>();
var finalizeWorkflowRunner = host.Services.GetRequiredService<IFinalizeWorkflowRunner>();
var options = host.Services.GetRequiredService<IOptions<SpecRunnerOptions>>().Value;

await PollingLoop.RunAsync(proposeWorkflowRunner, implementWorkflowRunner, updateWorkflowRunner, finalizeWorkflowRunner, options.PollingInterval, shutdownCts.Token, logger);

return 0;
