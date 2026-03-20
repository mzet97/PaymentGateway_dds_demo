using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.UseCases.Merchants.Commands;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Merchants.Commands.Handlers;

public sealed class CreateMerchantCommandHandler : RequestHandlerAsync<CreateMerchantCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateMerchantCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<CreateMerchantCommand> HandleAsync(
        CreateMerchantCommand command,
        CancellationToken cancellationToken = default)
    {
        var merchant = Merchant.Create(
            command.Name,
            command.Email,
            command.Document,
            command.Category,
            command.CallbackUrl);

        await _unitOfWork.Merchants.AddAsync(merchant, cancellationToken);

        command.Result = new CreateMerchantResult
        {
            MerchantId = merchant.Id,
            ApiKey = merchant.ApiKey ?? string.Empty,
            Status = merchant.Status
        };

        return await base.HandleAsync(command, cancellationToken);
    }
}
