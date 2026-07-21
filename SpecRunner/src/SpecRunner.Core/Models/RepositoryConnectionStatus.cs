namespace SpecRunner.Core.Models;

public enum RepositoryConnectionStatus
{
    NotConfigured,
    InvalidRepositoryUrl,
    Connected,
    AuthenticationFailed,
    RepositoryNotFound,
    NetworkError
}
