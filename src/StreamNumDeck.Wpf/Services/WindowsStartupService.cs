using Microsoft.Win32;

namespace StreamNumDeck.Wpf.Services;

internal static class WindowsStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StreamNumDeck";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Windows startup registry key is unavailable.");
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
