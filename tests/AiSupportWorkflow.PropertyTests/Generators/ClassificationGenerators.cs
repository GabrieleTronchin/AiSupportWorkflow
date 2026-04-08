namespace AiSupportWorkflow.PropertyTests.Generators;

using AiSupportWorkflow.Domain.Enums;
using AiSupportWorkflow.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;

public static class ClassificationGenerators
{
    private static readonly IssueCategory[] CodeRelatedCategories =
        [IssueCategory.BackendBug, IssueCategory.FrontendBug, IssueCategory.QualityTestIssue];

    public static Gen<IssueCategory> CodeRelatedCategoryGen =>
        Gen.Elements(CodeRelatedCategories);

    public static Arbitrary<IssueCategory> CodeRelatedCategoryArb() =>
        Arb.From(CodeRelatedCategoryGen);

    public static Arbitrary<ClassificationResult> ValidClassificationArb() =>
        Arb.From(
            from isCodeRelated in ArbMap.Default.GeneratorFor<bool>()
            from codeCategory in CodeRelatedCategoryGen
            from confidence in Gen.Choose(0, 100).Select(n => n / 100.0)
            from reasoning in ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get)
            let category = isCodeRelated ? codeCategory : IssueCategory.OutOfScope
            select new ClassificationResult(isCodeRelated, category, confidence, reasoning));
}
