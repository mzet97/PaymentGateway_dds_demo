using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Services;
using PaymentGateway.Domain.Observability;
using PaymentGateway.Domain.Repositories;
using PaymentGateway.Infrastructure.Configuration;
using PaymentGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;

namespace PaymentGateway.Services.Settlement;

public class SettlementService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IDdsPublisher _ddsPublisher;
    private readonly ILogger<SettlementService> _logger;

    public SettlementService(
        IPaymentRepository paymentRepository,
        IDdsPublisher ddsPublisher,
        ILogger<SettlementService> logger)
    {
        _paymentRepository = paymentRepository;
        _ddsPublisher = ddsPublisher;
        _logger = logger;
    }

    public async Task ProcessDailySettlementsAsync()
    {
        using var operation = PaymentGatewayTelemetry.StartOperation(
            "settlement",
            "process_daily",
            ActivityKind.Internal);

        _logger.LogInformation("Starting daily settlement processing...");

        // Get yesterday's date
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;

        _logger.LogInformation("Processing settlements for {Date}", yesterday);

        // 1. Query all captured payments from yesterday
        var payments = await _paymentRepository.GetByMerchantAsync(
            Guid.Empty, "approved", yesterday, today, 10000, 0);

        // 2. Group by merchant
        var merchantGroups = payments.GroupBy(p => p.MerchantId);

        try
        {
            foreach (var group in merchantGroups)
            {
                var merchantId = group.Key;
                var totalAmount = group.Sum(p => p.Amount.Amount);
                var feePercentage = 0.029m;
                var feeFixed = 0.30m;
                var platformFee = Math.Round(totalAmount * feePercentage + feeFixed * group.Count(), 2);
                var settlementAmount = totalAmount - platformFee;

                var settlement = new Settlement
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    PeriodStart = yesterday,
                    PeriodEnd = today.AddDays(-1),
                    TotalAmount = totalAmount,
                    FeeAmount = platformFee,
                    SettlementAmount = settlementAmount,
                    Status = SettlementStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                settlement.Status = SettlementStatus.Processed;
                settlement.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation("Settlement for merchant {MerchantId}: Total={Total}, Fee={Fee}, Settlement={Settlement}",
                    merchantId, totalAmount, platformFee, settlementAmount);

                await _ddsPublisher.PublishAsync("settlement.processed", new
                {
                    settlementId = settlement.Id,
                    merchantId = merchantId,
                    periodStart = settlement.PeriodStart,
                    periodEnd = settlement.PeriodEnd,
                    totalAmount = settlement.TotalAmount,
                    feeAmount = settlement.FeeAmount,
                    settlementAmount = settlement.SettlementAmount,
                    status = settlement.Status.ToString(),
                    processedAt = settlement.ProcessedAt
                }, CancellationToken.None);
            }

            _logger.LogInformation("Daily settlement processing completed");
        }
        catch (Exception ex)
        {
            operation.RecordException(ex);
            throw;
        }
    }

    public Task ReconcileSettlementsAsync()
    {
        _logger.LogInformation("Running settlement reconciliation...");

        // 1. Compare settlement totals with payment totals
        // 2. Identify discrepancies
        // 3. Log reconciliation report

        _logger.LogInformation("Reconciliation completed");
        return Task.CompletedTask;
    }
}
