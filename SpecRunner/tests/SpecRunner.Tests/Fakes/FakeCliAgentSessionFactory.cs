using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class FakeCliAgentSessionFactory : ICliAgentSessionFactory
{
    private readonly Func<ICliAgentSession> _factory;

    public FakeCliAgentSessionFactory(Func<ICliAgentSession> factory)
    {
        _factory = factory;
    }

    public List<ICliAgentSession> CreatedSessions { get; } = new();

    public ICliAgentSession CreateSession()
    {
        var session = _factory();
        CreatedSessions.Add(session);
        return session;
    }
}
