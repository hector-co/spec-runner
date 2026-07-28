namespace SpecRunner.Core.Configuration;

public class SpecRunnerOptions
{
    public const string SectionName = "SpecRunner";

    public string GitHubToken { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string LocalRepositoryPath { get; set; } = string.Empty;

    public string BaseBranchName { get; set; } = "main";

    public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    public List<string> AllowedAuthorAssociations { get; set; } = new() { "OWNER", "MEMBER", "COLLABORATOR" };

    public List<string> AllowedTriggerUsers { get; set; } = new();
}
