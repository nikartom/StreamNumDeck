using StreamNumDeck.Core.Actions;

namespace StreamNumDeck.Core.Audio;

public sealed record AudioApplication(string Id, string Name);

public interface ISystemAudioControlService
{
    Task<IReadOnlyList<AudioApplication>> GetApplicationsAsync(
        CancellationToken cancellationToken = default);

    Task ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default);

    Task<bool> GetDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default);

    Task ToggleMasterOutputMuteAsync(CancellationToken cancellationToken = default);

    Task AdjustMasterOutputVolumeAsync(
        VolumeAdjustmentDirection direction,
        int stepPercent,
        CancellationToken cancellationToken = default);

    Task AdjustApplicationVolumeAsync(
        string applicationId,
        VolumeAdjustmentDirection direction,
        int stepPercent,
        CancellationToken cancellationToken = default);
}
