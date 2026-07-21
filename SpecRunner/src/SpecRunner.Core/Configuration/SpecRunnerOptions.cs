namespace SpecRunner.Core.Configuration;

public class SpecRunnerOptions
{
    public const string SectionName = "SpecRunner";

    public string GitHubToken { get; set; } = string.Empty;

    public string RepositoryOwner { get; set; } = string.Empty;

    public string RepositoryName { get; set; } = string.Empty;

    public string LocalRepositoryPath { get; set; } = string.Empty;

    public string BaseBranchName { get; set; } = "main";

    public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
