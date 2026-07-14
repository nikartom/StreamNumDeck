using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StreamNumDeck.Infrastructure.Obs;

internal static class ObsProtocol
{
    public const int RpcVersion = 1;

    public static string CreateAuthentication(string password, string salt, string challenge)
    {
        Guard.NotNull(password, nameof(password));
        Guard.NotNullOrWhiteSpace(salt, nameof(salt));
        Guard.NotNullOrWhiteSpace(challenge, nameof(challenge));

        using var algorithm = SHA256.Create();
        var secret = Convert.ToBase64String(
            algorithm.ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(
            algorithm.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public static (string RequestType, JsonElement? RequestData) MapActionRequest(
        StreamNumDeck.Core.Actions.ObsActionDefinition action) => action.Action switch
    {
        StreamNumDeck.Core.Actions.ObsActionKind.SwitchScene =>
            ("SetCurrentProgramScene", JsonSerializer.SerializeToElement(new { sceneName = action.TargetName })),
        StreamNumDeck.Core.Actions.ObsActionKind.ToggleInputMute =>
            ("ToggleInputMute", JsonSerializer.SerializeToElement(new { inputName = action.TargetName })),
        StreamNumDeck.Core.Actions.ObsActionKind.StartStreaming => ("StartStream", null),
        StreamNumDeck.Core.Actions.ObsActionKind.StopStreaming => ("StopStream", null),
        StreamNumDeck.Core.Actions.ObsActionKind.StartRecording => ("StartRecord", null),
        StreamNumDeck.Core.Actions.ObsActionKind.StopRecording => ("StopRecord", null),
        StreamNumDeck.Core.Actions.ObsActionKind.SaveReplayBuffer => ("SaveReplayBuffer", null),
        StreamNumDeck.Core.Actions.ObsActionKind.RestartMediaSource =>
            ("TriggerMediaInputAction", JsonSerializer.SerializeToElement(new
            {
                inputName = action.TargetName,
                mediaAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
            })),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.Action, "Unsupported direct OBS action."),
    };
}
