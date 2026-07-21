namespace SpecRunner.Core.Models;

public enum CliAgentEventKind
{
    AssistantMessage,
    ToolUse,
    ToolResult,
    SystemInfo,
    Error,
    ResultCompleted
}
