using Paramore.Brighter;
using PaymentGateway.Application.Common.Behaviours;
using PaymentGateway.Application.UseCases.Merchants.Commands;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;
using PaymentGateway.Domain.Repositories;

namespace PaymentGateway.Application.UseCases.Merchants.Commands.Handlers;

public sealed class UpdateMerchantCommandHandler : RequestHandlerAsync<UpdateMerchantCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMerchantCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [RequestLogging(0, HandlerTiming.Before)]
    [RequestValidation(1, HandlerTiming.Before)]
    public override async Task<UpdateMerchantCommand> HandleAsync(
        UpdateMerchantCommand command,
        CancellationToken cancellationToken = default)
    {
        var merchant = await _unitOfWork.Merchants.GetByIdAsync(command.MerchantId, cancellationToken);
        if (merchant is null)
        {
            throw new InvalidOperationException("Merchant not found");
        }

        merchant.UpdateProfile(command.Name, command.Email, command.Category, command.CallbackUrl);
        await _unitOfWork.Merchants.UpdateAsync(merchant, cancellationToken);

        command.Result = MerchantViewModelMapper.ToDto(merchant);
        return await base.HandleAsync(command, cancellationToken);
    }
}
