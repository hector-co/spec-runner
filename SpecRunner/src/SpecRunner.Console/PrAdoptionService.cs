using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Console;

public class PrAdoptionService : IPrAdoptionService
{
    private readonly IGitService _git;
    private readonly IGitHubService _gitHub;
    private readonly SpecRunnerOptions _options;

    public PrAdoptionService(IGitService git, IGitHubService gitHub, IOptions<SpecRunnerOptions> options)
    {
        _git = git;
        _gitHub = gitHub;
        _options = options.Value;
    }

    public async Task<PrAdoptionResult> TryAdoptAsync(int prNumber, string headBranch, CancellationToken cancellationToken = default)
    {
        await _git.FetchAsync(headBranch, cancellationToken).ConfigureAwait(false);

        var addedFolders = await _git.ListAddedSpecFolderNamesAsync(_options.BaseBranchName, headBranch, cancellationToken).ConfigureAwait(false);

        if (addedFolders.Count == 0)
        {
            return PrAdoptionResult.NoSpecFolderFound();
        }

        if (addedFolders.Count > 1)
        {
            return PrAdoptionResult.MultipleSpecFoldersFound(addedFolders);
        }

        var closingIssueNumbers = await _gitHub.ListClosingIssueNumbersAsync(prNumber, cancellationToken).ConfigureAwait(false);

        if (closingIssueNumbers.Count > 1)
        {
            return PrAdoptionResult.MultipleIssuesFound(closingIssueNumbers);
        }

        int? issueNumber = closingIssueNumbers.Count == 1 ? closingIssueNumbers[0] : null;
        var specName = addedFolders[0];

        var trackedIssue = new TrackedIssue(issueNumber, specName)
        {
            BranchName = headBranch,
            PrNumber = prNumber
        };

        return PrAdoptionResult.Success(trackedIssue);
    }
}
