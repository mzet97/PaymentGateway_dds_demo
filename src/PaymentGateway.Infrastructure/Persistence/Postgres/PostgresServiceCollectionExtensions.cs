using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Persistence.Postgres.Repositories;

namespace PaymentGateway.Infrastructure.Persistence.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayPostgresPersistence(
        this IServiceCollection services,
        string postgresConnection,
        bool useForPaymentQueries = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnection);

        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // CQRS read-side: use PostgreSQL for payment queries instead of MongoDB
        if (useForPaymentQueries)
        {
            services.AddScoped<IPaymentRepository, PostgresPaymentRepository>();
        }

        return services;
    }
}
