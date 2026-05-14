namespace AiSupportWorkflow.Infrastructure.AgentFramework;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

internal static class ChatClientAgentFactory
{
    public static ChatClientAgent CreateClassificationAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions
        {
            Name = "ClassificationAgent",
            Description = "Classifies support emails into issue categories",
            ChatOptions = new ChatOptions
            {
                Temperature = 0.1f,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<ClassificationResult>()
            }
        });

    public static ChatClientAgent CreateResolutionAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions
        {
            Name = "ResolutionAgent",
            Description = "Performs root cause analysis and produces resolution reports",
            ChatOptions = new ChatOptions
            {
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<ResolutionReport>()
            }
        });

    public static ChatClientAgent CreateCodeGenAgent(IChatClient chatClient) =>
        new(chatClient, new ChatClientAgentOptions
        {
            Name = "CodeGenAgent",
            Description = "Generates code changes as pull requests from resolution reports",
            ChatOptions = new ChatOptions
            {
                Temperature = 0.5f,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<PullRequest>()
            }
        });
}
