using System.Diagnostics;
using System.Runtime.InteropServices;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Audio;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Audio;

public sealed class WindowsSystemAudioControlService : ISystemAudioControlService
{
    private const uint ClsctxAll = 23;
    private static readonly Guid AudioEndpointVolumeId = typeof(IAudioEndpointVolume).GUID;
    private static readonly Guid AudioSessionManager2Id = typeof(IAudioSessionManager2).GUID;

    public Task<IReadOnlyList<AudioApplication>> GetApplicationsAsync(
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<AudioApplication>>(
            () => EnumerateApplications(cancellationToken),
            cancellationToken);

    public Task ToggleDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            WithEndpointVolume(DataFlow.Capture, Role.Console, volume =>
            {
                ThrowIfFailed(volume.GetMute(out var muted));
                var context = Guid.Empty;
                ThrowIfFailed(volume.SetMute(!muted, ref context));
            });
        }, cancellationToken);

    public Task<bool> GetDefaultMicrophoneMuteAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = false;
            WithEndpointVolume(DataFlow.Capture, Role.Console, volume =>
            {
                ThrowIfFailed(volume.GetMute(out result));
            });
            return result;
        }, cancellationToken);

    public Task ToggleMasterOutputMuteAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            WithEndpointVolume(DataFlow.Render, Role.Multimedia, volume =>
            {
                ThrowIfFailed(volume.GetMute(out var muted));
                var context = Guid.Empty;
                ThrowIfFailed(volume.SetMute(!muted, ref context));
            });
        }, cancellationToken);

    public Task AdjustMasterOutputVolumeAsync(
        VolumeAdjustmentDirection direction,
        int stepPercent,
        CancellationToken cancellationToken = default) => Task.Run(() =>
        {
            ValidateStep(stepPercent);
            cancellationToken.ThrowIfCancellationRequested();
            WithEndpointVolume(DataFlow.Render, Role.Multimedia, volume =>
            {
                ThrowIfFailed(volume.GetMasterVolumeLevelScalar(out var current));
                var updated = ApplyAdjustment(current, direction, stepPercent);
                var context = Guid.Empty;
                ThrowIfFailed(volume.SetMasterVolumeLevelScalar(updated, ref context));
                if (direction is VolumeAdjustmentDirection.Increase)
                {
                    ThrowIfFailed(volume.SetMute(false, ref context));
                }
            });
        }, cancellationToken);

    public Task AdjustApplicationVolumeAsync(
        string applicationId,
        VolumeAdjustmentDirection direction,
        int stepPercent,
        CancellationToken cancellationToken = default) => Task.Run(() =>
        {
            ValidateStep(stepPercent);
            var normalizedId = AdjustApplicationVolumeActionDefinition.NormalizeApplicationId(applicationId);
            cancellationToken.ThrowIfCancellationRequested();

            var adjustedSessions = 0;
            WithSessionEnumerator(sessionEnumerator =>
            {
                ThrowIfFailed(sessionEnumerator.GetCount(out var count));
                for (var index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfFailed(sessionEnumerator.GetSession(index, out var session));
                    try
                    {
                        if (session is not IAudioSessionControl2 session2
                            || session is not ISimpleAudioVolume simpleVolume
                            || !TryGetApplicationId(session2, out var sessionApplicationId)
                            || !string.Equals(sessionApplicationId, normalizedId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ThrowIfFailed(simpleVolume.GetMasterVolume(out var current));
                        var updated = ApplyAdjustment(current, direction, stepPercent);
                        var context = Guid.Empty;
                        ThrowIfFailed(simpleVolume.SetMasterVolume(updated, ref context));
                        if (direction is VolumeAdjustmentDirection.Increase)
                        {
                            ThrowIfFailed(simpleVolume.SetMute(false, ref context));
                        }

                        adjustedSessions++;
                    }
                    finally
                    {
                        ReleaseComObject(session);
                    }
                }
            });

            if (adjustedSessions == 0)
            {
                throw new UserActionException(UserActionError.AudioSessionUnavailable, normalizedId);
            }
        }, cancellationToken);

    private static IReadOnlyList<AudioApplication> EnumerateApplications(CancellationToken cancellationToken)
    {
        var applications = new Dictionary<string, AudioApplication>(StringComparer.OrdinalIgnoreCase);
        WithSessionEnumerator(sessionEnumerator =>
        {
            ThrowIfFailed(sessionEnumerator.GetCount(out var count));
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfFailed(sessionEnumerator.GetSession(index, out var session));
                try
                {
                    if (session is not IAudioSessionControl2 session2
                        || !TryGetProcess(session2, out var process))
                    {
                        continue;
                    }

                    using (process)
                    {
                        var id = $"{process.ProcessName}.exe".ToLowerInvariant();
                        var description = TryGetProcessDescription(process);
                        var name = string.IsNullOrWhiteSpace(description)
                            ? id
                            : $"{description} ({id})";
                        if (!applications.ContainsKey(id))
                        {
                            applications.Add(id, new AudioApplication(id, name));
                        }
                    }
                }
                finally
                {
                    ReleaseComObject(session);
                }
            }
        });

        return applications.Values
            .OrderBy(static application => application.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void WithEndpointVolume(DataFlow flow, Role role, Action<IAudioEndpointVolume> action)
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? endpoint = null;
        object? activated = null;
        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            ThrowIfFailed(deviceEnumerator.GetDefaultAudioEndpoint(flow, role, out endpoint));
            var interfaceId = AudioEndpointVolumeId;
            ThrowIfFailed(endpoint.Activate(ref interfaceId, ClsctxAll, 0, out activated));
            action((IAudioEndpointVolume)activated);
        }
        finally
        {
            ReleaseComObject(activated);
            ReleaseComObject(endpoint);
            ReleaseComObject(deviceEnumerator);
        }
    }

    private static void WithSessionEnumerator(Action<IAudioSessionEnumerator> action)
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? endpoint = null;
        object? activated = null;
        IAudioSessionEnumerator? sessionEnumerator = null;
        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            ThrowIfFailed(deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out endpoint));
            var interfaceId = AudioSessionManager2Id;
            ThrowIfFailed(endpoint.Activate(ref interfaceId, ClsctxAll, 0, out activated));
            var sessionManager = (IAudioSessionManager2)activated;
            ThrowIfFailed(sessionManager.GetSessionEnumerator(out sessionEnumerator));
            action(sessionEnumerator);
        }
        finally
        {
            ReleaseComObject(sessionEnumerator);
            ReleaseComObject(activated);
            ReleaseComObject(endpoint);
            ReleaseComObject(deviceEnumerator);
        }
    }

    private static bool TryGetApplicationId(IAudioSessionControl2 session, out string applicationId)
    {
        applicationId = string.Empty;
        if (!TryGetProcess(session, out var process))
        {
            return false;
        }

        using (process)
        {
            applicationId = $"{process.ProcessName}.exe".ToLowerInvariant();
            return true;
        }
    }

    private static bool TryGetProcess(IAudioSessionControl2 session, out Process process)
    {
        process = null!;
        try
        {
            ThrowIfFailed(session.GetProcessId(out var processId));
            if (processId == 0)
            {
                return false;
            }

            process = Process.GetProcessById(checked((int)processId));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string? TryGetProcessDescription(Process process)
    {
        try
        {
            return process.MainModule?.FileVersionInfo.FileDescription;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static float ApplyAdjustment(
        float current,
        VolumeAdjustmentDirection direction,
        int stepPercent)
    {
        var delta = stepPercent / 100f;
        var adjusted = direction is VolumeAdjustmentDirection.Increase ? current + delta : current - delta;
        return Math.Max(0f, Math.Min(1f, adjusted));
    }

    private static void ValidateStep(int value)
    {
        if (value is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The volume step must be between 1 and 100 percent.");
        }
    }

    private static void ThrowIfFailed(int result) => Marshal.ThrowExceptionForHR(result);

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private enum DataFlow
    {
        Render,
        Capture,
        All,
    }

    private enum Role
    {
        Console,
        Multimedia,
        Communications,
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(nint client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(nint client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            nint activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);

        [PreserveSig] int OpenPropertyStore(uint access, out nint properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint notify);
        [PreserveSig] int UnregisterControlChangeNotify(nint notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid sessionId, uint flags, out nint sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid sessionId, uint flags, out nint simpleVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        [PreserveSig] int RegisterSessionNotification(nint notification);
        [PreserveSig] int UnregisterSessionNotification(nint notification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, nint notification);
        [PreserveSig] int UnregisterDuckNotification(nint notification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingId);
        [PreserveSig] int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(nint events);
        [PreserveSig] int UnregisterAudioSessionNotification(nint events);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2 : IAudioSessionControl
    {
        [PreserveSig] new int GetState(out int state);
        [PreserveSig] new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] new int GetGroupingParam(out Guid groupingId);
        [PreserveSig] new int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        [PreserveSig] new int RegisterAudioSessionNotification(nint events);
        [PreserveSig] new int UnregisterAudioSessionNotification(nint events);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
