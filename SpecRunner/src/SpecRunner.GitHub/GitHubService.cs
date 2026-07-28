using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.GitHub;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly SpecRunnerOptions _options;
    private readonly SemaphoreSlim _loginGate = new(1, 1);
    private string? _cachedLogin;

    public GitHubService(HttpClient httpClient, IOptions<SpecRunnerOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLogin is not null)
        {
            return _cachedLogin;
        }

        await _loginGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedLogin is not null)
            {
                return _cachedLogin;
            }

            using var response = await SendAsync(HttpMethod.Get, "https://api.github.com/user", null, cancellationToken).ConfigureAwait(false);
            var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);
            _cachedLogin = document.RootElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString()
                : null;

            if (_cachedLogin is null)
            {
                throw new GitHubApiException("GitHub /user response did not include a login.");
            }

            return _cachedLogin;
        }
        finally
        {
            _loginGate.Release();
        }
    }

    public async Task<IReadOnlyList<GitHubIssue>> ListOpenIssuesWithCommentsAsync(CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var issuesResponse = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/issues?state=open&per_page=100", null, cancellationToken).ConfigureAwait(false);
        var issuesDocument = await ParseJsonAsync(issuesResponse, cancellationToken).ConfigureAwait(false);

        var issues = new List<GitHubIssue>();
        foreach (var issueElement in issuesDocument.RootElement.EnumerateArray())
        {
            if (issueElement.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            var number = issueElement.GetProperty("number").GetInt32();
            var title = issueElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty;
            var body = issueElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;

            var comments = await ListIssueCommentsAsync(owner, repo, number, cancellationToken).ConfigureAwait(false);
            issues.Add(new GitHubIssue(number, title, body, comments));
        }

        return issues;
    }

    public async Task<IReadOnlyList<GitHubReaction>> ListCommentReactionsAsync(long commentId, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/issues/comments/{commentId}/reactions?per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var reactions = new List<GitHubReaction>();
        foreach (var reactionElement in document.RootElement.EnumerateArray())
        {
            var content = reactionElement.TryGetProperty("content", out var contentElement) ? contentElement.GetString() ?? string.Empty : string.Empty;
            var login = reactionElement.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString() ?? string.Empty
                : string.Empty;

            reactions.Add(new GitHubReaction(login, content));
        }

        return reactions;
    }

    public async Task AddCommentReactionAsync(long commentId, string reactionType, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/issues/comments/{commentId}/reactions",
            new { content = reactionType },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateIssueCommentAsync(int issueNumber, string body, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}/comments",
            new { body },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public async Task<int> CreateDraftPullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/pulls",
            new { title, body, head = headBranch, @base = baseBranch, draft = true },
            cancellationToken).ConfigureAwait(false);

        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("number").GetInt32();
    }

    public async Task<IReadOnlyList<GitHubPullRequest>> ListOpenPullRequestsAsync(CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/pulls?state=open&per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var pullRequests = new List<GitHubPullRequest>();
        foreach (var prElement in document.RootElement.EnumerateArray())
        {
            var number = prElement.GetProperty("number").GetInt32();
            var title = prElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty;
            var body = prElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;
            var headBranch = prElement.TryGetProperty("head", out var headElement) && headElement.TryGetProperty("ref", out var refElement)
                ? refElement.GetString() ?? string.Empty
                : string.Empty;

            pullRequests.Add(new GitHubPullRequest(number, title, body, headBranch));
        }

        return pullRequests;
    }

    public async Task<IReadOnlyList<PrComment>> ReadPrCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/issues/{prNumber}/comments?per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var comments = new List<PrComment>();
        foreach (var commentElement in document.RootElement.EnumerateArray())
        {
            var commentId = commentElement.GetProperty("id").GetInt64();
            var author = commentElement.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString() ?? string.Empty
                : string.Empty;
            var authorAssociation = commentElement.TryGetProperty("author_association", out var authorAssociationElement) && authorAssociationElement.ValueKind == JsonValueKind.String
                ? authorAssociationElement.GetString() ?? "NONE"
                : "NONE";
            var body = commentElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;
            var createdAt = commentElement.TryGetProperty("created_at", out var createdAtElement)
                ? createdAtElement.GetDateTimeOffset()
                : DateTimeOffset.UtcNow;

            comments.Add(new PrComment(commentId, author, authorAssociation, body, createdAt));
        }

        return comments;
    }

    public async Task WritePrCommentAsync(int prNumber, string body, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/issues/{prNumber}/comments",
            new { body },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrReviewComment>> ListPrReviewCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/comments?per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var comments = new List<PrReviewComment>();
        foreach (var commentElement in document.RootElement.EnumerateArray())
        {
            var commentId = commentElement.GetProperty("id").GetInt64();
            var path = commentElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.String
                ? pathElement.GetString() ?? string.Empty
                : string.Empty;
            var author = commentElement.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString() ?? string.Empty
                : string.Empty;
            var authorAssociation = commentElement.TryGetProperty("author_association", out var authorAssociationElement) && authorAssociationElement.ValueKind == JsonValueKind.String
                ? authorAssociationElement.GetString() ?? "NONE"
                : "NONE";
            var body = commentElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;
            var createdAt = commentElement.TryGetProperty("created_at", out var createdAtElement)
                ? createdAtElement.GetDateTimeOffset()
                : DateTimeOffset.UtcNow;

            comments.Add(new PrReviewComment(commentId, path, author, authorAssociation, body, createdAt));
        }

        return comments;
    }

    public async Task<IReadOnlyList<GitHubReaction>> ListReviewCommentReactionsAsync(long commentId, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/pulls/comments/{commentId}/reactions?per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var reactions = new List<GitHubReaction>();
        foreach (var reactionElement in document.RootElement.EnumerateArray())
        {
            var content = reactionElement.TryGetProperty("content", out var contentElement) ? contentElement.GetString() ?? string.Empty : string.Empty;
            var login = reactionElement.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString() ?? string.Empty
                : string.Empty;

            reactions.Add(new GitHubReaction(login, content));
        }

        return reactions;
    }

    public async Task AddReviewCommentReactionAsync(long commentId, string reactionType, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/pulls/comments/{commentId}/reactions",
            new { content = reactionType },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplyToReviewCommentAsync(int prNumber, long commentId, string body, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/comments",
            new { body, in_reply_to = commentId },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkPrReadyForReviewAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var prResponse = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}", null, cancellationToken).ConfigureAwait(false);
        var prDocument = await ParseJsonAsync(prResponse, cancellationToken).ConfigureAwait(false);
        var nodeId = prDocument.RootElement.TryGetProperty("node_id", out var nodeIdElement) ? nodeIdElement.GetString() : null;

        if (string.IsNullOrEmpty(nodeId))
        {
            throw new GitHubApiException($"GitHub pull request #{prNumber} response did not include a node_id.");
        }

        var mutation = new
        {
            query = "mutation($pullRequestId: ID!) { markPullRequestReadyForReview(input: { pullRequestId: $pullRequestId }) { pullRequest { id } } }",
            variables = new { pullRequestId = nodeId }
        };

        using var response = await SendAsync(HttpMethod.Post, "https://api.github.com/graphql", mutation, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("errors", out var errorsElement)
            && errorsElement.ValueKind == JsonValueKind.Array
            && errorsElement.GetArrayLength() > 0)
        {
            var message = errorsElement[0].TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "unknown GraphQL error";
            throw new GitHubApiException($"GitHub GraphQL mutation markPullRequestReadyForReview failed for PR #{prNumber}: {message}");
        }
    }

    public async Task UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Patch,
            $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}",
            new { body },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePullRequestTitleAsync(int prNumber, string title, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        using var response = await SendAsync(
            HttpMethod.Patch,
            $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}",
            new { title },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> ListClosingIssueNumbersAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        var (owner, repo) = GetOwnerRepo();

        var query = new
        {
            query = "query($owner: String!, $name: String!, $number: Int!) { repository(owner: $owner, name: $name) { pullRequest(number: $number) { closingIssuesReferences(first: 100) { nodes { number } } } } }",
            variables = new { owner, name = repo, number = prNumber }
        };

        using var response = await SendAsync(HttpMethod.Post, "https://api.github.com/graphql", query, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("errors", out var errorsElement)
            && errorsElement.ValueKind == JsonValueKind.Array
            && errorsElement.GetArrayLength() > 0)
        {
            var message = errorsElement[0].TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "unknown GraphQL error";
            throw new GitHubApiException($"GitHub GraphQL query closingIssuesReferences failed for PR #{prNumber}: {message}");
        }

        var issueNumbers = new List<int>();
        if (document.RootElement.TryGetProperty("data", out var dataElement)
            && dataElement.TryGetProperty("repository", out var repositoryElement)
            && repositoryElement.ValueKind == JsonValueKind.Object
            && repositoryElement.TryGetProperty("pullRequest", out var pullRequestElement)
            && pullRequestElement.ValueKind == JsonValueKind.Object
            && pullRequestElement.TryGetProperty("closingIssuesReferences", out var closingIssuesElement)
            && closingIssuesElement.TryGetProperty("nodes", out var nodesElement))
        {
            foreach (var node in nodesElement.EnumerateArray())
            {
                issueNumbers.Add(node.GetProperty("number").GetInt32());
            }
        }

        return issueNumbers;
    }

    private async Task<IReadOnlyList<GitHubIssueComment>> ListIssueCommentsAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}/comments?per_page=100", null, cancellationToken).ConfigureAwait(false);
        var document = await ParseJsonAsync(response, cancellationToken).ConfigureAwait(false);

        var comments = new List<GitHubIssueComment>();
        foreach (var commentElement in document.RootElement.EnumerateArray())
        {
            var commentId = commentElement.GetProperty("id").GetInt64();
            var author = commentElement.TryGetProperty("user", out var userElement) && userElement.TryGetProperty("login", out var loginElement)
                ? loginElement.GetString() ?? string.Empty
                : string.Empty;
            var authorAssociation = commentElement.TryGetProperty("author_association", out var authorAssociationElement) && authorAssociationElement.ValueKind == JsonValueKind.String
                ? authorAssociationElement.GetString() ?? "NONE"
                : "NONE";
            var body = commentElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString() ?? string.Empty
                : string.Empty;
            var createdAt = commentElement.TryGetProperty("created_at", out var createdAtElement)
                ? createdAtElement.GetDateTimeOffset()
                : DateTimeOffset.UtcNow;

            comments.Add(new GitHubIssueComment(commentId, author, authorAssociation, body, createdAt));
        }

        return comments;
    }

    private (string Owner, string Repo) GetOwnerRepo()
    {
        if (!RepositoryUrlParser.TryParse(_options.RepositoryUrl, out var owner, out var repo))
        {
            throw new GitHubApiException($"RepositoryUrl '{_options.RepositoryUrl}' is not a valid https://github.com/{{owner}}/{{repo}} URL.");
        }

        return (owner, repo);
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? jsonBody, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SpecRunner", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (jsonBody is not null)
        {
            var json = JsonSerializer.Serialize(jsonBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubApiException($"Network error calling GitHub API ({method} {url}): {ex.Message}", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubApiException($"GitHub API request to {url} timed out.", null, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            throw new GitHubApiException($"GitHub API call {method} {url} failed with HTTP {(int)statusCode}: {responseBody}", statusCode);
        }

        return response;
    }
}
