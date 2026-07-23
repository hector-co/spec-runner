using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Configuration;
using SpecRunner.GitHub;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class GitHubServiceTests
{
    private const string RepoUrl = "https://github.com/owner/repo";

    private static GitHubService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        SpecRunnerOptions? options = null)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(responder));
        return new GitHubService(httpClient, Options.Create(options ?? new SpecRunnerOptions { RepositoryUrl = RepoUrl, GitHubToken = "pat" }));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json) };

    [Fact]
    public async Task GetAuthenticatedLoginAsyncReturnsLoginFromUserEndpoint()
    {
        var service = CreateService(request =>
        {
            Assert.Equal("https://api.github.com/user", request.RequestUri!.ToString());
            return JsonResponse(HttpStatusCode.OK, """{"login":"spec-runner-bot"}""");
        });

        var login = await service.GetAuthenticatedLoginAsync();

        Assert.Equal("spec-runner-bot", login);
    }

    [Fact]
    public async Task GetAuthenticatedLoginAsyncCachesResultAcrossCalls()
    {
        var callCount = 0;
        var service = CreateService(_ =>
        {
            callCount++;
            return JsonResponse(HttpStatusCode.OK, """{"login":"spec-runner-bot"}""");
        });

        await service.GetAuthenticatedLoginAsync();
        await service.GetAuthenticatedLoginAsync();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ListOpenIssuesWithCommentsAsyncReturnsIssuesWithTheirComments()
    {
        var service = CreateService(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/repos/owner/repo/issues")
            {
                return JsonResponse(HttpStatusCode.OK, """
                    [
                      {"number":45,"title":"Add Login Page","body":"We need a login page."},
                      {"number":46,"title":"A PR, not an issue","body":"ignored","pull_request":{"url":"x"}}
                    ]
                    """);
            }

            if (path == "/repos/owner/repo/issues/45/comments")
            {
                return JsonResponse(HttpStatusCode.OK, """
                    [{"id":9001,"user":{"login":"someone"},"body":"/propose","created_at":"2026-01-01T00:00:00Z"}]
                    """);
            }

            throw new InvalidOperationException($"Unexpected request to {path}");
        });

        var issues = await service.ListOpenIssuesWithCommentsAsync();

        var issue = Assert.Single(issues);
        Assert.Equal(45, issue.Number);
        Assert.Equal("Add Login Page", issue.Title);
        Assert.Equal("We need a login page.", issue.Body);
        var comment = Assert.Single(issue.Comments);
        Assert.Equal(9001, comment.CommentId);
        Assert.Equal("someone", comment.Author);
        Assert.Equal("/propose", comment.Body);
    }

    [Fact]
    public async Task ListCommentReactionsAsyncReturnsReactionsWithAuthorAndType()
    {
        var service = CreateService(_ => JsonResponse(HttpStatusCode.OK, """
            [{"content":"eyes","user":{"login":"spec-runner-bot"}},{"content":"+1","user":{"login":"a-human"}}]
            """));

        var reactions = await service.ListCommentReactionsAsync(9001);

        Assert.Equal(2, reactions.Count);
        Assert.Contains(reactions, r => r.AuthorLogin == "spec-runner-bot" && r.ReactionType == "eyes");
        Assert.Contains(reactions, r => r.AuthorLogin == "a-human" && r.ReactionType == "+1");
    }

    [Fact]
    public async Task AddCommentReactionAsyncPostsToReactionsEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.Created, """{"id":1}""");
        });

        await service.AddCommentReactionAsync(9001, "eyes");

        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues/comments/9001/reactions", capturedRequest.RequestUri!.ToString());
        Assert.Contains("\"content\":\"eyes\"", capturedBody);
    }

    [Fact]
    public async Task CreateIssueCommentAsyncPostsBodyToIssueCommentsEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            return JsonResponse(HttpStatusCode.Created, """{"id":1}""");
        });

        await service.CreateIssueCommentAsync(45, "Created Draft PR #12 for this issue.");

        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues/45/comments", capturedRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task CreateDraftPullRequestAsyncPostsDraftTrueAndReturnsPrNumber()
    {
        string? capturedBody = null;
        var service = CreateService(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.Created, """{"number":12}""");
        });

        var prNumber = await service.CreateDraftPullRequestAsync("title", "body", "feature/45", "main");

        Assert.Equal(12, prNumber);
        using var document = JsonDocument.Parse(capturedBody!);
        Assert.True(document.RootElement.GetProperty("draft").GetBoolean());
        Assert.Equal("feature/45", document.RootElement.GetProperty("head").GetString());
        Assert.Equal("main", document.RootElement.GetProperty("base").GetString());
    }

    [Fact]
    public async Task ListOpenPullRequestsAsyncReturnsNumberTitleBodyAndHeadBranch()
    {
        var service = CreateService(request =>
        {
            Assert.Equal("/repos/owner/repo/pulls", request.RequestUri!.AbsolutePath);
            return JsonResponse(HttpStatusCode.OK, """
                [{"number":12,"title":"Add Login Page","body":"We need a login page.","head":{"ref":"feature/45"}}]
                """);
        });

        var pullRequests = await service.ListOpenPullRequestsAsync();

        var pr = Assert.Single(pullRequests);
        Assert.Equal(12, pr.Number);
        Assert.Equal("Add Login Page", pr.Title);
        Assert.Equal("We need a login page.", pr.Body);
        Assert.Equal("feature/45", pr.HeadBranch);
    }

    [Fact]
    public async Task ReadPrCommentsAsyncReturnsPrConversationComments()
    {
        var service = CreateService(request =>
        {
            Assert.Equal("/repos/owner/repo/issues/12/comments", request.RequestUri!.AbsolutePath);
            return JsonResponse(HttpStatusCode.OK, """
                [{"id":9001,"user":{"login":"someone"},"body":"/implement","created_at":"2026-01-01T00:00:00Z"}]
                """);
        });

        var comments = await service.ReadPrCommentsAsync(12);

        var comment = Assert.Single(comments);
        Assert.Equal(9001, comment.CommentId);
        Assert.Equal("someone", comment.Author);
        Assert.Equal("/implement", comment.Body);
    }

    [Fact]
    public async Task WritePrCommentAsyncPostsBodyToPrCommentsEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.Created, """{"id":1}""");
        });

        await service.WritePrCommentAsync(12, "Pushed changes for this comment.");

        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues/12/comments", capturedRequest.RequestUri!.ToString());
        Assert.Contains("\"body\":\"Pushed changes for this comment.\"", capturedBody);
    }

    [Fact]
    public async Task MarkPrReadyForReviewAsyncResolvesNodeIdAndCallsMutation()
    {
        HttpRequestMessage? mutationRequest = null;
        string? mutationBody = null;
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/repos/owner/repo/pulls/12", request.RequestUri!.AbsolutePath);
                return JsonResponse(HttpStatusCode.OK, """{"number":12,"node_id":"PR_kwABC"}""");
            }

            mutationRequest = request;
            mutationBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, """{"data":{"markPullRequestReadyForReview":{"pullRequest":{"id":"PR_kwABC"}}}}""");
        });

        await service.MarkPrReadyForReviewAsync(12);

        Assert.Equal("https://api.github.com/graphql", mutationRequest!.RequestUri!.ToString());
        Assert.Contains("\"pullRequestId\":\"PR_kwABC\"", mutationBody);
        Assert.Contains("markPullRequestReadyForReview", mutationBody);
    }

    [Fact]
    public async Task MarkPrReadyForReviewAsyncThrowsGitHubApiExceptionOnGraphQlErrorPayload()
    {
        var service = CreateService(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(HttpStatusCode.OK, """{"number":12,"node_id":"PR_kwABC"}""");
            }

            return JsonResponse(HttpStatusCode.OK, """{"errors":[{"message":"Pull request is not in draft state"}]}""");
        });

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.MarkPrReadyForReviewAsync(12));

        Assert.Contains("Pull request is not in draft state", exception.Message);
    }

    [Fact]
    public async Task UpdatePullRequestDescriptionAsyncPatchesPullRequestBody()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, """{"number":12}""");
        });

        await service.UpdatePullRequestDescriptionAsync(12, "new description");

        Assert.Equal(HttpMethod.Patch, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/pulls/12", capturedRequest.RequestUri!.ToString());
        Assert.Contains("\"body\":\"new description\"", capturedBody);
    }

    [Fact]
    public async Task UpdatePullRequestDescriptionAsyncThrowsGitHubApiExceptionOnFailure()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found")
        });

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.UpdatePullRequestDescriptionAsync(12, "new description"));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task UpdatePullRequestTitleAsyncPatchesPullRequestTitle()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, """{"number":12}""");
        });

        await service.UpdatePullRequestTitleAsync(12, "new title");

        Assert.Equal(HttpMethod.Patch, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/pulls/12", capturedRequest.RequestUri!.ToString());
        Assert.Contains("\"title\":\"new title\"", capturedBody);
    }

    [Fact]
    public async Task UpdatePullRequestTitleAsyncThrowsGitHubApiExceptionOnFailure()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found")
        });

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.UpdatePullRequestTitleAsync(12, "new title"));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task NonSuccessResponseThrowsGitHubApiExceptionWithStatusCode()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("bad credentials")
        });

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() => service.GetAuthenticatedLoginAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task TransportFailureThrowsGitHubApiException()
    {
        var httpClient = new HttpClient(FakeHttpMessageHandler.ThatThrows(new HttpRequestException("simulated DNS failure")));
        var service = new GitHubService(httpClient, Options.Create(new SpecRunnerOptions { RepositoryUrl = RepoUrl, GitHubToken = "pat" }));

        await Assert.ThrowsAsync<GitHubApiException>(() => service.GetAuthenticatedLoginAsync());
    }
}
