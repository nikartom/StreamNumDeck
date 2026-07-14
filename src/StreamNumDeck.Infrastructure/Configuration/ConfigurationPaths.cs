namespace StreamNumDeck.Infrastructure.Configuration;

public sealed record ConfigurationPaths
{
    public ConfigurationPaths(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A configuration directory is required.", nameof(directoryPath));
        }

        DirectoryPath = Path.GetFullPath(directoryPath);
        ConfigurationFilePath = Path.Combine(DirectoryPath, "settings.json");
        BackupFilePath = Path.Combine(DirectoryPath, "settings.backup.json");
    }

    public string DirectoryPath { get; }

    public string ConfigurationFilePath { get; }

    public string BackupFilePath { get; }

    public static ConfigurationPaths CreateDefault() => new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StreamNumDeck"));
}
