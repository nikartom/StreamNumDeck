using Microsoft.Win32;
using StreamNumDeck.Wpf.Services;

namespace StreamNumDeck.Wpf.Tests;

[TestClass]
public sealed class WindowsStartupServiceTests
{
    private const string ValueName = "StreamNumDeck";

    private string testKey = null!;

    [TestInitialize]
    public void Initialize()
    {
        testKey = $@"Software\StreamNumDeck.Tests\{Guid.NewGuid():N}";
        Registry.CurrentUser.CreateSubKey(testKey)?.Dispose();
    }

    [TestCleanup]
    public void Cleanup()
    {
        Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false);
    }

    [TestMethod]
    public void SetEnabled_RegistersAndRemovesQuotedExecutablePath()
    {
        var executablePath = @"C:\Program Files\StreamNumDeck\StreamNumDeck.exe";

        WindowsStartupService.SetEnabled(
            Registry.CurrentUser,
            testKey,
            ValueName,
            executablePath,
            enabled: true);

        Assert.IsTrue(WindowsStartupService.IsEnabled(
            Registry.CurrentUser,
            testKey,
            ValueName,
            executablePath));
        using (var key = Registry.CurrentUser.OpenSubKey(testKey))
        {
            Assert.AreEqual($"\"{executablePath}\"", key?.GetValue(ValueName));
            Assert.AreEqual(RegistryValueKind.String, key?.GetValueKind(ValueName));
        }

        WindowsStartupService.SetEnabled(
            Registry.CurrentUser,
            testKey,
            ValueName,
            executablePath,
            enabled: false);

        Assert.IsFalse(WindowsStartupService.IsEnabled(
            Registry.CurrentUser,
            testKey,
            ValueName,
            executablePath));
    }

    [TestMethod]
    public void IsEnabled_RejectsRegistrationForAnotherExecutable()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(testKey, writable: true))
        {
            key!.SetValue(ValueName, "\"C:\\Old\\StreamNumDeck.exe\"", RegistryValueKind.String);
        }

        Assert.IsFalse(WindowsStartupService.IsEnabled(
            Registry.CurrentUser,
            testKey,
            ValueName,
            @"C:\Current\StreamNumDeck.exe"));
    }
}
