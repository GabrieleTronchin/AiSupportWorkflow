using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Application.UseCases;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure;
using AiSupportWorkflow.Infrastructure.Agents;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.Infrastructure.WorkflowEngine;
using AiSupportWorkflow.Presentation;
using AiSupportWorkflow.Presentation.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to use string enum values
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Infrastructure services (Agent Framework, classifiers, resolvers, config)
builder.Services.AddInfrastructure(builder.Configuration);

// Persistence layer (EF Core InMemory, EfWorkflowStateTracker, WorkflowUpdateChannel)
builder.Services.AddPersistence();

// Inbox processor configuration and hosted service
builder.Services.Configure<InboxProcessorOptions>(options =>
{
    var section = builder.Configuration.GetSection("Workflow:InboxPollingIntervalSeconds");
    if (int.TryParse(section.Value, out var seconds))
        options.PollingIntervalSeconds = seconds;
});
builder.Services.AddHostedService<InboxProcessor>();

// gRPC services
builder.Services.AddGrpc();

// Application services
builder.Services.AddSingleton<IEmailProcessor, EmailProcessor>();
builder.Services.AddSingleton<ITeamRouter, TeamRouter>();
builder.Services.AddSingleton<IAgentSelector, AgentSelector>();
builder.Services.AddScoped<ProcessSupportEmailUseCase>();

// Workflow Engine (replaces old Orchestrator with WorkflowOrchestrator)
builder.Services.AddWorkflowEngine(builder.Configuration);

// Build IAIAgent instances from configuration
builder.Services.AddSingleton<IEnumerable<IAIAgent>>(sp =>
{
    var config = builder.Configuration
        .GetSection("Workflow")
        .Get<WorkflowConfiguration>() ?? new WorkflowConfiguration();

    var bugResolver = sp.GetRequiredService<IBugResolver>();

    return config.Teams
        .SelectMany(team => team.Agents.Select(agent =>
            (IAIAgent)new AiAgent(
                $"{team.TeamName}_{agent.Role}",
                team.TeamName,
                agent.Role,
                bugResolver)))
        .ToList();
});

// Application query services
builder.Services.AddScoped<AgentStatusService>();
builder.Services.AddScoped<InboxService>();
builder.Services.AddScoped<WorkflowQueryService>();

// Discover and register all IEndpoint implementations
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();

// gRPC-Web middleware (must be before endpoint mapping)
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// Map gRPC service
app.MapGrpcService<WorkflowMonitorService>();

// Map all Minimal API endpoints
app.MapEndpoints();

app.Run();
