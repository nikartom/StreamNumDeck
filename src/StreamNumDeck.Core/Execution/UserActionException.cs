namespace StreamNumDeck.Core.Execution;

public enum UserActionError
{
    TargetUnavailable,
    AudioSessionUnavailable,
    ObsUnavailable,
    ObsActionRejected,
    VTubeStudioUnavailable,
    VTubeStudioActionRejected,
    AudioFileUnsupported,
}

public sealed class UserActionException : Exception
{
    public UserActionException(
        UserActionError error,
        string? subject = null,
        Exception? innerException = null)
        : base(CreateDiagnosticMessage(error, subject), innerException)
    {
        Error = error;
        Subject = subject;
    }

    public UserActionError Error { get; }

    public string? Subject { get; }

    private static string CreateDiagnosticMessage(UserActionError error, string? subject) => error switch
    {
        UserActionError.TargetUnavailable => $"The configured target is unavailable: {subject}",
        UserActionError.AudioSessionUnavailable => $"The configured application has no active audio session: {subject}",
        UserActionError.ObsUnavailable => "OBS is not connected.",
        UserActionError.ObsActionRejected => $"OBS rejected the configured action: {subject}",
        UserActionError.VTubeStudioUnavailable => "VTube Studio is not connected.",
        UserActionError.VTubeStudioActionRejected => $"VTube Studio rejected the configured action: {subject}",
        UserActionError.AudioFileUnsupported => $"Windows could not play the configured audio file: {subject}",
        _ => "The configured action cannot be completed.",
    };
}
