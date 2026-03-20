using System.Security.Claims;
using FluentValidation;
using Paramore.Brighter;
using Paramore.Darker;
using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Merchants.Commands;
using PaymentGateway.Application.UseCases.Merchants.Queries;
using PaymentGateway.Application.UseCases.Merchants.ViewModels;
using PaymentGateway.Application.UseCases.Payments.Queries;

namespace PaymentGateway.Api.Endpoints;

public static class MerchantsEndpoints
{
    public static WebApplication MapMerchantsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/merchants", async (
            CreateMerchantCommand command,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            try
            {
                var result = await commandProcessor.SendWithResultAsync<CreateMerchantCommand, CreateMerchantResult>(command, ct);
                return Results.Created($"/api/v1/merchants/{result.MerchantId}", result);
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
        .WithTags("Merchants")
        .WithName("CreateMerchant")
        .RequireAuthorization(policy => policy.RequireRole("Admin", "admin"));

        app.MapGet("/api/v1/merchants/{id:guid}", async (Guid id, ClaimsPrincipal user, IMerchantScopeService merchantScopeService, IQueryProcessor queryProcessor, CancellationToken ct) =>
        {
            var scope = merchantScopeService.Resolve(user, id, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            var merchant = await queryProcessor.ExecuteAsync(new GetMerchantByIdQuery(id), ct);
            if (merchant == null)
                return Results.NotFound();

            return Results.Ok(merchant);
        })
        .WithTags("Merchants")
        .WithName("GetMerchant")
        .RequireAuthorization();

        app.MapPut("/api/v1/merchants/{id:guid}", async (
            Guid id,
            UpdateMerchantCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IAmACommandProcessor commandProcessor,
            IQueryProcessor queryProcessor,
            CancellationToken ct) =>
        {
            var scope = merchantScopeService.Resolve(user, id, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            var merchant = await queryProcessor.ExecuteAsync(new GetMerchantByIdQuery(id), ct);
            if (merchant == null)
                return Results.NotFound();

            command.MerchantId = id;
            try
            {
                var result = await commandProcessor.SendWithResultAsync<UpdateMerchantCommand, MerchantDto>(command, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return EndpointFailureResults.BadRequest(ex);
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
        .WithTags("Merchants")
        .WithName("UpdateMerchant")
        .RequireAuthorization();

        app.MapGet("/api/v1/merchants/{id:guid}/payments", async (
            Guid id,
            string? status,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            CancellationToken ct,
            int limit = 50,
            int offset = 0) =>
        {
            var scope = merchantScopeService.Resolve(user, id, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            var merchantId = scope.MerchantId ?? id;
            var result = await queryProcessor.ExecuteAsync(new GetPaymentsQuery
            {
                MerchantId = merchantId,
                Status = status,
                Limit = limit,
                Offset = offset
            }, ct);

            return Results.Ok(result);
        })
        .WithTags("Merchants")
        .WithName("GetMerchantPayments")
        .RequireAuthorization();

        return app;
    }
}
