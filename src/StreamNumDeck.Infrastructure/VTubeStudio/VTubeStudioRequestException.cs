namespace StreamNumDeck.Infrastructure.VTubeStudio;

public sealed class VTubeStudioRequestException(
    string requestType,
    int errorId,
    string? details = null) : Exception(
        string.IsNullOrWhiteSpace(details)
            ? $"VTube Studio request {requestType} failed with error {errorId}."
            : $"VTube Studio request {requestType} failed with error {errorId}: {details}")
{
    public string RequestType { get; } = requestType;

    public int ErrorId { get; } = errorId;
}
