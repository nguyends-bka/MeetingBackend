using System.Security.Cryptography;
using System.Text;
using MeetingBackend.Data;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services;

public class MeetingCodeService
{
    private readonly AppDbContext _db;
    private const string CHARACTERS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Loại bỏ 0, O, I, 1 để tránh nhầm lẫn
    private const int CODE_LENGTH = 6;

    public MeetingCodeService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Tạo code ngắn duy nhất cho meeting (6 ký tự)
    /// </summary>
    public async Task<string> GenerateUniqueCodeAsync()
    {
        string code;
        int attempts = 0;
        const int maxAttempts = 10;

        do
        {
            code = GenerateRandomCode();
            attempts++;

            // Kiểm tra code đã tồn tại chưa
            var exists = await _db.Meetings.AnyAsync(m => m.MeetingCode == code);

            if (!exists)
                return code;

            // Nếu đã thử nhiều lần, tăng độ dài code
            if (attempts >= maxAttempts)
            {
                // Fallback: sử dụng GUID ngắn hơn
                code = Guid.NewGuid().ToString("N").Substring(0, CODE_LENGTH).ToUpper();
                var fallbackExists = await _db.Meetings.AnyAsync(m => m.MeetingCode == code);
                if (!fallbackExists)
                    return code;
            }
        } while (attempts < maxAttempts * 2);

        // Cuối cùng, sử dụng timestamp-based code
        return GenerateTimestampCode();
    }

    private string GenerateRandomCode()
    {
        var random = new Random();
        var code = new StringBuilder(CODE_LENGTH);

        for (int i = 0; i < CODE_LENGTH; i++)
        {
            code.Append(CHARACTERS[random.Next(CHARACTERS.Length)]);
        }

        return code.ToString();
    }

    private string GenerateTimestampCode()
    {
        // Sử dụng timestamp để đảm bảo unique
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = new StringBuilder();
        
        // Convert timestamp sang base32-like encoding
        var chars = CHARACTERS.ToCharArray();
        var num = (ulong)timestamp;
        
        for (int i = 0; i < CODE_LENGTH; i++)
        {
            code.Append(chars[num % (ulong)chars.Length]);
            num /= (ulong)chars.Length;
        }

        return code.ToString();
    }

    /// <summary>
    /// Tạo passcode số ngẫu nhiên (4-6 chữ số)
    /// </summary>
    public string GeneratePasscode(int length = 6)
    {
        var random = new Random();
        var passcode = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            passcode.Append(random.Next(0, 10)); // 0-9
        }

        return passcode.ToString();
    }
}
