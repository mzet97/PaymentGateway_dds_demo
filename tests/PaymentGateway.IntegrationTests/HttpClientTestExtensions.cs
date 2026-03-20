using System.Net.Http.Headers;

namespace PaymentGateway.IntegrationTests;

internal static class HttpClientTestExtensions
{
    public static HttpClient WithTestIdentity(this HttpClient client, string role, Guid subjectId)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultRequestHeaders.Remove(IntegrationTestDefaults.RoleHeader);
        client.DefaultRequestHeaders.Remove(IntegrationTestDefaults.SubjectHeader);
        client.DefaultRequestHeaders.Add(IntegrationTestDefaults.RoleHeader, role);
        client.DefaultRequestHeaders.Add(IntegrationTestDefaults.SubjectHeader, subjectId.ToString());
        return client;
    }
}
