namespace AiSupportWorkflow.Application.Services;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.Interfaces;
using AiSupportWorkflow.Domain.ValueObjects;

public class EmailProcessor : IEmailProcessor
{
    public Result<IssueRecord> Process(IncomingEmail email)
    {
        if (string.IsNullOrWhiteSpace(email.Subject))
            return Result<IssueRecord>.Failure("Email subject cannot be empty or whitespace.");

        if (string.IsNullOrWhiteSpace(email.Body))
            return Result<IssueRecord>.Failure("Email body cannot be empty or whitespace.");

        var issue = new IssueRecord(
            Id: Guid.NewGuid(),
            Sender: email.Sender,
            Subject: email.Subject,
            Body: email.Body,
            CreatedAt: DateTimeOffset.UtcNow);

        return Result<IssueRecord>.Success(issue);
    }
}
