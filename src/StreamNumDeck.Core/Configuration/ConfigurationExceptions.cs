namespace StreamNumDeck.Core.Configuration;

public sealed class UnsupportedConfigurationVersionException(int version, int currentVersion)
    : IOException($"Configuration schema version {version} is not supported. Current version: {currentVersion}.")
{
    public int Version { get; } = version;

    public int CurrentVersion { get; } = currentVersion;
}

public sealed class ConfigurationLoadException : IOException
{
    public ConfigurationLoadException(string message, Exception primaryFailure, Exception? backupFailure = null)
        : base(message, backupFailure is null ? primaryFailure : new AggregateException(primaryFailure, backupFailure))
    {
        PrimaryFailure = primaryFailure;
        BackupFailure = backupFailure;
    }

    public Exception PrimaryFailure { get; }

    public Exception? BackupFailure { get; }
}
