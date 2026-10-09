using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels
{
    /// <summary>A handler's outcome: data on success, otherwise an HTTP status and a message the person can read.</summary>
    [ExcludeFromCodeCoverage]
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ApiResult<T> Ok(T data, int status = 200) => new() { Success = true, StatusCode = status, Data = data };
        public static ApiResult<T> Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
