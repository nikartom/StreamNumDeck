using System.Diagnostics;
using Microsoft.Win32;

namespace StreamNumDeck.Wpf.Services;

internal static class WindowsStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StreamNumDeck";

    public static bool IsEnabled() =>
        IsEnabled(Registry.CurrentUser, RunKey, ValueName, GetExecutablePath());

    public static void SetEnabled(bool enabled) =>
        SetEnabled(Registry.CurrentUser, RunKey, ValueName, GetExecutablePath(), enabled);

    internal static bool IsEnabled(
        RegistryKey root,
        string runKey,
        string valueName,
        string executablePath)
    {
        using var key = root.OpenSubKey(runKey, writable: false);
        return key?.GetValue(valueName) is string command
            && string.Equals(command, BuildCommand(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetEnabled(
        RegistryKey root,
        string runKey,
        string valueName,
        string executablePath,
        bool enabled)
    {
        using var key = root.OpenSubKey(runKey, writable: true)
            ?? throw new InvalidOperationException("Windows startup registry key is unavailable.");
        if (enabled)
        {
            key.SetValue(valueName, BuildCommand(executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static string GetExecutablePath() =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("The application executable path is unavailable.");

    private static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("The executable path is required.", nameof(executablePath));
        }

        return $"\"{Path.GetFullPath(executablePath)}\"";
    }
}
