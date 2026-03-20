using System.Security.Claims;
using FluentValidation;
using Paramore.Brighter;
using Paramore.Darker;
using PaymentGateway.Api.Common.Authorization;
using PaymentGateway.Application.Common.Messaging;
using PaymentGateway.Application.UseCases.Webhooks.Commands;
using PaymentGateway.Application.UseCases.Webhooks.Queries;
using PaymentGateway.Application.UseCases.Webhooks.ViewModels;

namespace PaymentGateway.Api.Endpoints;

public static class WebhooksEndpoints
{
    public static WebApplication MapWebhooksEndpoints(this WebApplication app)
    {
        app.MapPut("/api/v1/webhooks", async (
            CreateWebhookCommand command,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var scope = merchantScopeService.Resolve(user, command.MerchantId, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            try
            {
                var result = await commandProcessor.SendWithResultAsync<CreateWebhookCommand, WebhookDto>(command, ct);
                return Results.Created($"/api/v1/webhooks/{result.WebhookId}", result);
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
        .WithTags("Webhooks")
        .WithName("CreateWebhook")
        .RequireAuthorization();

        app.MapGet("/api/v1/webhooks", async (Guid merchantId, ClaimsPrincipal user, IMerchantScopeService merchantScopeService, IQueryProcessor queryProcessor, CancellationToken ct) =>
        {
            var scope = merchantScopeService.Resolve(user, merchantId, allowGlobalForAdmin: false);
            if (scope.ToErrorResult() is { } error)
                return error;

            var webhooks = await queryProcessor.ExecuteAsync(new GetWebhooksQuery(scope.MerchantId ?? merchantId), ct);
            return Results.Ok(webhooks);
        })
        .WithTags("Webhooks")
        .WithName("GetWebhooks")
        .RequireAuthorization();

        app.MapDelete("/api/v1/webhooks/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IMerchantScopeService merchantScopeService,
            IQueryProcessor queryProcessor,
            IAmACommandProcessor commandProcessor,
            CancellationToken ct) =>
        {
            var webhook = await queryProcessor.ExecuteAsync(new GetWebhookByIdQuery(id), ct);
            if (webhook == null)
                return Results.NotFound();

            var scope = merchantScopeService.Resolve(user, webhook.MerchantId, allowGlobalForAdmin: true);
            if (scope.ToErrorResult() is { } error)
                return error;

            try
            {
                await commandProcessor.SendWithResultAsync<DeleteWebhookCommand, bool>(new DeleteWebhookCommand
                {
                    WebhookId = id
                }, ct);
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

            return Results.NoContent();
        })
        .WithTags("Webhooks")
        .WithName("DeleteWebhook")
        .RequireAuthorization();

        return app;
    }
}
