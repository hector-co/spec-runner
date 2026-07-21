namespace SpecRunner.Core.Configuration;

public class CliAgentOptions
{
    public const string SectionName = "CliAgent";

    public string Executable { get; set; } = "claude";

    public List<string> Arguments { get; set; } = new();

    public string? WorkingDirectory { get; set; }
}
