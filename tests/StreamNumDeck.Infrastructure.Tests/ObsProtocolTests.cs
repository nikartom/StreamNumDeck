using StreamNumDeck.Core.Actions;
using StreamNumDeck.Infrastructure.Obs;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class ObsProtocolTests
{
    [TestMethod]
    public void CreateAuthentication_UsesObsWebSocketV5Algorithm()
    {
        var authentication = ObsProtocol.CreateAuthentication(
            "supersecretpassword",
            "+lErt9f2w",
            "748D9B4...");

        Assert.AreEqual("TpFdn1rF0XjcqC2oh/w41N356h6TbPuvb1eHDlsvqfo=", authentication);
    }

    [TestMethod]
    public void MapActionRequest_MapsSceneAndMediaActions()
    {
        var scene = ObsProtocol.MapActionRequest(
            new ObsActionDefinition(ObsActionKind.SwitchScene, "Игра"));
        var media = ObsProtocol.MapActionRequest(
            new ObsActionDefinition(ObsActionKind.RestartMediaSource, "Заставка"));
        var stopMedia = ObsProtocol.MapActionRequest(
            new ObsActionDefinition(ObsActionKind.StopMediaSource, "Заставка"));

        Assert.AreEqual("SetCurrentProgramScene", scene.RequestType);
        Assert.AreEqual("Игра", scene.RequestData!.Value.GetProperty("sceneName").GetString());
        Assert.AreEqual("TriggerMediaInputAction", media.RequestType);
        Assert.AreEqual("Заставка", media.RequestData!.Value.GetProperty("inputName").GetString());
        Assert.AreEqual(
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
            media.RequestData.Value.GetProperty("mediaAction").GetString());
        Assert.AreEqual(
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP",
            stopMedia.RequestData!.Value.GetProperty("mediaAction").GetString());
    }

    [TestMethod]
    [DataRow(ObsActionKind.StartStreaming, "StartStream")]
    [DataRow(ObsActionKind.StopStreaming, "StopStream")]
    [DataRow(ObsActionKind.StartRecording, "StartRecord")]
    [DataRow(ObsActionKind.StopRecording, "StopRecord")]
    [DataRow(ObsActionKind.SaveReplayBuffer, "SaveReplayBuffer")]
    [DataRow(ObsActionKind.ToggleStreaming, "ToggleStream")]
    [DataRow(ObsActionKind.ToggleRecording, "ToggleRecord")]
    [DataRow(ObsActionKind.ToggleRecordingPause, "ToggleRecordPause")]
    [DataRow(ObsActionKind.StartReplayBuffer, "StartReplayBuffer")]
    [DataRow(ObsActionKind.StopReplayBuffer, "StopReplayBuffer")]
    [DataRow(ObsActionKind.ToggleVirtualCamera, "ToggleVirtualCam")]
    [DataRow(ObsActionKind.TriggerStudioModeTransition, "TriggerStudioModeTransition")]
    public void MapActionRequest_MapsTargetlessActions(ObsActionKind actionKind, string expectedRequest)
    {
        var request = ObsProtocol.MapActionRequest(new ObsActionDefinition(actionKind));

        Assert.AreEqual(expectedRequest, request.RequestType);
        Assert.IsNull(request.RequestData);
    }

    [TestMethod]
    [DataRow("OBS_MEDIA_STATE_PLAYING", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE")]
    [DataRow("OBS_MEDIA_STATE_PAUSED", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY")]
    [DataRow("OBS_MEDIA_STATE_STOPPED", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY")]
    public void GetMediaPlayPauseAction_MapsCurrentState(string state, string expectedAction)
    {
        Assert.AreEqual(expectedAction, ObsProtocol.GetMediaPlayPauseAction(state));
    }
}
