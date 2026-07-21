using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Git;
using SpecRunner.GitHub;
using SpecRunner.State;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<SpecRunnerOptions>()
    .Bind(builder.Configuration.GetSection(SpecRunnerOptions.SectionName));

builder.Services.AddSingleton<ISpecNameResolver, SpecNameResolver>();
builder.Services.AddSingleton<IGitService, NotImplementedGitService>();
builder.Services.AddSingleton<IGitHubService, NotImplementedGitHubService>();
builder.Services.AddSingleton<IStateStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SpecRunnerOptions>>().Value;
    var filePath = Path.Combine(options.LocalRepositoryPath, ".specrunner", "state.json");
    return new JsonFileStateStore(filePath);
});

using var host = builder.Build();

// Resolve registered services to confirm the DI container is wired correctly.
host.Services.GetRequiredService<IOptions<SpecRunnerOptions>>();
host.Services.GetRequiredService<ISpecNameResolver>();
host.Services.GetRequiredService<IStateStore>();
host.Services.GetRequiredService<IGitService>();
host.Services.GetRequiredService<IGitHubService>();

return 0;
