using System.Collections.Immutable;
using StreamNumDeck.Core.Actions;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class KeyboardMacroTests
{
    [TestMethod]
    public void TextCodec_ParsesAndFormatsSequence()
    {
        var steps = KeyboardMacroTextCodec.Parse(
            """
            Ctrl+Shift+S
            wait 250
            Alt+Tab
            MediaPlayPause
            """);

        Assert.HasCount(4, steps);
        var firstChord = steps[0].Chord!;
        Assert.AreEqual(MacroKey.S, firstChord.Key);
        Assert.AreEqual(MacroModifiers.Control | MacroModifiers.Shift, firstChord.Modifiers);
        Assert.AreEqual(250, steps[1].DelayMilliseconds);
        var thirdChord = steps[2].Chord!;
        Assert.AreEqual(MacroKey.Tab, thirdChord.Key);
        Assert.AreEqual(MacroModifiers.Alt, thirdChord.Modifiers);
        Assert.AreEqual(MacroKey.MediaPlayPause, steps[3].Chord!.Key);

        Assert.AreEqual(
            $"Ctrl+Shift+S{Environment.NewLine}wait 250{Environment.NewLine}Alt+Tab{Environment.NewLine}MediaPlayPause",
            KeyboardMacroTextCodec.Format(steps));
    }

    [TestMethod]
    public void TextCodec_SupportsRussianDelayAndComments()
    {
        var steps = KeyboardMacroTextCodec.Parse(
            """
            # Открыть окно
            Win+1
            пауза 100
            Enter
            """);

        Assert.HasCount(3, steps);
        var firstChord = steps[0].Chord!;
        Assert.AreEqual(MacroKey.Digit1, firstChord.Key);
        Assert.AreEqual(MacroModifiers.Windows, firstChord.Modifiers);
        Assert.AreEqual(100, steps[1].DelayMilliseconds);
    }

    [TestMethod]
    public void TextCodec_ReportsLineForInvalidKey()
    {
        var exception = Assert.ThrowsExactly<KeyboardMacroFormatException>(() =>
            KeyboardMacroTextCodec.Parse("Ctrl+S\nCtrl+NotAKey"));

        Assert.AreEqual(KeyboardMacroFormatError.InvalidLine, exception.Error);
        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Message, "Line 2");
    }

    [TestMethod]
    public void Definition_RejectsEmptyAndOverlongMacros()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new KeyboardMacroActionDefinition([]));

        var steps = Enumerable
            .Repeat(KeyboardMacroStep.KeyPress(MacroKey.A), KeyboardMacroActionDefinition.MaximumSteps + 1)
            .ToImmutableArray();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KeyboardMacroActionDefinition(steps));
    }
}
