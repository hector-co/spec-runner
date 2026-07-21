using System.Diagnostics;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Git;

public class GitService : IGitService
{
    private readonly SpecRunnerOptions _options;

    public GitService(IOptions<SpecRunnerOptions> options)
    {
        _options = options.Value;
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        await RunGitAsync(cancellationToken, "fetch", "origin", _options.BaseBranchName).ConfigureAwait(false);
        await RunGitAsync(cancellationToken, "checkout", _options.BaseBranchName).ConfigureAwait(false);
        await RunGitAsync(cancellationToken, "merge", "--ff-only", $"origin/{_options.BaseBranchName}").ConfigureAwait(false);
    }

    public Task FetchAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "fetch", "origin", branchName);

    public async Task ResetHardAsync(string targetRef, CancellationToken cancellationToken = default)
    {
        await RunGitAsync(cancellationToken, "reset", "--hard", targetRef).ConfigureAwait(false);
        await RunGitAsync(cancellationToken, "clean", "-fd").ConfigureAwait(false);
    }

    public Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "branch", "-f", branchName);

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "checkout", branchName);

    public async Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        await RunGitAsync(cancellationToken, "add", "-A").ConfigureAwait(false);
        await RunGitAsync(cancellationToken, "commit", "-m", message).ConfigureAwait(false);
    }

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "push", "--set-upstream", "origin", branchName);

    private async Task RunGitAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _options.LocalRepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdErr = await stdErrTask.ConfigureAwait(false);
        await stdOutTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new GitCommandException($"git {string.Join(' ', arguments)}", process.ExitCode, stdErr);
        }
    }
}
