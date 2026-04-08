namespace AiSupportWorkflow.Domain.Interfaces;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;

public interface IEmailProcessor
{
    Result<IssueRecord> Process(IncomingEmail email);
}
