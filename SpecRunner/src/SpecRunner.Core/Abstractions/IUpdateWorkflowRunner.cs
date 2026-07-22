namespace SpecRunner.Core.Abstractions;

public interface IUpdateWorkflowRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
