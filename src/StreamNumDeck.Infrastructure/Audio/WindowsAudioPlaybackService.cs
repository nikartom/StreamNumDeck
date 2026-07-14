using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.Render;
using Windows.Storage;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Execution;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Infrastructure.Audio;

public sealed class WindowsAudioPlaybackService : IAudioPlaybackService
{
    private static readonly TimeSpan MaximumPreloadedDuration = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim graphGate = new(1, 1);
    private readonly object activeSoundsGate = new();
    private readonly Dictionary<AudioFileInputNode, string> activeSounds = [];
    private readonly Dictionary<string, AudioFileInputNode> preloadedSounds =
        new(StringComparer.OrdinalIgnoreCase);
    private AudioGraph? graph;
    private AudioDeviceOutputNode? outputNode;
    private string? graphDeviceKey;
    private volatile bool graphInvalidated;
    private bool disposed;

    public async Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
        cancellationToken.ThrowIfCancellationRequested();
        var defaultDeviceId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);

        var result = new List<AudioOutputDevice>(devices.Count + 1)
        {
            new(null, "System default device", true),
        };
        result.AddRange(devices
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new AudioOutputDevice(
                device.Id,
                device.Name,
                string.Equals(device.Id, defaultDeviceId, StringComparison.Ordinal))));
        return result;
    }

    public async Task PlayAsync(
        PlaySoundActionDefinition action,
        GlobalSettings settings,
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        Guard.NotNull(action, nameof(action));
        Guard.NotNull(settings, nameof(settings));

        var normalizedPath = Path.GetFullPath(action.FilePath);
        if (!File.Exists(normalizedPath))
        {
            throw new UserActionException(UserActionError.TargetUnavailable, normalizedPath);
        }

        await graphGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureGraphAsync(settings.AudioOutputDeviceId, cancellationToken).ConfigureAwait(false);

            if (!settings.AllowConcurrentSounds || action.PlaybackBehavior is SoundPlaybackBehavior.StopOthers)
            {
                StopSounds(static (_, _) => true);
            }
            else if (action.PlaybackBehavior is SoundPlaybackBehavior.RestartSameSound)
            {
                StopSounds((_, path) => string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase));
            }

            var wasPreloaded = preloadedSounds.TryGetValue(normalizedPath, out var node);
            if (wasPreloaded)
            {
                preloadedSounds.Remove(normalizedPath);
            }
            node ??= await CreateFileNodeAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            node.OutgoingGain = action.Volume / 100d * settings.MasterVolume / 100d;
            node.FileCompleted += FileInputNode_FileCompleted;
            lock (activeSoundsGate)
            {
                activeSounds.Add(node, normalizedPath);
            }

            node.Start();
            if (settings.PreloadShortSounds && (wasPreloaded || node.Duration <= MaximumPreloadedDuration))
            {
                _ = ReplenishPreloadedNodeAsync(normalizedPath, graphDeviceKey);
            }
        }
        finally
        {
            graphGate.Release();
        }
    }

    public async Task PreloadAsync(
        IEnumerable<PlaySoundActionDefinition> actions,
        GlobalSettings settings,
        CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        Guard.NotNull(actions, nameof(actions));
        Guard.NotNull(settings, nameof(settings));

        var paths = actions
            .Select(static action => Path.GetFullPath(action.FilePath))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedPaths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        await graphGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!settings.PreloadShortSounds)
            {
                DisposePreloadedSounds();
                return;
            }

            foreach (var stalePath in preloadedSounds.Keys.Where(path => !expectedPaths.Contains(path)).ToArray())
            {
                preloadedSounds[stalePath].Dispose();
                preloadedSounds.Remove(stalePath);
            }

            if (paths.Length == 0)
            {
                return;
            }

            await EnsureGraphAsync(settings.AudioOutputDeviceId, cancellationToken).ConfigureAwait(false);
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (preloadedSounds.ContainsKey(path))
                {
                    continue;
                }

                AudioFileInputNode? node = null;
                try
                {
                    node = await CreateFileNodeAsync(path, cancellationToken).ConfigureAwait(false);
                    if (node.Duration <= MaximumPreloadedDuration)
                    {
                        preloadedSounds.Add(path, node);
                        node = null;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Preloading is an optimization. Playback reports a detailed
                    // error later if the same file still cannot be opened.
                }
                finally
                {
                    node?.Dispose();
                }
            }
        }
        finally
        {
            graphGate.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        await graphGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopSounds(static (_, _) => true);
        }
        finally
        {
            graphGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await graphGate.WaitAsync().ConfigureAwait(false);
        try
        {
            StopSounds(static (_, _) => true);
            DisposePreloadedSounds();
            outputNode?.Dispose();
            outputNode = null;
            if (graph is not null)
            {
                graph.UnrecoverableErrorOccurred -= Graph_UnrecoverableErrorOccurred;
            }

            graph?.Dispose();
            graph = null;
            disposed = true;
        }
        finally
        {
            graphGate.Release();
            graphGate.Dispose();
        }
    }

    private async Task EnsureGraphAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var deviceKey = deviceId ?? $"default:{MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default)}";
        if (graph is not null
            && !graphInvalidated
            && string.Equals(graphDeviceKey, deviceKey, StringComparison.Ordinal))
        {
            return;
        }

        StopSounds(static (_, _) => true);
        DisposePreloadedSounds();
        outputNode?.Dispose();
        outputNode = null;
        if (graph is not null)
        {
            graph.UnrecoverableErrorOccurred -= Graph_UnrecoverableErrorOccurred;
        }

        graph?.Dispose();
        graph = null;

        var graphSettings = new AudioGraphSettings(AudioRenderCategory.Media)
        {
            QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency,
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            graphSettings.PrimaryRenderDevice = await DeviceInformation.CreateFromIdAsync(deviceId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var graphResult = await AudioGraph.CreateAsync(graphSettings);
        if (graphResult.Status is not AudioGraphCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"Не удалось создать аудиодвижок: {graphResult.Status}. {graphResult.ExtendedError?.Message}");
        }

        graph = graphResult.Graph;
        graph.UnrecoverableErrorOccurred += Graph_UnrecoverableErrorOccurred;
        var outputResult = await graph.CreateDeviceOutputNodeAsync();
        if (outputResult.Status is not AudioDeviceNodeCreationStatus.Success)
        {
            graph.UnrecoverableErrorOccurred -= Graph_UnrecoverableErrorOccurred;
            graph.Dispose();
            graph = null;
            throw new InvalidOperationException(
                $"Не удалось открыть устройство вывода: {outputResult.Status}. {outputResult.ExtendedError?.Message}");
        }

        outputNode = outputResult.DeviceOutputNode;
        graphDeviceKey = deviceKey;
        graphInvalidated = false;
        graph.Start();
    }

    private void Graph_UnrecoverableErrorOccurred(
        AudioGraph sender,
        AudioGraphUnrecoverableErrorOccurredEventArgs args)
    {
        graphInvalidated = true;
    }

    private async Task<AudioFileInputNode> CreateFileNodeAsync(
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(normalizedPath);
        cancellationToken.ThrowIfCancellationRequested();
        var creationResult = await graph!.CreateFileInputNodeAsync(file);
        if (creationResult.Status is not AudioFileNodeCreationStatus.Success)
        {
            throw new UserActionException(
                UserActionError.AudioFileUnsupported,
                normalizedPath,
                creationResult.ExtendedError);
        }

        creationResult.FileInputNode.AddOutgoingConnection(outputNode!);
        return creationResult.FileInputNode;
    }

    private async Task ReplenishPreloadedNodeAsync(string normalizedPath, string? expectedDeviceKey)
    {
        try
        {
            await graphGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (disposed
                    || graphInvalidated
                    || !string.Equals(graphDeviceKey, expectedDeviceKey, StringComparison.Ordinal)
                    || preloadedSounds.ContainsKey(normalizedPath)
                    || !File.Exists(normalizedPath))
                {
                    return;
                }

                var node = await CreateFileNodeAsync(normalizedPath, CancellationToken.None).ConfigureAwait(false);
                if (node.Duration <= MaximumPreloadedDuration)
                {
                    preloadedSounds.Add(normalizedPath, node);
                }
                else
                {
                    node.Dispose();
                }
            }
            finally
            {
                graphGate.Release();
            }
        }
        catch
        {
            // Playback has already started. A failed cache refill must not
            // surface as an unobserved task exception.
        }
    }

    private void FileInputNode_FileCompleted(AudioFileInputNode sender, object args) => CompleteSound(sender);

    private void CompleteSound(AudioFileInputNode node)
    {
        lock (activeSoundsGate)
        {
            if (!activeSounds.Remove(node))
            {
                return;
            }
        }

        node.FileCompleted -= FileInputNode_FileCompleted;
        node.Dispose();
    }

    private void StopSounds(Func<AudioFileInputNode, string, bool> predicate)
    {
        List<AudioFileInputNode> nodes;
        lock (activeSoundsGate)
        {
            nodes = activeSounds
                .Where(pair => predicate(pair.Key, pair.Value))
                .Select(static pair => pair.Key)
                .ToList();
            foreach (var node in nodes)
            {
                activeSounds.Remove(node);
            }
        }

        foreach (var node in nodes)
        {
            node.FileCompleted -= FileInputNode_FileCompleted;
            node.Stop();
            node.Dispose();
        }
    }

    private void DisposePreloadedSounds()
    {
        foreach (var node in preloadedSounds.Values)
        {
            node.Dispose();
        }

        preloadedSounds.Clear();
    }
}
