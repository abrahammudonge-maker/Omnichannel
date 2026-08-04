namespace Omni.Shared.Responses;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = (errors ?? Array.Empty<string>()).ToList() };
}

public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse Ok(string message = "Request completed successfully.") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = (errors ?? Array.Empty<string>()).ToList() };
}
