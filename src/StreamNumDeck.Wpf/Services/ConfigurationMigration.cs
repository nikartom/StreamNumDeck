namespace StreamNumDeck.Wpf.Services;

internal static class ConfigurationMigration
{
    public static void TryMigrate(string appDataRoot, string destinationConfigurationRoot)
    {
        var destinationSettings = Path.Combine(destinationConfigurationRoot, "settings.json");
        if (File.Exists(destinationSettings))
        {
            return;
        }

        var legacyUnpackagedSettings = Path.Combine(appDataRoot, "settings.json");
        if (File.Exists(legacyUnpackagedSettings))
        {
            Directory.CreateDirectory(destinationConfigurationRoot);
            File.Copy(legacyUnpackagedSettings, destinationSettings, overwrite: false);
            var legacyBackup = Path.Combine(appDataRoot, "settings.backup.json");
            if (File.Exists(legacyBackup))
            {
                File.Copy(legacyBackup, Path.Combine(destinationConfigurationRoot, "settings.backup.json"), overwrite: false);
            }

            return;
        }

        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");
        if (!Directory.Exists(packagesRoot))
        {
            return;
        }

        var legacyLocalState = Directory
            .EnumerateDirectories(packagesRoot, "*.StreamNumDeck_*")
            .Select(path => Path.Combine(path, "LocalState"))
            .Where(Directory.Exists)
            .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
            .FirstOrDefault(path => File.Exists(Path.Combine(path, "Configuration", "settings.json")));
        if (legacyLocalState is null)
        {
            return;
        }

        CopyDirectory(Path.Combine(legacyLocalState, "Configuration"), destinationConfigurationRoot);
        var legacyAssets = Path.Combine(legacyLocalState, "Assets");
        if (Directory.Exists(legacyAssets))
        {
            CopyDirectory(legacyAssets, Path.Combine(appDataRoot, "Assets"));
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var child in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }
}
