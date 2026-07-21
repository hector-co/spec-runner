using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.GitHub;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class HttpRepositoryConnectionTesterTests
{
    private const string Token = "super-secret-pat-value";

    private static HttpRepositoryConnectionTester CreateTester(
        SpecRunnerOptions options,
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null,
        Exception? throws = null)
    {
        var handler = throws is not null
            ? FakeHttpMessageHandler.ThatThrows(throws)
            : new FakeHttpMessageHandler(responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var httpClient = new HttpClient(handler);
        return new HttpRepositoryConnectionTester(
            httpClient,
            Options.Create(options),
            NullLogger<HttpRepositoryConnectionTester>.Instance);
    }

    [Fact]
    public async Task ReturnsNotConfiguredWhenRepositoryUrlIsEmpty()
    {
        var tester = CreateTester(new SpecRunnerOptions { RepositoryUrl = "", GitHubToken = Token });

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.NotConfigured, result.Status);
    }

    [Fact]
    public async Task ReturnsNotConfiguredWhenGitHubTokenIsEmpty()
    {
        var tester = CreateTester(new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = "" });

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.NotConfigured, result.Status);
    }

    [Fact]
    public async Task ReturnsInvalidRepositoryUrlForMalformedUrl()
    {
        var tester = CreateTester(new SpecRunnerOptions { RepositoryUrl = "git@github.com:owner/repo.git", GitHubToken = Token });

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.InvalidRepositoryUrl, result.Status);
    }

    [Fact]
    public async Task ReturnsConnectedOnHttp200()
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.Connected, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ReturnsAuthenticationFailedOn401Or403(HttpStatusCode statusCode)
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            _ => new HttpResponseMessage(statusCode));

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.AuthenticationFailed, result.Status);
    }

    [Fact]
    public async Task ReturnsRepositoryNotFoundOn404()
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.RepositoryNotFound, result.Status);
    }

    [Fact]
    public async Task ReturnsNetworkErrorOnTransportException()
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            throws: new HttpRequestException("simulated DNS failure"));

        var result = await tester.TestConnectionAsync();

        Assert.Equal(RepositoryConnectionStatus.NetworkError, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task MessageNeverContainsTheConfiguredToken(HttpStatusCode statusCode)
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            _ => new HttpResponseMessage(statusCode));

        var result = await tester.TestConnectionAsync();

        Assert.DoesNotContain(Token, result.Message);
    }

    [Fact]
    public async Task MessageNeverContainsTheConfiguredTokenOnNetworkError()
    {
        var tester = CreateTester(
            new SpecRunnerOptions { RepositoryUrl = "https://github.com/owner/repo", GitHubToken = Token },
            throws: new HttpRequestException("simulated DNS failure"));

        var result = await tester.TestConnectionAsync();

        Assert.DoesNotContain(Token, result.Message);
    }
}
