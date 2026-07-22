namespace SpecRunner.Core.Abstractions;

public interface IFinalizeWorkflowRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
