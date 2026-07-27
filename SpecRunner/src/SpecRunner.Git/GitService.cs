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
        => RunGitAsync(cancellationToken, "branch", branchName);

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "checkout", branchName);

    public async Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default)
    {
        var localExitCode = await RunGitAllowingFailureAsync(cancellationToken, "show-ref", "--verify", "--quiet", $"refs/heads/{branchName}").ConfigureAwait(false);
        if (localExitCode == 0)
        {
            return true;
        }

        var remoteExitCode = await RunGitAllowingFailureAsync(cancellationToken, "ls-remote", "--exit-code", "--heads", "origin", branchName).ConfigureAwait(false);
        return remoteExitCode == 0;
    }

    public async Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        await RunGitAsync(cancellationToken, "add", "-A").ConfigureAwait(false);
        await RunGitAsync(cancellationToken, "commit", "-m", message).ConfigureAwait(false);
    }

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default)
        => RunGitAsync(cancellationToken, "push", "--set-upstream", "origin", branchName);

    public async Task<IReadOnlyList<string>> ListAddedSpecFolderNamesAsync(string baseBranch, string headBranch, CancellationToken cancellationToken = default)
    {
        var arguments = new[] { "diff", "--name-only", $"origin/{baseBranch}...origin/{headBranch}", "--", "openspec/changes" };
        var (exitCode, stdOut, stdErr) = await RunGitCoreAsync(cancellationToken, arguments).ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new GitCommandException($"git {string.Join(' ', arguments)}", exitCode, stdErr);
        }

        var folderNames = new List<string>();
        foreach (var line in stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = line.Trim().Split('/');
            if (segments.Length >= 3 && segments[0] == "openspec" && segments[1] == "changes")
            {
                var folderName = segments[2];
                if (!folderNames.Contains(folderName, StringComparer.Ordinal))
                {
                    folderNames.Add(folderName);
                }
            }
        }

        return folderNames;
    }

    private async Task RunGitAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var (exitCode, _, stdErr) = await RunGitCoreAsync(cancellationToken, arguments).ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new GitCommandException($"git {string.Join(' ', arguments)}", exitCode, stdErr);
        }
    }

    private async Task<int> RunGitAllowingFailureAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var (exitCode, _, _) = await RunGitCoreAsync(cancellationToken, arguments).ConfigureAwait(false);
        return exitCode;
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunGitCoreAsync(CancellationToken cancellationToken, params string[] arguments)
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
        var stdOut = await stdOutTask.ConfigureAwait(false);

        return (process.ExitCode, stdOut, stdErr);
    }
}
