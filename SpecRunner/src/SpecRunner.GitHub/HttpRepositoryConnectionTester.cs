using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.GitHub;

public class HttpRepositoryConnectionTester : IRepositoryConnectionTester
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<SpecRunnerOptions> _options;
    private readonly ILogger<HttpRepositoryConnectionTester> _logger;

    public HttpRepositoryConnectionTester(HttpClient httpClient, IOptions<SpecRunnerOptions> options, ILogger<HttpRepositoryConnectionTester> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<RepositoryConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.RepositoryUrl) || string.IsNullOrWhiteSpace(options.GitHubToken))
        {
            return new RepositoryConnectionResult(
                RepositoryConnectionStatus.NotConfigured,
                "RepositoryUrl and GitHubToken must both be configured.");
        }

        if (!RepositoryUrlParser.TryParse(options.RepositoryUrl, out var owner, out var repo))
        {
            return new RepositoryConnectionResult(
                RepositoryConnectionStatus.InvalidRepositoryUrl,
                $"RepositoryUrl '{options.RepositoryUrl}' is not a valid https://github.com/{{owner}}/{{repo}} URL.");
        }

        _logger.LogInformation("Testing connection to {Owner}/{Repo}", owner, repo);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.GitHubToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SpecRunner", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error testing connection to {Owner}/{Repo}", owner, repo);
            return new RepositoryConnectionResult(
                RepositoryConnectionStatus.NetworkError,
                $"Network error contacting GitHub: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timed out testing connection to {Owner}/{Repo}", owner, repo);
            return new RepositoryConnectionResult(
                RepositoryConnectionStatus.NetworkError,
                "Network error contacting GitHub: request timed out.");
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new RepositoryConnectionResult(
                    RepositoryConnectionStatus.Connected,
                    $"Connected to {owner}/{repo}."),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new RepositoryConnectionResult(
                    RepositoryConnectionStatus.AuthenticationFailed,
                    $"GitHub rejected the configured token (HTTP {(int)response.StatusCode})."),
                HttpStatusCode.NotFound => new RepositoryConnectionResult(
                    RepositoryConnectionStatus.RepositoryNotFound,
                    $"Repository {owner}/{repo} was not found or is not accessible with the configured token."),
                _ => new RepositoryConnectionResult(
                    RepositoryConnectionStatus.NetworkError,
                    $"Unexpected response from GitHub (HTTP {(int)response.StatusCode}).")
            };
        }
    }
}
