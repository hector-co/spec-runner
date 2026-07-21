namespace SpecRunner.Core.Models;

public record CliAgentEvent(CliAgentEventKind Kind, string Payload, string RawLine, DateTimeOffset Timestamp)
{
    public int? ExitCode { get; init; }

    public string? StdErr { get; init; }
}
