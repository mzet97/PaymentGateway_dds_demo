using FluentValidation;

namespace PaymentGateway.Api.Endpoints;

internal static class EndpointFailureResults
{
    public static IResult BadRequest(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Results.BadRequest(new { error = exception.Message });
    }

    public static IResult ValidationProblem(ValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errors = exception.Errors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "request" : error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return Results.ValidationProblem(errors);
    }
}
