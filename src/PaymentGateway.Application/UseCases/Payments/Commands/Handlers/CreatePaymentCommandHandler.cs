using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.Services;
using PaymentGateway.Application.UseCases.Payments.Commands;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Domain.ValueObjects;

namespace PaymentGateway.Application.UseCases.Payments.Commands.Handlers;

public sealed class CreatePaymentCommandHandler : RequestHandlerAsync<CreatePaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDdsPublisher _ddsPublisher;

    public CreatePaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        IDdsPublisher ddsPublisher)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _ddsPublisher = ddsPublisher;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<CreatePaymentCommand> HandleAsync(
        CreatePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var merchant = await _unitOfWork.Merchants.GetByIdAsync(command.MerchantId, cancellationToken);
        if (merchant is null)
            throw new InvalidOperationException("Merchant not found");

        if (!merchant.CanProcessTransaction(command.Amount))
            throw new InvalidOperationException("Merchant cannot process this transaction amount");

        if (command.Customer is null)
            throw new InvalidOperationException("Customer is required");

        var customer = new CustomerInfo(
            command.Customer.Email,
            command.Customer.Name,
            command.Customer.Document ?? string.Empty,
            command.Customer.Ip,
            command.Customer.Phone);

        var payment = Payment.Create(
            command.MerchantId,
            new Money(command.Amount, command.Currency),
            ParsePaymentMethod(command.Method),
            customer,
            command.Description,
            command.Items?.Select(item => MapItem(item, command.Currency)).ToList(),
            command.Metadata,
            command.IdempotencyKey);

        await _paymentRepository.AddAsync(payment, cancellationToken);

        var now = DateTime.UtcNow;
        await _ddsPublisher.PublishAsync("payment.create", new
        {
            paymentId = payment.Id,
            merchantId = payment.MerchantId,
            amount = payment.Amount.Amount,
            currency = payment.Amount.Currency,
            method = payment.Method.ToString(),
            customer = new
            {
                email = payment.Customer.Email,
                name = payment.Customer.Name,
                document = payment.Customer.Document,
                ip = payment.Customer.Ip
            },
            timestamp = now
        }, cancellationToken);

        PaymentGatewayTelemetry.RecordPaymentLifecycle(
            "created",
            payment.Status.ToString().ToLowerInvariant(),
            payment.Method.ToString(),
            payment.MerchantId);

        command.Result = new CreatePaymentResult
        {
            PaymentId = payment.Id,
            Status = "pending",
            CreatedAt = now,
            ExpiresAt = payment.ExpiresAt
        };

        return await base.HandleAsync(command, cancellationToken);
    }

    private static PaymentItem MapItem(PaymentItemDto item, string currency)
    {
        return new PaymentItem(item.Sku, item.Name, item.Quantity, item.UnitPrice, currency);
    }

    private static PaymentMethod ParsePaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new InvalidOperationException("Payment method is required");

        var normalized = method
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (Enum.TryParse<PaymentMethod>(normalized, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Unsupported payment method '{method}'");
    }
}
