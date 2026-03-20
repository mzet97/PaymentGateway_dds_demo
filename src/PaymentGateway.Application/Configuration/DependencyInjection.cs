using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Darker;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Merchants.Queries;
using PaymentGateway.Application.UseCases.Merchants.Queries.Handlers;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;
using PaymentGateway.Application.UseCases.Payments.Queries;
using PaymentGateway.Application.UseCases.Payments.Queries.Handlers;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Application.UseCases.Statistics.Queries;
using PaymentGateway.Application.UseCases.Statistics.Queries.Handlers;
using PaymentGateway.Application.UseCases.Statistics.ViewModels;
using PaymentGateway.Application.UseCases.Webhooks.Queries;
using PaymentGateway.Application.UseCases.Webhooks.Queries.Handlers;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;

namespace PaymentGateway.Application.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentGatewayApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var currentAssembly = Assembly.GetAssembly(typeof(DependencyInjection));
        if (currentAssembly is null)
        {
            throw new InvalidOperationException("Unable to resolve PaymentGateway.Application assembly.");
        }

        BrighterLoggingBootstrap.EnsureInitialized();
        services.AddBrighter(options => { options.HandlerLifetime = ServiceLifetime.Scoped; })
            .AutoFromAssemblies([currentAssembly]);

        services.AddValidatorsFromAssembly(currentAssembly);
        services.AddTransient(typeof(ValidationHandler<>));
        services.AddTransient(typeof(UnhandledExceptionHandler<>));

        services.AddScoped<IQueryProcessor, ServiceProviderQueryProcessor>();
        services.AddScoped<IQueryHandler<GetMerchantByIdQuery, MerchantDto?>, GetMerchantByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetPaymentByIdQuery, PaymentDto?>, GetPaymentByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetPaymentsQuery, PaymentsListResult>, GetPaymentsQueryHandler>();
        services.AddScoped<IQueryHandler<GetStatisticsQuery, StatisticsDto>, GetStatisticsQueryHandler>();
        services.AddScoped<IQueryHandler<GetWebhookByIdQuery, WebhookDto?>, GetWebhookByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetWebhooksQuery, List<WebhookDto>>, GetWebhooksQueryHandler>();

        return services;
    }
}
