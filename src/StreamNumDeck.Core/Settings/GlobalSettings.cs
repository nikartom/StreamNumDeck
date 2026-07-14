namespace StreamNumDeck.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record ObsConnectionSettings
{
    public ObsConnectionSettings(string host, int port, string credentialKey)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("An OBS host is required.", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The OBS port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(credentialKey))
        {
            throw new ArgumentException("A secure credential key is required.", nameof(credentialKey));
        }

        Host = host.Trim();
        Port = port;
        CredentialKey = credentialKey.Trim();
    }

    public string Host { get; }

    public int Port { get; }

    public string CredentialKey { get; }

    public static ObsConnectionSettings Default { get; } = new("127.0.0.1", 4455, "obs.default");
}

public sealed record GlobalSettings
{
    public GlobalSettings(
        string? audioOutputDeviceId,
        int masterVolume,
        bool allowConcurrentSounds,
        bool preloadShortSounds,
        bool startWithWindows,
        bool minimizeToTray,
        bool enableCaptureOnStartup,
        AppTheme theme,
        ObsConnectionSettings obs)
    {
        if (masterVolume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(masterVolume), "Master volume must be between 0 and 100.");
        }

        obs = Guard.NotNull(obs, nameof(obs));

        AudioOutputDeviceId = string.IsNullOrWhiteSpace(audioOutputDeviceId) ? null : audioOutputDeviceId!.Trim();
        MasterVolume = masterVolume;
        AllowConcurrentSounds = allowConcurrentSounds;
        PreloadShortSounds = preloadShortSounds;
        StartWithWindows = startWithWindows;
        MinimizeToTray = minimizeToTray;
        EnableCaptureOnStartup = enableCaptureOnStartup;
        Theme = theme;
        Obs = obs;
    }

    public string? AudioOutputDeviceId { get; }

    public int MasterVolume { get; }

    public bool AllowConcurrentSounds { get; }

    public bool PreloadShortSounds { get; }

    public bool StartWithWindows { get; }

    public bool MinimizeToTray { get; }

    public bool EnableCaptureOnStartup { get; }

    public bool CaptureNumpad { get; init; } = true;

    public bool CaptureNavigationBlock { get; init; } = true;

    public AppTheme Theme { get; }

    public ObsConnectionSettings Obs { get; }

    public static GlobalSettings Default { get; } = new(
        null,
        85,
        allowConcurrentSounds: true,
        preloadShortSounds: true,
        startWithWindows: false,
        minimizeToTray: true,
        enableCaptureOnStartup: true,
        AppTheme.System,
        ObsConnectionSettings.Default);
}
