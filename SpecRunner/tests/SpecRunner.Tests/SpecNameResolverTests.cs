using SpecRunner.Core;

namespace SpecRunner.Tests;

public class SpecNameResolverTests
{
    private readonly SpecNameResolver _resolver = new();

    [Fact]
    public void ReplacesSpacesWithDashes()
    {
        var result = _resolver.Resolve(45, "Add Login Page");

        Assert.Equal("feat-45-add-login-page", result);
    }

    [Fact]
    public void LowerCasesTheTitle()
    {
        var result = _resolver.Resolve(1, "UPPER CASE TITLE");

        Assert.Equal("feat-1-upper-case-title", result);
    }

    [Fact]
    public void StripsInvalidFolderNameCharacters()
    {
        var result = _resolver.Resolve(7, "Fix: crash on save/load?");

        Assert.StartsWith("feat-7-", result);
        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('?', result);
    }
}
