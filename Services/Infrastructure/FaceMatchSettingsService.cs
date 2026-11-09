using System.Globalization;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services.Infrastructure;

public interface IFaceMatchSettingsService
{
    Task<double> GetThresholdAsync(CancellationToken cancellationToken = default);
    Task SetThresholdAsync(double threshold, CancellationToken cancellationToken = default);
}

public sealed class FaceMatchSettingsService : IFaceMatchSettingsService
{
    public const double DefaultThreshold = 0.80;
    public const double MinThreshold = 0.50;
    public const double MaxThreshold = 0.99;

    private const string ThresholdKey = "FaceMatch.Threshold";

    private readonly AppDbContext _db;
    private readonly ILogger<FaceMatchSettingsService> _logger;

    public FaceMatchSettingsService(AppDbContext db, ILogger<FaceMatchSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<double> GetThresholdAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == ThresholdKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored == null) return DefaultThreshold;

        if (double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value >= MinThreshold
            && value <= MaxThreshold)
        {
            return value;
        }

        _logger.LogWarning("Invalid face threshold in database: {Value}. Using default.", stored);
        return DefaultThreshold;
    }

    public async Task SetThresholdAsync(double threshold, CancellationToken cancellationToken = default)
    {
        var value = threshold.ToString("0.00", CultureInfo.InvariantCulture);
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == ThresholdKey, cancellationToken);

        if (setting == null)
        {
            setting = new SystemSetting { Key = ThresholdKey };
            _db.SystemSettings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
