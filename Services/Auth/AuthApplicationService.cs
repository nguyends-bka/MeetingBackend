using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Auth;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services.Auth;

public class AuthApplicationService : IAuthApplicationService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IFaceAuthService _faceAuthService;

    public AuthApplicationService(AppDbContext db, JwtTokenService jwt, IFaceAuthService faceAuthService)
    {
        _db = db;
        _jwt = jwt;
        _faceAuthService = faceAuthService;
    }

    public async Task<AuthActionResult> RegisterAsync(RegisterRequestDto req, CancellationToken cancellationToken = default)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username, cancellationToken))
            return AuthActionResult.BadRequest("Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác.");

        if (!string.IsNullOrWhiteSpace(req.AcademicRank)
            && req.AcademicRank != "GS"
            && req.AcademicRank != "PGS")
        {
            return AuthActionResult.BadRequest("Học hàm chỉ nhận GS hoặc PGS");
        }

        if (!string.IsNullOrWhiteSpace(req.AcademicDegree)
            && req.AcademicDegree != "TS"
            && req.AcademicDegree != "ThS"
            && req.AcademicDegree != "CN"
            && req.AcademicDegree != "KS")
        {
            return AuthActionResult.BadRequest("Học vị chỉ nhận TS, ThS, CN hoặc KS");
        }

        if (!string.IsNullOrWhiteSpace(req.Avatar))
        {
            try
            {
                _ = Convert.FromBase64String(req.Avatar);
            }
            catch (FormatException)
            {
                return AuthActionResult.BadRequest("Avatar phải là chuỗi Base64 hợp lệ");
            }
        }

        if (req.OrganizationUnitId.HasValue)
        {
            var ouExists = await _db.OrganizationUnits
                .AsNoTracking()
                .AnyAsync(x => x.Id == req.OrganizationUnitId.Value, cancellationToken);
            if (!ouExists)
            {
                return AuthActionResult.BadRequest("Đơn vị công tác không tồn tại");
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = Roles.User,
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLower(),
            Position = string.IsNullOrWhiteSpace(req.Position) ? null : req.Position.Trim(),
            AcademicRank = string.IsNullOrWhiteSpace(req.AcademicRank) ? null : req.AcademicRank.Trim(),
            AcademicDegree = string.IsNullOrWhiteSpace(req.AcademicDegree) ? null : req.AcademicDegree.Trim(),
            OrganizationUnitId = req.OrganizationUnitId,
            Avatar = string.IsNullOrWhiteSpace(req.Avatar) ? null : req.Avatar.Trim(),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AuthActionResult.Ok("Registration successful");
    }

    public async Task<AuthActionResult<LoginResponseDto>> LoginAsync(LoginRequestDto req, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username, cancellationToken);

        if (user == null)
            return AuthActionResult<LoginResponseDto>.Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return AuthActionResult<LoginResponseDto>.Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng");

        var token = _jwt.CreateToken(user);

        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(user)
        };

        return AuthActionResult<LoginResponseDto>.Ok(response);
    }

    public async Task<AuthActionResult<LoginResponseDto>> LoginWithFaceAsync(FaceLoginRequestDto req, CancellationToken cancellationToken = default)
    {
        var authResult = await _faceAuthService.AuthenticateAsync(req.Embedding ?? Array.Empty<int>(), cancellationToken);

        if (!authResult.IsSuccess)
        {
            var message = authResult.ErrorMessage ?? "Face không hợp lệ";
            if (string.Equals(message, "Embedding không hợp lệ", StringComparison.Ordinal)
                || message.Contains("-128..127", StringComparison.Ordinal))
            {
                return AuthActionResult<LoginResponseDto>.BadRequest(message);
            }

            return AuthActionResult<LoginResponseDto>.Unauthorized(message);
        }

        var bestUser = authResult.User!;
        var token = _jwt.CreateToken(bestUser);

        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(bestUser)
        };

        return AuthActionResult<LoginResponseDto>.Ok(response);
    }

    public async Task<AuthActionResult<LoginResponseDto>> RefreshSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            return AuthActionResult<LoginResponseDto>.Unauthorized("Người dùng không tồn tại");

        var token = _jwt.CreateToken(user);
        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(user)
        };

        return AuthActionResult<LoginResponseDto>.Ok(response);
    }
}
