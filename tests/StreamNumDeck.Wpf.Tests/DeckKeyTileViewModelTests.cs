using System.Windows;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Wpf.Presentation;
using StreamNumDeck.Wpf.ViewModels;

namespace StreamNumDeck.Wpf.Tests;

[TestClass]
public sealed class DeckKeyTileViewModelTests
{
    [TestMethod]
    public void Update_NoAction_ShowsPhysicalLabelAndAddIcon()
    {
        var tile = new DeckKeyTileViewModel(DeckKey.Numpad7);
        var assignment = new KeyAssignment(
            "Старая подпись",
            IconReference.BuiltIn("play"),
            new NoActionDefinition());

        tile.Update(assignment, new StubIconAssetStore());

        Assert.AreEqual("7", tile.Label);
        Assert.AreEqual(DeckPresentation.GetGlyph("plus"), tile.Glyph);
        Assert.AreEqual(Visibility.Visible, tile.LabelVisibility);
        Assert.AreEqual(Visibility.Visible, tile.GlyphVisibility);
        Assert.AreEqual(Visibility.Visible, tile.IconAreaVisibility);
        Assert.AreEqual(Visibility.Collapsed, tile.CustomIconVisibility);
    }

    [TestMethod]
    public void Update_Emoji_ShowsColorEmojiImage()
    {
        var tile = new DeckKeyTileViewModel(DeckKey.Numpad7);
        var assignment = new KeyAssignment(
            "Праздник",
            IconReference.Emoji("🎉"),
            new OpenUriActionDefinition("https://example.com"));

        tile.Update(assignment, new StubIconAssetStore());

        Assert.AreEqual("🎉", tile.Glyph);
        Assert.AreEqual("Segoe UI Emoji", tile.GlyphFontFamily);
        Assert.AreEqual(Visibility.Collapsed, tile.GlyphVisibility);
        Assert.AreEqual(Visibility.Visible, tile.CustomIconVisibility);
        Assert.IsNotNull(tile.CustomIconSource);
    }

    [TestMethod]
    public void EmojiCatalog_AllOptionsHavePackagedImages()
    {
        Assert.HasCount(153, EmojiCatalog.Options);
        Assert.HasCount(
            EmojiCatalog.Options.Count,
            EmojiCatalog.Options.Select(option => option.Emoji).Distinct(StringComparer.Ordinal));

        foreach (var option in EmojiCatalog.Options)
        {
            Assert.IsNotNull(
                EmojiCatalog.TryLoadImage(option.Emoji),
                $"Missing packaged image for {option.Emoji} ({option.Name}).");
        }
    }

    private sealed class StubIconAssetStore : IIconAssetStore
    {
        public Task<IconReference> ImportAsync(
            string sourceFilePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string ResolvePath(IconReference icon) => string.Empty;
    }
}
