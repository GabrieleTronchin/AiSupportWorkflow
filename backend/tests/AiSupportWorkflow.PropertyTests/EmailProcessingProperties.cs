namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Application.Services;
using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.PropertyTests.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

public class EmailProcessingProperties
{
    // Feature: ai-support-workflow, Property 1: Email processing round trip
    // **Validates: Requirements 1.1, 1.3**
    [Property(MaxTest = 100, Arbitrary = [typeof(EmailGenerators)])]
    public Property ValidEmail_ProcessedIssueRecord_PreservesFieldsAndHasUniqueId(IncomingEmail email)
    {
        var processor = new EmailProcessor();
        var result = processor.Process(email);

        return (result.IsSuccess
            && result.Value!.Sender == email.Sender
            && result.Value.Subject == email.Subject
            && result.Value.Body == email.Body
            && result.Value.Id != Guid.Empty)
            .ToProperty();
    }

    // Feature: ai-support-workflow, Property 1: Email processing round trip (unique IDs)
    [Property(MaxTest = 100, Arbitrary = [typeof(EmailGenerators)])]
    public Property ValidEmail_ProcessedTwice_ProducesDifferentIds(IncomingEmail email)
    {
        var processor = new EmailProcessor();
        var result1 = processor.Process(email);
        var result2 = processor.Process(email);

        return (result1.IsSuccess
            && result2.IsSuccess
            && result1.Value!.Id != result2.Value!.Id)
            .ToProperty();
    }

    // Feature: ai-support-workflow, Property 2: Invalid email rejection
    // **Validates: Requirements 1.2**
    [Property(MaxTest = 100, Arbitrary = [typeof(EmailGenerators)])]
    public Property InvalidEmail_IsRejected_ReturnsFailure(IncomingEmail email)
    {
        var isInvalid = string.IsNullOrWhiteSpace(email.Subject) || string.IsNullOrWhiteSpace(email.Body);

        var processor = new EmailProcessor();
        var result = processor.Process(email);

        return (!isInvalid || !result.IsSuccess).ToProperty();
    }
}
