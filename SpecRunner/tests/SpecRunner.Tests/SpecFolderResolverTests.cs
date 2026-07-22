using Microsoft.Extensions.Options;
using SpecRunner.Console;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Tests;

public class SpecFolderResolverTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _changesRoot;
    private readonly SpecFolderResolver _resolver;

    public SpecFolderResolverTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _changesRoot = Path.Combine(_tempDirectory, "openspec", "changes");
        Directory.CreateDirectory(_changesRoot);

        var options = Options.Create(new SpecRunnerOptions
        {
            LocalRepositoryPath = _tempDirectory
        });
        _resolver = new SpecFolderResolver(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReturnsExpectedNameWhenExactFolderExists()
    {
        Directory.CreateDirectory(Path.Combine(_changesRoot, "feat-45-add-login-page"));

        var result = await _resolver.ResolveAsync("feat-45-add-login-page", 45);

        Assert.Equal("feat-45-add-login-page", result);
    }

    [Fact]
    public async Task FallsBackToPrefixMatchWhenExactFolderMissing()
    {
        Directory.CreateDirectory(Path.Combine(_changesRoot, "feat-45-login-page"));

        var result = await _resolver.ResolveAsync("feat-45-add-login-page", 45);

        Assert.Equal("feat-45-login-page", result);
    }

    [Fact]
    public async Task ThrowsWhenNoMatchingFolderExists()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _resolver.ResolveAsync("feat-45-add-login-page", 45));
    }
}
