using System.Text;

namespace StreamNumDeck.Wpf.Services;

internal static class AppLogger
{
    private const long MaximumLogSize = 1024 * 1024;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTimeOffset> RecentEntries = new(StringComparer.Ordinal);

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StreamNumDeck",
        "Logs",
        "StreamNumDeck.log");

    public static void Error(string context, Exception exception)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var signature = $"{context}|{exception.GetType().FullName}|{exception.Message}";
            lock (Sync)
            {
                if (RecentEntries.TryGetValue(signature, out var previous)
                    && now - previous < DuplicateWindow)
                {
                    return;
                }

                RecentEntries[signature] = now;
                if (RecentEntries.Count > 200)
                {
                    RecentEntries.Clear();
                    RecentEntries[signature] = now;
                }

                RotateIfRequired();
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    $"[{now:yyyy-MM-dd HH:mm:ss.fff zzz}] ERROR {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never interrupt input capture or the user interface.
        }
    }

    private static void RotateIfRequired()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaximumLogSize)
        {
            return;
        }

        var backupPath = LogPath + ".1";
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        File.Move(LogPath, backupPath);
    }
}
