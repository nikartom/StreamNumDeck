namespace StreamNumDeck.Infrastructure.Obs;

public sealed class ObsRequestException(
    string requestType,
    int code,
    string? comment) : Exception(
        string.IsNullOrWhiteSpace(comment)
            ? $"OBS rejected {requestType} (code {code})."
            : $"OBS rejected {requestType}: {comment} (code {code}).")
{
    public string RequestType { get; } = requestType;

    public int Code { get; } = code;
}
