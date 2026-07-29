namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Shared envelope for every API response, following the team's api_design_rules:
/// { "code", "message", "data" }.
/// Properties are PascalCase in C# but ASP.NET Core serialises them as camelCase.
/// </summary>
public class ApiResponse<T>
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Code = 200, Message = message, Data = data };

    public static ApiResponse<T> Fail(int code, string message) =>
        new() { Code = code, Message = message, Data = default };
}
