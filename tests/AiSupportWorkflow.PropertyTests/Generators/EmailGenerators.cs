namespace AiSupportWorkflow.PropertyTests.Generators;

using AiSupportWorkflow.Domain.Entities;
using FsCheck;
using FsCheck.Fluent;

public static class EmailGenerators
{
    private static Gen<string> NonEmptyNonWhitespaceString =>
        ArbMap.Default.GeneratorFor<NonEmptyString>()
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrWhiteSpace(s));

    public static Arbitrary<IncomingEmail> ValidEmailArb() =>
        Arb.From(
            from sender in NonEmptyNonWhitespaceString
            from subject in NonEmptyNonWhitespaceString
            from body in NonEmptyNonWhitespaceString
            select new IncomingEmail(sender, subject, body));

    public static Arbitrary<IncomingEmail> InvalidEmailArb() =>
        Arb.From(
            from sender in NonEmptyNonWhitespaceString
            from validSubject in NonEmptyNonWhitespaceString
            from validBody in NonEmptyNonWhitespaceString
            from makeSubjectInvalid in ArbMap.Default.GeneratorFor<bool>()
            let subject = makeSubjectInvalid ? "" : validSubject
            let body = makeSubjectInvalid ? validBody : ""
            select new IncomingEmail(sender, subject, body));
}
