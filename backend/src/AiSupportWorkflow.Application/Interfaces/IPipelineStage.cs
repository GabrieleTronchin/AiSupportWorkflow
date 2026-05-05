namespace AiSupportWorkflow.Application.Interfaces;

using AiSupportWorkflow.Domain.ValueObjects;

public interface IPipelineStage<TInput, TOutput>
{
    string StageName { get; }
    Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken ct = default);
}
