using System.Text.RegularExpressions;
using SpecRunner.Core.Abstractions;

namespace SpecRunner.Console;

public partial class CommandTemplateRenderer : ICommandTemplateRenderer
{
    public async Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CommandTemplates", $"{templateName}.txt");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Command template '{templateName}' was not found at '{path}'.", path);
        }

        var template = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return TokenRegex().Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            if (!values.TryGetValue(token, out var value))
            {
                throw new InvalidOperationException(
                    $"Command template '{templateName}' has an unresolved placeholder '{{{{{token}}}}}'.");
            }

            return value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();
}
