using SpecRunner.Console;

namespace SpecRunner.Tests;

public class CommandTemplateRendererTests
{
    private readonly CommandTemplateRenderer _renderer = new();

    [Fact]
    public async Task PlaceholdersAreReplacedWithSuppliedValues()
    {
        var rendered = await _renderer.RenderAsync(
            "propose",
            new Dictionary<string, string>
            {
                ["spec_name"] = "45-add-login-page",
                ["issue_title"] = "Add Login Page",
                ["issue_body"] = "We need a login page."
            });

        Assert.Equal(
            $"/opsx-propose 45-add-login-page{Environment.NewLine}Add Login Page{Environment.NewLine}We need a login page.{Environment.NewLine}{Environment.NewLine}" +
            $"This is an unattended run — do not ask for confirmation or clarification{Environment.NewLine}" +
            $"at any step. If something is ambiguous, make the most reasonable{Environment.NewLine}" +
            $"assumption, note it in proposal.md under a brief \"Assumptions\" note, and{Environment.NewLine}" +
            $"continue.",
            rendered);
        Assert.DoesNotContain("{{", rendered);
        Assert.DoesNotContain("}}", rendered);
    }

    [Fact]
    public async Task RenderingAnUnknownTemplateNameFailsClearly()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _renderer.RenderAsync("no-such-template", new Dictionary<string, string>()));

        Assert.Contains("no-such-template.txt", ex.Message);
    }

    [Fact]
    public async Task APlaceholderWithNoSuppliedValueFailsClearly()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _renderer.RenderAsync("apply", new Dictionary<string, string> { ["spec_name"] = "45-add-login-page" }));

        Assert.Contains("instructions", ex.Message);
    }

    [Fact]
    public async Task ADoubleQuoteInASuppliedValueIsEscapedNotRemoved()
    {
        var rendered = await _renderer.RenderAsync(
            "update",
            new Dictionary<string, string>
            {
                ["spec_name"] = "45-add-login-page",
                ["instructions"] = "also handle the \"edge case\" comment"
            });

        Assert.Contains("also handle the \\\"edge case\\\" comment", rendered);
    }

    [Fact]
    public async Task ABackslashInASuppliedValueIsEscapedBeforeQuoteEscaping()
    {
        var rendered = await _renderer.RenderAsync(
            "update",
            new Dictionary<string, string>
            {
                ["spec_name"] = "45-add-login-page",
                ["instructions"] = "ends with a backslash-quote: \\\""
            });

        Assert.Contains("ends with a backslash-quote: \\\\\\\"", rendered);
    }

    [Fact]
    public async Task AValueWithNoQuotesOrBackslashesIsUnaffected()
    {
        var rendered = await _renderer.RenderAsync(
            "update",
            new Dictionary<string, string>
            {
                ["spec_name"] = "45-add-login-page",
                ["instructions"] = "a plain instruction with no special characters"
            });

        Assert.Contains("a plain instruction with no special characters", rendered);
    }
}
