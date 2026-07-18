using StreamNumDeck.Core.Icons;
using StreamNumDeck.Infrastructure.Icons;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class FileSystemIconAssetStoreTests
{
    private string testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StreamNumDeck.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ImportAsync_CopiesAssetByContentHashAndDeduplicatesIt()
    {
        var sourcePath = Path.Combine(testDirectory, "source.png");
        File.WriteAllBytes(sourcePath, [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4]);
        var store = new FileSystemIconAssetStore(Path.Combine(testDirectory, "assets"));

        var first = await store.ImportAsync(sourcePath);
        var second = await store.ImportAsync(sourcePath);

        Assert.AreEqual(IconKind.CustomAsset, first.Kind);
        Assert.AreEqual(first, second);
        Assert.IsTrue(File.Exists(store.ResolvePath(first)));
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(testDirectory, "assets", "icons")));
    }

    [TestMethod]
    public async Task ImportAsync_RejectsUnsupportedFormats()
    {
        var sourcePath = Path.Combine(testDirectory, "icon.gif");
        File.WriteAllBytes(sourcePath, [1, 2, 3]);
        var store = new FileSystemIconAssetStore(Path.Combine(testDirectory, "assets"));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(sourcePath));
    }
}
