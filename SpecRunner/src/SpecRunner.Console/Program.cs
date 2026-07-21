using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
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

builder.Services.AddSingleton<ISpecNameResolver, SpecNameResolver>();
builder.Services.AddSingleton<IGitService, NotImplementedGitService>();
builder.Services.AddSingleton<IGitHubService, NotImplementedGitHubService>();
builder.Services.AddHttpClient<IRepositoryConnectionTester, HttpRepositoryConnectionTester>();
builder.Services.AddSingleton<IStateStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SpecRunnerOptions>>().Value;
    var filePath = Path.Combine(options.LocalRepositoryPath, ".specrunner", "state.json");
    return new JsonFileStateStore(filePath);
});

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Resolve registered services to confirm the DI container is wired correctly.
host.Services.GetRequiredService<IOptions<SpecRunnerOptions>>();
host.Services.GetRequiredService<ISpecNameResolver>();
host.Services.GetRequiredService<IStateStore>();
host.Services.GetRequiredService<IGitService>();
host.Services.GetRequiredService<IGitHubService>();
host.Services.GetRequiredService<IRepositoryConnectionTester>();

logger.LogInformation("SpecRunner host started");

return 0;
