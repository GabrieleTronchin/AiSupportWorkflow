namespace AiSupportWorkflow.Presentation.Endpoints;

using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Presentation.Endpoints.Primitives;
using Microsoft.Extensions.Options;

public class ConfigEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Config");

        group.MapGet("/config", (IOptions<WorkflowConfiguration> config) =>
        {
            return Results.Ok(new
            {
                sequentialProcessing = config.Value.SequentialProcessing,
            });
        }).WithSummary("Get runtime configuration flags for the dashboard");
    }
}
