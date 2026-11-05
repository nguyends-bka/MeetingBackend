using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeetingBackend.Services;

public class FaceAuthService : IFaceAuthService
{
    private const float DefaultThreshold = 0.85f;

    // Hệ số giải lượng tử int8 -> float32 (giá trị / 127).
    private const float Int8Scale = 127f;

    private readonly AppDbContext _db;
    private readonly IFaceMatchingClient _faceMatchingClient;
    private readonly FaceMatchingOptions _options;
    private readonly ILogger<FaceAuthService> _logger;

    public FaceAuthService(
        AppDbContext db,
        IFaceMatchingClient faceMatchingClient,
        IOptions<FaceMatchingOptions> options,
        ILogger<FaceAuthService> logger)
    {
        _db = db;
        _faceMatchingClient = faceMatchingClient;
        _options = options.Value;
        _logger = logger;
    }

    private bool UseRemote =>
        string.Equals(_options.Mode, FaceMatchingModes.Remote, StringComparison.OrdinalIgnoreCase);

    public async Task<FaceAuthResult> AuthenticateAsync(int[] embedding, CancellationToken cancellationToken = default)
    {
        if (embedding == null || embedding.Length == 0)
        {
            return new FaceAuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Embedding không hợp lệ"
            };
        }

        if (!TryConvertToByteArray(embedding, out var probeBytes, out var errorMessage))
        {
            return new FaceAuthResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        var useRemote = UseRemote;
        var probeFloats = useRemote ? Array.Empty<float>() : ToFloatArray(embedding);

        var candidates = await _db.Users
            .Where(u => u.FaceEmbedding != null)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new FaceAuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Không có dữ liệu khuôn mặt phù hợp"
            };
        }

        User? bestUser = null;
        var bestScore = float.MinValue;

        foreach (var candidate in candidates)
        {
            if (candidate.FaceEmbedding == null) continue;

            var score = useRemote
                ? await BestSimilarityFromStoredEmbeddings(candidate.FaceEmbedding, probeBytes, cancellationToken)
                : BestLocalCosineFromStoredEmbeddings(candidate.FaceEmbedding, probeFloats);

            _logger.LogInformation(
                "FaceLogin mode={Mode} candidate={Username} score={Score:F4} threshold={Threshold:F2}",
                useRemote ? FaceMatchingModes.Remote : FaceMatchingModes.Local,
                candidate.Username,
                score,
                DefaultThreshold);

            if (score > bestScore)
            {
                bestScore = score;
                bestUser = candidate;
            }
        }

        _logger.LogInformation(
            "FaceLogin best_score={BestScore:F4} threshold={Threshold:F2} matched_user={MatchedUser}",
            bestScore,
            DefaultThreshold,
            bestUser?.Username ?? "<none>");

        if (bestUser == null || bestScore < DefaultThreshold)
        {
            return new FaceAuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Face không khớp",
                BestScore = bestScore
            };
        }

        return new FaceAuthResult
        {
            IsSuccess = true,
            User = bestUser,
            BestScore = bestScore
        };
    }

    private static bool TryConvertToByteArray(int[] source, out byte[] bytes, out string errorMessage)
    {
        bytes = Array.Empty<byte>();
        errorMessage = string.Empty;

        if (source.Length == 0)
        {
            errorMessage = "Embedding không hợp lệ";
            return false;
        }

        bytes = new byte[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] < -128 || source[i] > 127)
            {
                errorMessage = "Mỗi phần tử embedding phải nằm trong khoảng -128..127";
                return false;
            }

            bytes[i] = unchecked((byte)(sbyte)source[i]);
        }

        return true;
    }

    // ===== Mode "Remote": gọi service FaceMatching qua HTTP (cách cũ) =====
    private async Task<float> BestSimilarityFromStoredEmbeddings(short[,] stored, byte[] probe, CancellationToken cancellationToken)
    {
        if (probe.Length == 0 || stored.Length == 0) return 0;

        var rows = stored.GetLength(0);
        var cols = stored.GetLength(1);

        if (rows <= 0 || cols <= 0 || cols != probe.Length) return 0;

        var best = float.MinValue;
        var segment = new byte[cols];

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var value = stored[r, c];
                if (value < -128 || value > 127)
                {
                    return 0;
                }
                segment[c] = unchecked((byte)(sbyte)value);
            }

            var score = await _faceMatchingClient.ComputeSimilarityAsync(segment, probe, cancellationToken);
            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }

    // ===== Mode "Local": int8 -> float32 (/127) rồi tính cosine similarity =====

    /// <summary>
    /// Chuyển embedding int8 (-128..127, đã kiểm tra) sang float32 bằng cách chia cho 127.
    /// </summary>
    private static float[] ToFloatArray(int[] source)
    {
        var vector = new float[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            vector[i] = source[i] / Int8Scale;
        }
        return vector;
    }

    /// <summary>
    /// Với mỗi embedding đã lưu (mỗi hàng của mảng), chuyển int8 -> float32
    /// rồi tính cosine similarity với probe; trả về điểm cao nhất.
    /// </summary>
    private static float BestLocalCosineFromStoredEmbeddings(short[,] stored, float[] probe)
    {
        if (probe.Length == 0 || stored.Length == 0) return 0;

        var rows = stored.GetLength(0);
        var cols = stored.GetLength(1);

        if (rows <= 0 || cols <= 0 || cols != probe.Length) return 0;

        var best = float.MinValue;
        var segment = new float[cols];

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var value = stored[r, c];
                if (value < -128 || value > 127)
                {
                    return 0;
                }
                segment[c] = value / Int8Scale;
            }

            var score = CosineSimilarity(segment, probe);
            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0 || normB <= 0) return 0;

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
