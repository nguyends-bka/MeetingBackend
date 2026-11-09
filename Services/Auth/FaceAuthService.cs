using System.Buffers.Binary;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeetingBackend.Services;

public class FaceAuthService : IFaceAuthService
{
    private const float DefaultThreshold = 0.85f;

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
        var probeFloats = Array.Empty<float>();
        if (!useRemote && !TryDecodeFloat32(probeBytes, out probeFloats))
        {
            return new FaceAuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Embedding không hợp lệ"
            };
        }

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

    // ===== Mode "Local": 512 byte -> 128 float32 (IEEE 754 little-endian) rồi tính cosine similarity =====

    /// <summary>
    /// Giải mã mảng byte thô thành float32 little-endian (4 byte / phần tử),
    /// tương đương struct.unpack("&lt;128f", raw) bên Python.
    /// </summary>
    private static bool TryDecodeFloat32(ReadOnlySpan<byte> raw, out float[] vector)
    {
        vector = Array.Empty<float>();
        if (raw.Length == 0 || raw.Length % sizeof(float) != 0) return false;

        var result = new float[raw.Length / sizeof(float)];
        for (var i = 0; i < result.Length; i++)
        {
            var value = BinaryPrimitives.ReadSingleLittleEndian(raw.Slice(i * sizeof(float), sizeof(float)));
            if (!float.IsFinite(value)) return false;
            result[i] = value;
        }

        vector = result;
        return true;
    }

    /// <summary>
    /// Với mỗi embedding đã lưu (mỗi hàng: các giá trị -128..127 = byte thô),
    /// giải mã sang float32 rồi tính cosine similarity với probe; trả về điểm cao nhất.
    /// </summary>
    private static float BestLocalCosineFromStoredEmbeddings(short[,] stored, float[] probe)
    {
        if (probe.Length == 0 || stored.Length == 0) return 0;

        var rows = stored.GetLength(0);
        var cols = stored.GetLength(1);

        if (rows <= 0 || cols <= 0 || cols != probe.Length * sizeof(float)) return 0;

        var best = float.MinValue;
        var raw = new byte[cols];

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var value = stored[r, c];
                if (value < -128 || value > 127)
                {
                    return 0;
                }
                raw[c] = unchecked((byte)value); // tương đương x & 0xFF
            }

            if (!TryDecodeFloat32(raw, out var segment)) continue;

            var score = CosineSimilarity(segment, probe);
            if (score > best)
            {
                best = score;
            }
        }

        return best == float.MinValue ? 0 : best;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        // Tính bằng double (float64) cho chính xác, giống numpy.
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            double x = a[i], y = b[i];
            dot += x * y;
            normA += x * x;
            normB += y * y;
        }

        if (normA <= 0 || normB <= 0) return 0;

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
