namespace SpecRunner.Core.Abstractions;

public interface IProposeWorkflowRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
