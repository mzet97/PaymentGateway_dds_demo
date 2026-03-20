namespace PaymentGateway.Api.Common;

public record BaseResult<T>
{
    public T? Data { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = new();

    public static BaseResult<T> SuccessResult(T data, string message = "Operation completed successfully")
        => new() { Data = data, Success = true, Message = message };

    public static BaseResult<T> ErrorResult(string message, List<string>? errors = null)
        => new() { Data = default, Success = false, Message = message, Errors = errors ?? new List<string>() };

    public static BaseResult<T> ErrorResult(string message, string error)
        => new() { Data = default, Success = false, Message = message, Errors = new List<string> { error } };
}

public record BaseResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = new();

    public static BaseResult SuccessResult(string message = "Operation completed successfully")
        => new() { Success = true, Message = message };

    public static BaseResult ErrorResult(string message, List<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors ?? new List<string>() };

    public static BaseResult ErrorResult(string message, string error)
        => new() { Success = false, Message = message, Errors = new List<string> { error } };
}
