using SpecRunner.Core.Models;
using SpecRunner.State;

namespace SpecRunner.Tests;

public class JsonFileStateStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _filePath;

    public JsonFileStateStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _filePath = Path.Combine(_tempDirectory, "state.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadReturnsEmptyStateWhenFileIsMissing()
    {
        var store = new JsonFileStateStore(_filePath);

        var state = await store.LoadAsync();

        Assert.Empty(state.Issues);
    }

    [Fact]
    public async Task SaveThenLoadRoundTripsState()
    {
        var store = new JsonFileStateStore(_filePath);
        var issue = new TrackedIssue(45, "45-add-login-page")
        {
            PrNumber = 12,
            Comments = { new TrackedComment(1, "propose", CommentStatus.Working) }
        };
        var state = new SpecRunnerState { Issues = { issue } };

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync();

        var loadedIssue = Assert.Single(loaded.Issues);
        Assert.Equal(issue.IssueNumber, loadedIssue.IssueNumber);
        Assert.Equal(issue.SpecName, loadedIssue.SpecName);
        Assert.Equal(issue.PrNumber, loadedIssue.PrNumber);
        var loadedComment = Assert.Single(loadedIssue.Comments);
        Assert.Equal(1, loadedComment.CommentId);
        Assert.Equal("propose", loadedComment.CommentKind);
        Assert.Equal(CommentStatus.Working, loadedComment.Status);
    }

    [Fact]
    public async Task FindByIssueNumberReturnsMatchingRecord()
    {
        var store = new JsonFileStateStore(_filePath);
        var state = new SpecRunnerState
        {
            Issues = { new TrackedIssue(45, "45-add-login-page") }
        };
        await store.SaveAsync(state);

        var found = await store.FindByIssueNumberAsync(45);

        Assert.NotNull(found);
        Assert.Equal("45-add-login-page", found!.SpecName);
    }

    [Fact]
    public async Task FindByPrNumberReturnsMatchingRecord()
    {
        var store = new JsonFileStateStore(_filePath);
        var issue = new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 };
        var state = new SpecRunnerState { Issues = { issue } };
        await store.SaveAsync(state);

        var found = await store.FindByPrNumberAsync(12);

        Assert.NotNull(found);
        Assert.Equal(45, found!.IssueNumber);
    }
}
