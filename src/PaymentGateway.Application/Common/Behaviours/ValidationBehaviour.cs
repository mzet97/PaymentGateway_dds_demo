using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;

namespace PaymentGateway.Application.Common.Behaviours;

public sealed class RequestValidationAttribute : RequestHandlerAttribute
{
    public RequestValidationAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
        : base(step, timing)
    {
    }

    public override Type GetHandlerType()
    {
        return typeof(ValidationHandler<>);
    }
}

public sealed class ValidationHandler<TRequest> : RequestHandlerAsync<TRequest>
    where TRequest : class, IRequest
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
    {
        var validators = _serviceProvider.GetServices<IValidator<TRequest>>();
        if (!validators.Any())
        {
            return await base.HandleAsync(command, cancellationToken);
        }

        var context = new ValidationContext<TRequest>(command);
        var results = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await base.HandleAsync(command, cancellationToken);
    }
}
