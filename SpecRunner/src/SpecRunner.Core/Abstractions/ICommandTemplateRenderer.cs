namespace SpecRunner.Core.Abstractions;

public interface ICommandTemplateRenderer
{
    Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default);
}
