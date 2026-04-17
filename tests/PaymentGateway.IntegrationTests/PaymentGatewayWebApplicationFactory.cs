using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Logging;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.DDS;

namespace PaymentGateway.IntegrationTests;

/// <summary>
/// Web application factory for integration testing.
/// </summary>
public class PaymentGatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    public PaymentGatewayWebApplicationFactory()
    {
        TestEnvironmentBootstrap.EnsureConfigured();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            ApplicationLogging.LoggerFactory = NullLoggerFactory.Instance;

            services.RemoveAll<TestMerchantRepository>();
            services.AddSingleton(new TestMerchantRepository());
            services.RemoveAll<TestWebhookRepository>();
            services.AddSingleton(new TestWebhookRepository());
            services.RemoveAll<IDdsPublisher>();
            services.AddSingleton<IDdsPublisher, InMemoryDdsPublisher>();
            services.RemoveAll<IMerchantRepository>();
            services.AddSingleton<IMerchantRepository>(sp => sp.GetRequiredService<TestMerchantRepository>());
            services.RemoveAll<IWebhookRepository>();
            services.AddSingleton<IWebhookRepository>(sp => sp.GetRequiredService<TestWebhookRepository>());
            services.RemoveAll<IUnitOfWork>();
            services.AddSingleton<IUnitOfWork>(sp => new TestUnitOfWork(sp.GetRequiredService<TestMerchantRepository>()));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
