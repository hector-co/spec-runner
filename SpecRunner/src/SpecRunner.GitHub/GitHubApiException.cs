using System.Net;

namespace SpecRunner.GitHub;

public class GitHubApiException : Exception
{
    public GitHubApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
