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

        Assert.AreEqual("SetCurrentProgramScene", scene.RequestType);
        Assert.AreEqual("Игра", scene.RequestData!.Value.GetProperty("sceneName").GetString());
        Assert.AreEqual("TriggerMediaInputAction", media.RequestType);
        Assert.AreEqual("Заставка", media.RequestData!.Value.GetProperty("inputName").GetString());
        Assert.AreEqual(
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
            media.RequestData.Value.GetProperty("mediaAction").GetString());
    }

    [TestMethod]
    [DataRow(ObsActionKind.StartStreaming, "StartStream")]
    [DataRow(ObsActionKind.StopStreaming, "StopStream")]
    [DataRow(ObsActionKind.StartRecording, "StartRecord")]
    [DataRow(ObsActionKind.StopRecording, "StopRecord")]
    [DataRow(ObsActionKind.SaveReplayBuffer, "SaveReplayBuffer")]
    public void MapActionRequest_MapsTargetlessActions(ObsActionKind actionKind, string expectedRequest)
    {
        var request = ObsProtocol.MapActionRequest(new ObsActionDefinition(actionKind));

        Assert.AreEqual(expectedRequest, request.RequestType);
        Assert.IsNull(request.RequestData);
    }
}
