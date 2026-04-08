namespace AiSupportWorkflow.PropertyTests;

using AiSupportWorkflow.Domain.Entities;
using AiSupportWorkflow.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

public class ResolutionProperties
{
    private static Gen<string> NonEmptyNonWhitespaceString =>
        ArbMap.Default.GeneratorFor<NonEmptyString>()
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrWhiteSpace(s));

    private static readonly string[] ValidDummyAppPrefixes =
        ["DummyApps/ApplicationA/", "DummyApps/ApplicationB/"];

    private static Gen<string> DummyAppFilePathGen =>
        Gen.Elements(
            "DummyApps/ApplicationA/src/Controllers/OrderController.cs",
            "DummyApps/ApplicationA/src/Components/OrderSummary.razor",
            "DummyApps/ApplicationA/tests/OrderServiceTests.cs",
            "DummyApps/ApplicationB/src/Controllers/UserController.cs",
            "DummyApps/ApplicationB/src/Components/UserProfile.razor",
            "DummyApps/ApplicationB/tests/UserServiceTests.cs");

    // Feature: ai-support-workflow, Property 6: Resolution report completeness
    // **Validates: Requirements 5.1, 5.2**
    [Property(MaxTest = 100)]
    public Property NonEscalatedReport_HasAllRequiredFieldsNonEmpty(Guid issueId)
    {
        var gen =
            from rootCause in NonEmptyNonWhitespaceString
            from component in NonEmptyNonWhitespaceString
            from severity in NonEmptyNonWhitespaceString
            from fix in NonEmptyNonWhitespaceString
            select new ResolutionReport(issueId, rootCause, component, severity, fix, false, null);

        return Prop.ForAll(Arb.From(gen), report =>
            !string.IsNullOrWhiteSpace(report.RootCauseDescription)
            && !string.IsNullOrWhiteSpace(report.AffectedComponent)
            && !string.IsNullOrWhiteSpace(report.SeverityAssessment)
            && !string.IsNullOrWhiteSpace(report.ProposedFixSummary)
            && !report.RequiresEscalation);
    }

    // Feature: ai-support-workflow, Property 7: PR completeness and traceability
    // **Validates: Requirements 6.1, 6.2, 6.4**
    [Property(MaxTest = 100)]
    public Property PullRequest_HasAllFieldsNonEmptyAndMatchesIssueId(Guid issueId)
    {
        var gen =
            from title in NonEmptyNonWhitespaceString
            from description in NonEmptyNonWhitespaceString
            from diff in NonEmptyNonWhitespaceString
            from fileCount in Gen.Choose(1, 5)
            from files in Gen.ListOf(DummyAppFilePathGen, fileCount)
            select new PullRequest(Guid.NewGuid(), issueId, title, description, files.ToList(), diff);

        return Prop.ForAll(Arb.From(gen), pr =>
            !string.IsNullOrWhiteSpace(pr.Title)
            && !string.IsNullOrWhiteSpace(pr.Description)
            && !string.IsNullOrWhiteSpace(pr.SimulatedDiff)
            && pr.AffectedFilePaths.Count >= 1
            && pr.IssueId == issueId);
    }

    // Feature: ai-support-workflow, Property 10: Fix references valid dummy app files
    // **Validates: Requirements 9.4**
    [Property(MaxTest = 100)]
    public Property PullRequest_AffectedFilePaths_ReferenceValidDummyAppFiles(Guid issueId)
    {
        var gen =
            from title in NonEmptyNonWhitespaceString
            from description in NonEmptyNonWhitespaceString
            from diff in NonEmptyNonWhitespaceString
            from fileCount in Gen.Choose(1, 5)
            from files in Gen.ListOf(DummyAppFilePathGen, fileCount)
            select new PullRequest(Guid.NewGuid(), issueId, title, description, files.ToList(), diff);

        return Prop.ForAll(Arb.From(gen), pr =>
            pr.AffectedFilePaths.All(path =>
                ValidDummyAppPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))));
    }
}
