using Akka.Actor;
using Akka.Hosting;
using AiSupportWorkflow.Application.Configuration;
using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Application.UseCases;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Infrastructure;
using AiSupportWorkflow.Infrastructure.Actors;
using AiSupportWorkflow.Infrastructure.Agents;
using AiSupportWorkflow.Infrastructure.Persistence;
using AiSupportWorkflow.Infrastructure.Services;
using AiSupportWorkflow.Presentation;
using AiSupportWorkflow.Presentation.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IEmailProcessor, EmailProcessor>();
builder.Services.AddScoped<ITeamRouter, TeamRouter>();
builder.Services.AddScoped<IAgentSelector, AgentSelector>();
builder.Services.AddScoped<IOrchestrator, Orchestrator>();
builder.Services.AddScoped<ProcessSupportEmailUseCase>();

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

// gRPC-Web middleware (must be before endpoint mapping)
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// Map gRPC service
app.MapGrpcService<WorkflowMonitorService>();

// Map all Minimal API endpoints
app.MapEndpoints();

app.Run();
