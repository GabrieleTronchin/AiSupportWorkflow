using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Application.UseCases;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Infrastructure.Agents;
using AiSupportWorkflow.Presentation;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure services (Semantic Kernel, classifiers, resolvers, state tracker, config)
builder.Services.AddInfrastructure(builder.Configuration);

// Application services
builder.Services.AddSingleton<IEmailProcessor, EmailProcessor>();
builder.Services.AddSingleton<ITeamRouter, TeamRouter>();
builder.Services.AddSingleton<IAgentSelector, AgentSelector>();
builder.Services.AddSingleton<IOrchestrator, Orchestrator>();
builder.Services.AddSingleton<ProcessSupportEmailUseCase>();

// Build IAIAgent instances from configuration
builder.Services.AddSingleton<IEnumerable<IAIAgent>>(sp =>
{
    var config = builder.Configuration
        .GetSection("Workflow")
        .Get<WorkflowConfiguration>() ?? new WorkflowConfiguration();

    var bugResolver = sp.GetRequiredService<IBugResolver>();

    return config.Teams
        .SelectMany(team => team.Agents.Select(agent =>
            (IAIAgent)new SemanticKernelAgent(
                $"{team.TeamName}_{agent.Role}",
                team.TeamName,
                agent.Role,
                bugResolver)))
        .ToList();
});

// Configure Akka.NET actor system
builder.Services.AddAkka("SupportWorkflowSystem", (akkaBuilder, sp) =>
{
    akkaBuilder.WithActors((system, registry, resolver) =>
    {
        var agents = resolver.GetService<IEnumerable<IAIAgent>>()
            ?? Enumerable.Empty<IAIAgent>();

        var logger = resolver.GetService<ILogger<SupervisorActor>>()!;

        var supervisorProps = Props.Create(() => new SupervisorActor(agents, logger));
        var supervisor = system.ActorOf(supervisorProps, "supervisor");
        registry.Register<SupervisorActor>(supervisor);
    });
});

// Register the supervisor actor bridge for Application layer access
builder.Services.AddSingleton<ISupervisorActorBridge, SupervisorActorBridge>();

// Discover and register all IEndpoint implementations
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();

// Map all Minimal API endpoints
app.MapEndpoints();

app.Run();
