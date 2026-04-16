using Microsoft.AspNetCore.Mvc;
using MeetingBackend.DTOs.Auth;
using MeetingBackend.Services.Auth;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthApplicationService _authApplicationService;

    public AuthController(IAuthApplicationService authApplicationService)
    {
        _authApplicationService = authApplicationService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto req)
    {
        var result = await _authApplicationService.RegisterAsync(req, HttpContext.RequestAborted);
        return result.Status switch
        {
            AuthActionStatus.Ok => Ok(new { message = result.Message ?? "Registration successful" }),
            AuthActionStatus.BadRequest => BadRequest(new { message = result.Message }),
            AuthActionStatus.Unauthorized => Unauthorized(new { message = result.Message }),
            _ => BadRequest(new { message = "Yeu cau khong hop le" }),
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto req)
    {
        var result = await _authApplicationService.LoginAsync(req, HttpContext.RequestAborted);
        return result.Status switch
        {
            AuthActionStatus.Ok => Ok(result.Data),
            AuthActionStatus.BadRequest => BadRequest(new { message = result.Message }),
            AuthActionStatus.Unauthorized => Unauthorized(new { message = result.Message }),
            _ => BadRequest(new { message = "Yeu cau khong hop le" }),
        };
    }

    [HttpPost("login/face")]
    public async Task<IActionResult> LoginWithFace(FaceLoginRequestDto req)
    {
        var result = await _authApplicationService.LoginWithFaceAsync(req, HttpContext.RequestAborted);
        return result.Status switch
        {
            AuthActionStatus.Ok => Ok(result.Data),
            AuthActionStatus.BadRequest => BadRequest(new { message = result.Message }),
            AuthActionStatus.Unauthorized => Unauthorized(new { message = result.Message }),
            _ => BadRequest(new { message = "Yeu cau khong hop le" }),
        };
    }
}
