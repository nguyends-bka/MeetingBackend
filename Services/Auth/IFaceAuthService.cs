using MeetingBackend.Entities;

namespace MeetingBackend.Services;

public interface IFaceAuthService
{
    Task<FaceAuthResult> AuthenticateAsync(int[] embedding, CancellationToken cancellationToken = default);
}

public sealed class FaceAuthResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public User? User { get; init; }
    public float BestScore { get; init; }
}
