namespace PaymentGateway.IntegrationTests;

internal static class IntegrationTestDefaults
{
    public static readonly Guid DefaultMerchantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid SecondaryMerchantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid AdminSubjectId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public const string RoleHeader = "X-Test-Role";
    public const string SubjectHeader = "X-Test-SubjectId";
}
