using System.Security.Claims;
using FluentValidation;
using Paramore.Brighter;
using Paramore.Darker;
using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Payments.Commands;
using PaymentGateway.Application.UseCases.Payments.Queries;
using PaymentGateway.Application.UseCases.Payments.ViewModels;
using PaymentGateway.Application.Services;

namespace PaymentGateway.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static WebApplication MapPaymentsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/payments/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            CancellationToken ct) =>
        {
            var result = await queryProcessor.ExecuteAsync(new GetPaymentByIdQuery(id), ct);
            if (result == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, result.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            return Results.Ok(result);
        })
        .WithTags("Payments")
        .WithName("GetPayment")
        .RequireAuthorization();

        app.MapGet("/api/v1/payments", async (
            string? merchantId,
            string? status,
            DateTime? from,
            DateTime? to,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            CancellationToken ct,
            int limit = 50,
            int offset = 0) =>
        {
            Guid? requestedMerchantId = null;
            if (!string.IsNullOrWhiteSpace(merchantId))
            {
                if (!Guid.TryParse(merchantId, out var parsedMerchantId) || parsedMerchantId == Guid.Empty)
                    return Results.BadRequest(new { error = "Invalid merchantId" });

                requestedMerchantId = parsedMerchantId;
            }

            var scope = merchantScopeService.Resolve(user, requestedMerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            var query = new GetPaymentsQuery
            {
                MerchantId = scope.MerchantId,
                Status = status,
                From = from,
                To = to,
                Limit = limit,
                Offset = offset
            };

            var result = await queryProcessor.ExecuteAsync(query, ct);
            return Results.Ok(result);
        })
        .WithTags("Payments")
        .WithName("GetPayments")
        .RequireAuthorization();

        app.MapPost("/api/v1/payments", async (
            CreatePaymentCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var requestedMerchantId = command.MerchantId == Guid.Empty ? (Guid?)null : command.MerchantId;
            var scope = merchantScopeService.Resolve(user, requestedMerchantId, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            if (!scope.MerchantId.HasValue || scope.MerchantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Merchant identifier is required" });

            command.MerchantId = scope.MerchantId.Value;

            try
            {
                var result = await commandProcessor.SendWithResultAsync<CreatePaymentCommand, CreatePaymentResult>(command, ct);
                return Results.Accepted($"/api/v1/payments/{result.PaymentId}", result);
            }
            catch (InvalidOperationException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
            }
            catch (ArgumentException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
            }
            catch (ValidationException ex)
            {
                return EndpointFailureResults.ValidationProblem(ex);
            }
        })
        .WithTags("Payments")
        .WithName("CreatePayment")
        .RequireAuthorization();

        app.MapPost("/api/v1/payments/{id:guid}/refund", async (
            Guid id,
            RefundPaymentCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var payment = await queryProcessor.ExecuteAsync(new GetPaymentByIdQuery(id), ct);
            if (payment == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, payment.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            command.PaymentId = id;
            try
            {
                var result = await commandProcessor.SendWithResultAsync<RefundPaymentCommand, RefundResult>(command, ct);
                return Results.Accepted($"/api/v1/payments/{id}/refund", result);
            }
            catch (InvalidOperationException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
            }
            catch (ValidationException ex)
            {
                return EndpointFailureResults.ValidationProblem(ex);
            }
        })
        .WithTags("Payments")
        .WithName("RefundPayment")
        .RequireAuthorization();

        app.MapPost("/api/v1/payments/{id:guid}/capture", async (
            Guid id,
            CapturePaymentCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var payment = await queryProcessor.ExecuteAsync(new GetPaymentByIdQuery(id), ct);
            if (payment == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, payment.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            command.PaymentId = id;
            try
            {
                var result = await commandProcessor.SendWithResultAsync<CapturePaymentCommand, CaptureResult>(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
            }
            catch (ValidationException ex)
            {
                return EndpointFailureResults.ValidationProblem(ex);
            }
        })
        .WithTags("Payments")
        .WithName("CapturePayment")
        .RequireAuthorization();

        app.MapPost("/api/v1/payments/{id:guid}/cancel", async (
            Guid id,
            CancelPaymentCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var payment = await queryProcessor.ExecuteAsync(new GetPaymentByIdQuery(id), ct);
            if (payment == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, payment.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            command.PaymentId = id;
            try
            {
                var result = await commandProcessor.SendWithResultAsync<CancelPaymentCommand, CaptureResult>(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
            }
            catch (ValidationException ex)
            {
                return EndpointFailureResults.ValidationProblem(ex);
            }
        })
        .WithTags("Payments")
        .WithName("CancelPayment")
        .RequireAuthorization();

        // Reprocess: republish pending/failed payments to DDS for re-analysis
        app.MapPost("/api/v1/payments/{id:guid}/reprocess", async (
            Guid id,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            IDdsPublisher ddsPublisher,
            CancellationToken ct) =>
        {
            var payment = await queryProcessor.ExecuteAsync(new GetPaymentByIdQuery(id), ct);
            if (payment == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, payment.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            // Only reprocess pending or failed payments
            var status = payment.Status?.ToLowerInvariant();
            if (status != "pending" && status != "failed" && status != "expired")
                return Results.BadRequest(new { error = $"Cannot reprocess payment with status '{payment.Status}'" });

            // Republish to DDS for reprocessing
            await ddsPublisher.PublishAsync("payment.create", new
            {
                paymentId = payment.Id,
                merchantId = payment.MerchantId,
                amount = payment.Amount,
                currency = payment.Currency,
                method = payment.Method,
                customer = payment.Customer,
                timestamp = DateTime.UtcNow
            }, ct);

            return Results.Ok(new { message = "Payment queued for reprocessing", paymentId = id });
        })
        .WithTags("Payments")
        .WithName("ReprocessPayment")
        .RequireAuthorization();

        return app;
    }
}
