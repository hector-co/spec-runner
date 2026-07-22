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
                ["issue_body"] = "We need a login page."
            });

        Assert.Equal(
            "/opsx-propose 45-add-login-page\nWe need a login page.\n\n" +
            "This is an unattended run — do not ask for confirmation or clarification\n" +
            "at any step. If something is ambiguous, make the most reasonable\n" +
            "assumption, note it in proposal.md under a brief \"Assumptions\" note, and\n" +
            "continue.",
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
}
