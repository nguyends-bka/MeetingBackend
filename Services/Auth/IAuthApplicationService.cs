using MeetingBackend.DTOs.Auth;

namespace MeetingBackend.Services.Auth;

public interface IAuthApplicationService
{
    Task<AuthActionResult> RegisterAsync(RegisterRequestDto req, CancellationToken cancellationToken = default);
    Task<AuthActionResult<LoginResponseDto>> LoginAsync(LoginRequestDto req, CancellationToken cancellationToken = default);
    Task<AuthActionResult<LoginResponseDto>> LoginWithFaceAsync(FaceLoginRequestDto req, CancellationToken cancellationToken = default);
}

public enum AuthActionStatus
{
    Ok,
    BadRequest,
    Unauthorized,
}

public class AuthActionResult
{
    public AuthActionStatus Status { get; init; }
    public string? Message { get; init; }

    public static AuthActionResult Ok(string? message = null) => new() { Status = AuthActionStatus.Ok, Message = message };
    public static AuthActionResult BadRequest(string message) => new() { Status = AuthActionStatus.BadRequest, Message = message };
    public static AuthActionResult Unauthorized(string message) => new() { Status = AuthActionStatus.Unauthorized, Message = message };
}

public class AuthActionResult<T>
{
    public AuthActionStatus Status { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }

    public static AuthActionResult<T> Ok(T data) => new() { Status = AuthActionStatus.Ok, Data = data };
    public static AuthActionResult<T> BadRequest(string message) => new() { Status = AuthActionStatus.BadRequest, Message = message };
    public static AuthActionResult<T> Unauthorized(string message) => new() { Status = AuthActionStatus.Unauthorized, Message = message };
}
