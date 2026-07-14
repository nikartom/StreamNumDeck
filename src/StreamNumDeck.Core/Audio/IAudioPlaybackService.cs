using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Core.Audio;

public sealed record AudioOutputDevice(string? Id, string Name, bool IsSystemDefault);

public interface IAudioPlaybackService : IAsyncDisposable
{
    Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync(CancellationToken cancellationToken = default);

    Task PlayAsync(
        PlaySoundActionDefinition action,
        GlobalSettings settings,
        CancellationToken cancellationToken = default);

    Task PreloadAsync(
        IEnumerable<PlaySoundActionDefinition> actions,
        GlobalSettings settings,
        CancellationToken cancellationToken = default);

    Task StopAllAsync(CancellationToken cancellationToken = default);
}
