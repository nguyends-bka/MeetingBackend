using MeetingBackend.DTOs.Meeting;

namespace MeetingBackend.Services.Meeting;

public interface IMeetingApplicationService
{
    Task<MeetingAppResult<CreateMeetingResponseDto>> CreateAsync(CurrentUserContext user, CreateMeetingRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<JoinMeetingResponseDto>> JoinByLinkAsync(CurrentUserContext user, JoinByLinkRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<JoinMeetingResponseDto>> JoinAsync(CurrentUserContext user, JoinMeetingRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<object>> JoinByCodeAsync(CurrentUserContext user, JoinMeetingRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<List<MeetingListItemDto>>> GetMeetingsAsync(CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<MeetingListItemDto>> GetMeetingByIdAsync(CurrentUserContext user, Guid meetingId, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<MeetingListItemDto>> UpdateMeetingAsync(CurrentUserContext user, Guid meetingId, UpdateMeetingRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<LeaveMeetingResponseDto>> LeaveAsync(CurrentUserContext user, LeaveMeetingRequestDto request, CancellationToken cancellationToken = default);
    Task<MeetingAppResult<object>> EndMeetingAsync(CurrentUserContext user, Guid meetingId, CancellationToken cancellationToken = default);
}

public sealed class CurrentUserContext
{
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? Role { get; init; }
}

public enum MeetingAppStatus
{
    Ok,
    BadRequest,
    Unauthorized,
    NotFound,
}

public sealed class MeetingAppResult<T>
{
    public MeetingAppStatus Status { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }

    public static MeetingAppResult<T> Ok(T data) => new() { Status = MeetingAppStatus.Ok, Data = data };
    public static MeetingAppResult<T> BadRequest(string message) => new() { Status = MeetingAppStatus.BadRequest, Message = message };
    public static MeetingAppResult<T> Unauthorized(string message) => new() { Status = MeetingAppStatus.Unauthorized, Message = message };
    public static MeetingAppResult<T> NotFound(string message) => new() { Status = MeetingAppStatus.NotFound, Message = message };
}
