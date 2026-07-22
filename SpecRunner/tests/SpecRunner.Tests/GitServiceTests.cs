using System.Diagnostics;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Configuration;
using SpecRunner.Git;

namespace SpecRunner.Tests;

public class GitServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _originPath;
    private readonly string _localPath;
    private readonly string _peerPath;
    private readonly GitService _gitService;

    public GitServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "specrunner-git-tests-" + Guid.NewGuid());
        _originPath = Path.Combine(_root, "origin.git");
        _localPath = Path.Combine(_root, "local");
        _peerPath = Path.Combine(_root, "peer");

        Directory.CreateDirectory(_originPath);
        RunGit(_originPath, "init", "--bare", "-b", "main");

        Directory.CreateDirectory(_localPath);
        RunGit(_localPath, "init", "-b", "main");
        RunGit(_localPath, "config", "user.email", "test@example.com");
        RunGit(_localPath, "config", "user.name", "Test User");
        RunGit(_localPath, "remote", "add", "origin", _originPath);
        File.WriteAllText(Path.Combine(_localPath, "initial.txt"), "initial");
        RunGit(_localPath, "add", "-A");
        RunGit(_localPath, "commit", "-m", "initial commit");
        RunGit(_localPath, "push", "-u", "origin", "main");

        var options = Options.Create(new SpecRunnerOptions
        {
            LocalRepositoryPath = _localPath,
            BaseBranchName = "main"
        });
        _gitService = new GitService(options);
    }

    public void Dispose()
    {
        TryDeleteDirectory(_root);
    }

    [Fact]
    public async Task PullAsyncFastForwardsBaseBranchFromOrigin()
    {
        RunGit(_root, "clone", _originPath, _peerPath);
        RunGit(_peerPath, "config", "user.email", "peer@example.com");
        RunGit(_peerPath, "config", "user.name", "Peer User");
        File.WriteAllText(Path.Combine(_peerPath, "from-peer.txt"), "peer change");
        RunGit(_peerPath, "add", "-A");
        RunGit(_peerPath, "commit", "-m", "peer commit");
        RunGit(_peerPath, "push", "origin", "main");

        await _gitService.PullAsync();

        Assert.True(File.Exists(Path.Combine(_localPath, "from-peer.txt")));
    }

    [Fact]
    public async Task FetchAsyncRetrievesNamedBranchWithoutChangingCheckedOutBranch()
    {
        RunGit(_root, "clone", _originPath, _peerPath);
        RunGit(_peerPath, "config", "user.email", "peer@example.com");
        RunGit(_peerPath, "config", "user.name", "Peer User");
        RunGit(_peerPath, "checkout", "-b", "feature/45");
        File.WriteAllText(Path.Combine(_peerPath, "from-peer.txt"), "peer change");
        RunGit(_peerPath, "add", "-A");
        RunGit(_peerPath, "commit", "-m", "peer commit");
        RunGit(_peerPath, "push", "origin", "feature/45");

        await _gitService.FetchAsync("feature/45");

        var remoteTip = RunGitCapture(_localPath, "rev-parse", "origin/feature/45").Trim();
        Assert.NotEmpty(remoteTip);
        Assert.Equal("main", RunGitCapture(_localPath, "rev-parse", "--abbrev-ref", "HEAD").Trim());
    }

    [Fact]
    public async Task FetchAsyncOfUnknownBranchThrowsGitCommandException()
    {
        var exception = await Assert.ThrowsAsync<GitCommandException>(
            () => _gitService.FetchAsync("does-not-exist"));

        Assert.NotEmpty(exception.StandardError);
        Assert.NotEqual(0, exception.ExitCode);
    }

    [Fact]
    public async Task ResetHardAsyncDiscardsUncommittedAndUntrackedChanges()
    {
        File.WriteAllText(Path.Combine(_localPath, "initial.txt"), "modified content");
        File.WriteAllText(Path.Combine(_localPath, "untracked.txt"), "should be removed");

        await _gitService.ResetHardAsync("HEAD");

        Assert.Equal("initial", File.ReadAllText(Path.Combine(_localPath, "initial.txt")));
        Assert.False(File.Exists(Path.Combine(_localPath, "untracked.txt")));
    }

    [Fact]
    public async Task CreateBranchAsyncAndSwitchBranchAsyncCheckOutNewBranch()
    {
        await _gitService.CreateBranchAsync("feature/45");
        await _gitService.SwitchBranchAsync("feature/45");

        Assert.Equal("feature/45", RunGitCapture(_localPath, "rev-parse", "--abbrev-ref", "HEAD").Trim());
    }

    [Fact]
    public async Task BranchExistsAsyncReturnsTrueForExistingLocalBranch()
    {
        await _gitService.CreateBranchAsync("feature/45");

        Assert.True(await _gitService.BranchExistsAsync("feature/45"));
    }

    [Fact]
    public async Task BranchExistsAsyncReturnsTrueForRemoteOnlyBranch()
    {
        RunGit(_root, "clone", _originPath, _peerPath);
        RunGit(_peerPath, "config", "user.email", "peer@example.com");
        RunGit(_peerPath, "config", "user.name", "Peer User");
        RunGit(_peerPath, "checkout", "-b", "feature/45");
        File.WriteAllText(Path.Combine(_peerPath, "from-peer.txt"), "peer change");
        RunGit(_peerPath, "add", "-A");
        RunGit(_peerPath, "commit", "-m", "peer commit");
        RunGit(_peerPath, "push", "origin", "feature/45");

        Assert.True(await _gitService.BranchExistsAsync("feature/45"));
    }

    [Fact]
    public async Task BranchExistsAsyncReturnsFalseForUnusedBranchName()
    {
        Assert.False(await _gitService.BranchExistsAsync("feature/does-not-exist"));
    }

    [Fact]
    public async Task CreateBranchAsyncFailsInsteadOfOverwritingExistingLocalBranch()
    {
        await _gitService.CreateBranchAsync("feature/45");
        var originalTip = RunGitCapture(_localPath, "rev-parse", "feature/45").Trim();

        File.WriteAllText(Path.Combine(_localPath, "new-file.txt"), "content");
        RunGit(_localPath, "add", "-A");
        RunGit(_localPath, "commit", "-m", "advance HEAD past feature/45");

        var exception = await Assert.ThrowsAsync<GitCommandException>(
            () => _gitService.CreateBranchAsync("feature/45"));

        Assert.NotEmpty(exception.StandardError);
        Assert.NotEqual(0, exception.ExitCode);
        Assert.Equal(originalTip, RunGitCapture(_localPath, "rev-parse", "feature/45").Trim());
    }

    [Fact]
    public async Task CommitAsyncStagesAndCommitsPendingChanges()
    {
        File.WriteAllText(Path.Combine(_localPath, "new-file.txt"), "content");

        await _gitService.CommitAsync("adding specs for #45");

        var subject = RunGitCapture(_localPath, "log", "-1", "--pretty=%s").Trim();
        Assert.Equal("adding specs for #45", subject);
        Assert.Empty(RunGitCapture(_localPath, "status", "--porcelain").Trim());
    }

    [Fact]
    public async Task PushAsyncPublishesBranchToOrigin()
    {
        await _gitService.CreateBranchAsync("feature/45");
        await _gitService.SwitchBranchAsync("feature/45");
        File.WriteAllText(Path.Combine(_localPath, "new-file.txt"), "content");
        await _gitService.CommitAsync("adding specs for #45");

        await _gitService.PushAsync("feature/45");

        var branches = RunGitCapture(_originPath, "branch", "--list", "feature/45");
        Assert.Contains("feature/45", branches);
    }

    [Fact]
    public async Task FailingGitCommandThrowsGitCommandExceptionWithStandardError()
    {
        var exception = await Assert.ThrowsAsync<GitCommandException>(
            () => _gitService.ResetHardAsync("refs/heads/does-not-exist"));

        Assert.NotEmpty(exception.StandardError);
        Assert.NotEqual(0, exception.ExitCode);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; leftover temp directories don't fail the test run.
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        RunGitCapture(workingDirectory, arguments);
    }

    private static string RunGitCapture(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stdErr}");
        }

        return stdOut;
    }
}
