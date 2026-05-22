using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services.Meeting;

public class MeetingApplicationService : IMeetingApplicationService
{
    private readonly AppDbContext _db;
    private readonly LiveKitTokenService _liveKit;
    private readonly LiveKitEgressService _egress;
    private readonly IConfiguration _config;
    private readonly MeetingCodeService _codeService;

    public MeetingApplicationService(
        AppDbContext db,
        LiveKitTokenService liveKit,
        LiveKitEgressService egress,
        IConfiguration config,
        MeetingCodeService codeService)
    {
        _db = db;
        _liveKit = liveKit;
        _egress = egress;
        _config = config;
        _codeService = codeService;
    }

    public async Task<MeetingAppResult<CreateMeetingResponseDto>> CreateAsync(CurrentUserContext user, CreateMeetingRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return MeetingAppResult<CreateMeetingResponseDto>.Unauthorized("User identity not found");

        var startAtUtc = request.StartAt.HasValue
            ? FromUnixMs(request.StartAt.Value)
            : DateTime.UtcNow;
        DateTime? estimatedEndUtc = request.EstimatedEndAt.HasValue
            ? FromUnixMs(request.EstimatedEndAt.Value)
            : null;
        if (estimatedEndUtc.HasValue && estimatedEndUtc.Value <= startAtUtc)
        {
            return MeetingAppResult<CreateMeetingResponseDto>.BadRequest("Thời gian kết thúc dự kiến phải sau thời gian bắt đầu");
        }

        var meetingCode = await _codeService.GenerateUniqueCodeAsync();
        var passcode = !string.IsNullOrEmpty(request.Passcode)
            ? request.Passcode
            : _codeService.GeneratePasscode(6);

        var meeting = new Entities.Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            HostName = request.HostName,
            HostIdentity = userId,
            Location = request.Location,
            RoomName = Guid.NewGuid().ToString(),
            MeetingCode = meetingCode,
            Passcode = passcode,
            CreatedAt = startAtUtc,
            StartedAt = estimatedEndUtc
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new CreateMeetingResponseDto
        {
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            Passcode = meeting.Passcode,
            RoomName = meeting.RoomName
        };

        return MeetingAppResult<CreateMeetingResponseDto>.Ok(response);
    }

    public async Task<MeetingAppResult<JoinMeetingResponseDto>> JoinByLinkAsync(CurrentUserContext user, JoinByLinkRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.MeetingId == Guid.Empty)
            return MeetingAppResult<JoinMeetingResponseDto>.BadRequest("Meeting ID is required");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == request.MeetingId, cancellationToken);

        if (meeting == null)
            return MeetingAppResult<JoinMeetingResponseDto>.NotFound("Meeting not found");

        var userId = user.UserId;
        var username = user.Username ?? "Unknown";
        if (string.IsNullOrWhiteSpace(userId))
            return MeetingAppResult<JoinMeetingResponseDto>.Unauthorized("User identity not found");

        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId, username, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return MeetingAppResult<JoinMeetingResponseDto>.Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(meeting.RoomName, userId, username);
        var liveKitUrl = NormalizeLiveKitUrl(_config["LiveKit:Url"]);

        var response = new JoinMeetingResponseDto
        {
            Token = token,
            LiveKitUrl = liveKitUrl,
            RoomName = meeting.RoomName,
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            ParticipantId = participant.Id,
            HostIdentity = meeting.HostIdentity,
            IsMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username, cancellationToken),
        };

        return MeetingAppResult<JoinMeetingResponseDto>.Ok(response);
    }

    public async Task<MeetingAppResult<JoinMeetingResponseDto>> JoinAsync(CurrentUserContext user, JoinMeetingRequestDto request, CancellationToken cancellationToken = default)
    {
        Entities.Meeting? meeting = null;

        if (request.MeetingId.HasValue && request.MeetingId.Value != Guid.Empty)
        {
            meeting = await _db.Meetings
                .FirstOrDefaultAsync(m => m.Id == request.MeetingId.Value, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.MeetingCode))
        {
            meeting = await _db.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == request.MeetingCode.ToUpper().Trim(), cancellationToken);
        }

        if (meeting == null)
            return MeetingAppResult<JoinMeetingResponseDto>.NotFound("Meeting not found");

        if (string.IsNullOrEmpty(request.Passcode) || meeting.Passcode != request.Passcode)
            return MeetingAppResult<JoinMeetingResponseDto>.Unauthorized("Invalid passcode");

        var userId = user.UserId;
        var username = user.Username ?? "Unknown";
        if (string.IsNullOrWhiteSpace(userId))
            return MeetingAppResult<JoinMeetingResponseDto>.Unauthorized("User identity not found");

        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId, username, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return MeetingAppResult<JoinMeetingResponseDto>.Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(meeting.RoomName, userId, username);
        var liveKitUrl = NormalizeLiveKitUrl(_config["LiveKit:Url"]);

        var response = new JoinMeetingResponseDto
        {
            Token = token,
            LiveKitUrl = liveKitUrl,
            RoomName = meeting.RoomName,
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            ParticipantId = participant.Id,
            HostIdentity = meeting.HostIdentity,
            IsMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username, cancellationToken),
        };

        return MeetingAppResult<JoinMeetingResponseDto>.Ok(response);
    }

    public async Task<MeetingAppResult<object>> JoinByCodeAsync(CurrentUserContext user, JoinMeetingRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.MeetingCode))
            return MeetingAppResult<object>.BadRequest("Meeting code is required");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.MeetingCode == request.MeetingCode.ToUpper().Trim(), cancellationToken);

        if (meeting == null)
            return MeetingAppResult<object>.NotFound("Meeting not found");

        if (string.IsNullOrEmpty(request.Passcode) || meeting.Passcode != request.Passcode)
            return MeetingAppResult<object>.Unauthorized("Invalid passcode");

        var userId = user.UserId;
        var username = user.Username ?? "Unknown";
        if (string.IsNullOrWhiteSpace(userId))
            return MeetingAppResult<object>.Unauthorized("User identity not found");

        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId, username, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return MeetingAppResult<object>.Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(meeting.RoomName, userId, username);
        var liveKitUrl = NormalizeLiveKitUrl(_config["LiveKit:Url"]);

        return MeetingAppResult<object>.Ok(new
        {
            token,
            liveKitUrl,
            roomName = meeting.RoomName,
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            title = meeting.Title,
            participantId = participant.Id,
            hostIdentity = meeting.HostIdentity,
            isMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username, cancellationToken),
        });
    }

    public async Task<MeetingAppResult<List<MeetingListItemDto>>> GetMeetingsAsync(CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        var userRole = user.Role;
        var username = user.Username ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return MeetingAppResult<List<MeetingListItemDto>>.Unauthorized("User identity not found");

        IQueryable<Entities.Meeting> query = _db.Meetings;

        HashSet<Guid> coHostMeetingIdSet = new();
        if (userRole != "Admin")
        {
            var coIds = await _db.MeetingCoHosts
                .AsNoTracking()
                .Where(c => c.HostUserId == userId)
                .Select(c => c.MeetingId)
                .ToListAsync(cancellationToken);
            coHostMeetingIdSet = coIds.ToHashSet();
            query = query.Where(m => m.HostIdentity == userId || coHostMeetingIdSet.Contains(m.Id));
        }

        var meetings = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var meetingIds = meetings.Select(m => m.Id).ToList();

        var activeCounts = await _db.MeetingParticipants
            .Where(p => meetingIds.Contains(p.MeetingId) && p.LeftAt == null)
            .GroupBy(p => p.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count, cancellationToken);

        var managerMeetingIds = string.IsNullOrWhiteSpace(username)
            ? new HashSet<Guid>()
            : (await _db.MeetingPollManagers
                .AsNoTracking()
                .Where(x => x.Username.ToLower() == username.Trim().ToLower())
                .Select(x => x.MeetingId)
                .ToListAsync(cancellationToken))
              .ToHashSet();

        var normalizedUserId = userId.Trim();
        var normalizedUsername = username.Trim();
        var response = meetings.Select(m =>
        {
            var isPrimaryHost = string.Equals(m.HostIdentity, normalizedUserId, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedUsername)
                    && string.Equals(m.HostIdentity, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            var isCoHost = coHostMeetingIdSet.Contains(m.Id);
            var isMeetingHost = isPrimaryHost || isCoHost;
            var canManagePoll = isMeetingHost || managerMeetingIds.Contains(m.Id);
            var dto = MeetingMapper.ToMeetingListItemDto(m);
            dto.IsMeetingHost = isMeetingHost;
            dto.CanManagePoll = canManagePoll;
            dto.ActiveParticipantCount = activeCounts.TryGetValue(m.Id, out var c) ? c : 0;
            return dto;
        }).ToList();

        await EnrichHostInfoAsync(response, cancellationToken);

        return MeetingAppResult<List<MeetingListItemDto>>.Ok(response);
    }

    public async Task<MeetingAppResult<MeetingListItemDto>> GetMeetingByIdAsync(CurrentUserContext user, Guid meetingId, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        var userRole = user.Role;
        var username = user.Username ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return MeetingAppResult<MeetingListItemDto>.Unauthorized("User identity not found");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);

        if (meeting == null)
            return MeetingAppResult<MeetingListItemDto>.NotFound("Meeting not found");

        // Phân quyền: Admin, Host, Co-host, Invitee, hoặc đã từng tham gia (Participant)
        bool hasAccess = userRole == "Admin";

        if (!hasAccess)
        {
            var isPrimaryHost = string.Equals(meeting.HostIdentity, userId, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(username)
                    && string.Equals(meeting.HostIdentity, username.Trim(), StringComparison.OrdinalIgnoreCase));

            if (isPrimaryHost)
            {
                hasAccess = true;
            }
            else
            {
                var isCoHost = await MeetingHostAuth.IsCoHostAsync(_db, meetingId, userId, cancellationToken);
                if (isCoHost)
                {
                    hasAccess = true;
                }
                else
                {
                    var isInvited = await _db.MeetingInvitees
                        .AnyAsync(i => i.MeetingId == meetingId && i.Username.ToLower() == username.Trim().ToLower(), cancellationToken);
                    if (isInvited)
                    {
                        hasAccess = true;
                    }
                    else
                    {
                        var hasParticipated = await _db.MeetingParticipants
                            .AnyAsync(p => p.MeetingId == meetingId && (p.UserId == userId || p.Username.ToLower() == username.Trim().ToLower()), cancellationToken);
                        if (hasParticipated)
                        {
                            hasAccess = true;
                        }
                    }
                }
            }
        }

        if (!hasAccess)
        {
            return MeetingAppResult<MeetingListItemDto>.Unauthorized("Bạn không có quyền truy cập thông tin cuộc họp này");
        }

        var isPrimaryHostUser = string.Equals(meeting.HostIdentity, userId.Trim(), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(username)
                && string.Equals(meeting.HostIdentity, username.Trim(), StringComparison.OrdinalIgnoreCase));
        var isCoHostUser = await MeetingHostAuth.IsCoHostAsync(_db, meetingId, userId, cancellationToken);
        var isMeetingHost = isPrimaryHostUser || isCoHostUser;

        var managerMeetingIds = string.IsNullOrWhiteSpace(username)
            ? new HashSet<Guid>()
            : (await _db.MeetingPollManagers
                .AsNoTracking()
                .Where(x => x.Username.ToLower() == username.Trim().ToLower() && x.MeetingId == meetingId)
                .Select(x => x.MeetingId)
                .ToListAsync(cancellationToken))
              .ToHashSet();

        var canManagePoll = isMeetingHost || managerMeetingIds.Contains(meetingId);

        var activeParticipantCount = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.LeftAt == null)
            .CountAsync(cancellationToken);

        var dto = MeetingMapper.ToMeetingListItemDto(meeting);
        dto.IsMeetingHost = isMeetingHost;
        dto.CanManagePoll = canManagePoll;
        dto.ActiveParticipantCount = activeParticipantCount;

        var list = new List<MeetingListItemDto> { dto };
        await EnrichHostInfoAsync(list, cancellationToken);

        return MeetingAppResult<MeetingListItemDto>.Ok(list[0]);
    }

    public async Task<MeetingAppResult<MeetingListItemDto>> UpdateMeetingAsync(CurrentUserContext user, Guid meetingId, UpdateMeetingRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        var username = user.Username ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.Title))
            return MeetingAppResult<MeetingListItemDto>.BadRequest("Tiêu đề cuộc họp không được để trống");

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);
        if (meeting == null)
            return MeetingAppResult<MeetingListItemDto>.NotFound("Meeting not found");

        if (!await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId ?? string.Empty, username, cancellationToken))
            return MeetingAppResult<MeetingListItemDto>.Unauthorized("Only meeting host can update this meeting");

        if (meeting.EndedAt.HasValue)
            return MeetingAppResult<MeetingListItemDto>.BadRequest("Cuộc họp đã kết thúc, không thể chỉnh sửa");

        var activeCount = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.LeftAt == null)
            .CountAsync(cancellationToken);
        if (activeCount > 0)
            return MeetingAppResult<MeetingListItemDto>.BadRequest("Cuộc họp đang diễn ra, không thể chỉnh sửa");

        var startAtUtc = FromUnixMs(request.StartAt);
        DateTime? estimatedEndUtc = request.EstimatedEndAt.HasValue
            ? FromUnixMs(request.EstimatedEndAt.Value)
            : null;
        if (estimatedEndUtc.HasValue && estimatedEndUtc.Value <= startAtUtc)
            return MeetingAppResult<MeetingListItemDto>.BadRequest("Thời gian kết thúc dự kiến phải sau thời gian bắt đầu");

        meeting.Title = request.Title.Trim();
        meeting.CreatedAt = startAtUtc;
        meeting.StartedAt = estimatedEndUtc;

        await _db.SaveChangesAsync(cancellationToken);

        var dto = MeetingMapper.ToMeetingListItemDto(meeting);
        dto.IsMeetingHost = true;
        dto.CanManagePoll = true;
        dto.ActiveParticipantCount = 0;

        var list = new List<MeetingListItemDto> { dto };
        await EnrichHostInfoAsync(list, cancellationToken);

        return MeetingAppResult<MeetingListItemDto>.Ok(list[0]);
    }

    public async Task<MeetingAppResult<LeaveMeetingResponseDto>> LeaveAsync(CurrentUserContext user, LeaveMeetingRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return MeetingAppResult<LeaveMeetingResponseDto>.Unauthorized("User identity not found");

        var meetingId = request.MeetingId ?? Guid.Empty;

        if (meetingId == Guid.Empty && request.ParticipantId.HasValue)
        {
            meetingId = await _db.MeetingParticipants
                .Where(p => p.Id == request.ParticipantId.Value && p.UserId == userId)
                .Select(p => p.MeetingId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (meetingId == Guid.Empty)
            return MeetingAppResult<LeaveMeetingResponseDto>.BadRequest("MeetingId is required");

        var now = DateTime.UtcNow;

        var actives = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.UserId == userId && p.LeftAt == null)
            .ToListAsync(cancellationToken);

        foreach (var p in actives)
        {
            p.LeftAt = now;
        }

        if (actives.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        var response = new LeaveMeetingResponseDto
        {
            Message = "Left meeting successfully",
            UpdatedCount = actives.Count
        };

        return MeetingAppResult<LeaveMeetingResponseDto>.Ok(response);
    }

    public async Task<MeetingAppResult<object>> EndMeetingAsync(CurrentUserContext user, Guid meetingId, CancellationToken cancellationToken = default)
    {
        var userId = user.UserId;
        var username = user.Username ?? string.Empty;

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);
        if (meeting == null)
            return MeetingAppResult<object>.NotFound("Meeting not found");

        if (!await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId ?? string.Empty, username, cancellationToken))
            return MeetingAppResult<object>.Unauthorized("Only meeting host can end meeting");

        var now = DateTime.UtcNow;
        meeting.StartedAt ??= now;
        meeting.EndedAt = now;

        var activeRecordings = await _db.MeetingRecordings
            .Where(r => r.MeetingId == meetingId
                && (r.Status == "Starting" || r.Status == "Active" || r.Status == "Stopping"))
            .ToListAsync(cancellationToken);

        foreach (var recording in activeRecordings)
        {
            recording.EndedAtUtc ??= now;

            if (string.IsNullOrWhiteSpace(recording.EgressId))
            {
                recording.Status = "Failed";
                recording.ErrorMessage = "Recording does not have egress id";
                continue;
            }

            var (ok, error) = await _egress.StopEgressAsync(recording.EgressId, cancellationToken);
            var isAlreadyFinished = !string.IsNullOrWhiteSpace(error)
                && error.Contains("EGRESS_COMPLETE", StringComparison.OrdinalIgnoreCase);

            // Keep status at Stopping after meeting end and let background watcher
            // finalize to Completed/Failed once output file state is stable.
            recording.Status = "Stopping";
            if (ok || isAlreadyFinished)
            {
                recording.ErrorMessage = null;
            }
            else
            {
                recording.ErrorMessage = error;
            }
        }

        var actives = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.LeftAt == null)
            .ToListAsync(cancellationToken);
        foreach (var p in actives)
        {
            p.LeftAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MeetingAppResult<object>.Ok(new { message = "Meeting ended", endedAt = meeting.EndedAt });
    }

    private static DateTime FromUnixMs(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
    }

    private static string NormalizeLiveKitUrl(string? rawUrl)
    {
        var value = rawUrl?.Trim();
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        static string StripRtcPath(string path)
        {
            var normalized = path.TrimEnd('/');
            if (normalized.EndsWith("/rtc/v1", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^7];
            else if (normalized.EndsWith("/rtc", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^4];

            return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            var builder = new UriBuilder(absoluteUri)
            {
                Path = StripRtcPath(absoluteUri.AbsolutePath),
            };
            return builder.Uri.ToString().TrimEnd('/');
        }

        value = value.TrimEnd('/');
        if (value.EndsWith("/rtc/v1", StringComparison.OrdinalIgnoreCase))
            value = value[..^7];
        else if (value.EndsWith("/rtc", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];

        return value;
    }

    private async Task<MeetingParticipant> RecordJoinAsync(Guid meetingId, string userId, string username, CancellationToken cancellationToken)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);
        if (meeting == null)
        {
            throw new InvalidOperationException("Meeting not found");
        }

        var existingActive = await _db.MeetingParticipants
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.UserId == userId && p.LeftAt == null, cancellationToken);

        if (existingActive != null)
        {
            if (!string.Equals(existingActive.Username, username, StringComparison.Ordinal))
            {
                existingActive.Username = username;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return existingActive;
        }

        if (meeting.EndedAt.HasValue)
        {
            throw new UnauthorizedAccessException("Meeting has ended");
        }

        var participant = new MeetingParticipant
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            UserId = userId,
            Username = username,
            JoinedAt = DateTime.UtcNow
        };
        _db.MeetingParticipants.Add(participant);

        if (!meeting.StartedAt.HasValue)
        {
            meeting.StartedAt = participant.JoinedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return participant;
    }

    private async Task EnrichHostInfoAsync(List<MeetingListItemDto> dtos, CancellationToken cancellationToken)
    {
        var hostIdentities = dtos
            .Select(d => d.HostIdentity)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        if (!hostIdentities.Any())
            return;

        var hostGuids = new List<Guid>();
        var hostUsernames = new List<string>();
        foreach (var hid in hostIdentities)
        {
            if (Guid.TryParse(hid, out var g))
            {
                hostGuids.Add(g);
            }
            else
            {
                hostUsernames.Add(hid);
            }
        }

        var hostUsers = await _db.Users
            .AsNoTracking()
            .Where(u => hostGuids.Contains(u.Id) || hostUsernames.Contains(u.Username))
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync(cancellationToken);

        var hostMapById = hostUsers.ToDictionary(u => u.Id.ToString().ToLower(), u => u);
        var hostMapByUsername = hostUsers.ToDictionary(u => u.Username.ToLower(), u => u);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrEmpty(dto.HostIdentity))
                continue;

            var lookupKey = dto.HostIdentity.ToLower();
            if (hostMapById.TryGetValue(lookupKey, out var userObj) || hostMapByUsername.TryGetValue(lookupKey, out userObj))
            {
                dto.HostName = !string.IsNullOrWhiteSpace(userObj.FullName) ? userObj.FullName : userObj.Username;
                dto.HostIdentity = userObj.Username;
            }
        }
    }
}
