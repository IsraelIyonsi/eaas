namespace EaaS.Shared.Contracts;

public record ApiResponse<T>(bool Success, T Data);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new(true, data);
}
