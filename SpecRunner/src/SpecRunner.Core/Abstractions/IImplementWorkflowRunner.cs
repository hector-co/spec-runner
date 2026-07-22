namespace SpecRunner.Core.Abstractions;

public interface IImplementWorkflowRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
