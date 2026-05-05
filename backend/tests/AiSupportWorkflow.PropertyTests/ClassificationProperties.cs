namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using AiSupportWorkflow.PropertyTests.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

public class ClassificationProperties
{
    private static readonly IssueCategory[] CodeRelatedCategories =
        [IssueCategory.BackendBug, IssueCategory.FrontendBug, IssueCategory.QualityTestIssue];

    // Feature: ai-support-workflow, Property 3: Classification result consistency
    // **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    [Property(MaxTest = 100, Arbitrary = [typeof(ClassificationGenerators)])]
    public Property ClassificationResult_IsConsistent(ClassificationResult classification)
    {
        var categoryConsistent = classification.IsCodeRelated
            ? CodeRelatedCategories.Contains(classification.Category)
            : classification.Category == IssueCategory.OutOfScope;

        var confidenceInRange = classification.ConfidenceScore >= 0.0
            && classification.ConfidenceScore <= 1.0;

        return (categoryConsistent && confidenceInRange).ToProperty();
    }
}
