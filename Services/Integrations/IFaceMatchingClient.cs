namespace MeetingBackend.Services;

public interface IFaceMatchingClient
{
    Task<float> ComputeSimilarityAsync(byte[] template1, byte[] template2, CancellationToken cancellationToken = default);
}
