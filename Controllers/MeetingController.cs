using Microsoft.AspNetCore.Mvc;
using MeetingBackend.Models;
using MeetingBackend.Services;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
public class MeetingController : ControllerBase
{
    private readonly LiveKitTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public MeetingController(
        LiveKitTokenService tokenService,
        IConfiguration configuration)
    {
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [HttpPost("join")]
    public IActionResult Join([FromBody] JoinMeetingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Room) ||
            string.IsNullOrWhiteSpace(request.Identity))
        {
            return BadRequest("Room and Identity are required");
        }

        var token = _tokenService.CreateToken(
            request.Room,
            request.Identity
        );

        return Ok(new
        {
            token,
            liveKitUrl = _configuration["LiveKit:Url"]
        });
    }
}
