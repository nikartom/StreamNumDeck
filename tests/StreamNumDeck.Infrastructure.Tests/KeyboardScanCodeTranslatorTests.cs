using StreamNumDeck.Core.Deck;
using StreamNumDeck.Infrastructure.Input;

namespace StreamNumDeck.Infrastructure.Tests;

[TestClass]
public sealed class KeyboardScanCodeTranslatorTests
{
    [TestMethod]
    public void TranslatesEveryAssignablePhysicalKey()
    {
        (uint ScanCode, bool Extended, DeckKey Key)[] cases =
        [
            (0x52, true, DeckKey.Insert),
            (0x47, true, DeckKey.Home),
            (0x49, true, DeckKey.PageUp),
            (0x53, true, DeckKey.Delete),
            (0x4F, true, DeckKey.End),
            (0x51, true, DeckKey.PageDown),
            (0x35, true, DeckKey.NumpadDivide),
            (0x37, false, DeckKey.NumpadMultiply),
            (0x4A, false, DeckKey.NumpadSubtract),
            (0x47, false, DeckKey.Numpad7),
            (0x48, false, DeckKey.Numpad8),
            (0x49, false, DeckKey.Numpad9),
            (0x4E, false, DeckKey.NumpadAdd),
            (0x4B, false, DeckKey.Numpad4),
            (0x4C, false, DeckKey.Numpad5),
            (0x4D, false, DeckKey.Numpad6),
            (0x4F, false, DeckKey.Numpad1),
            (0x50, false, DeckKey.Numpad2),
            (0x51, false, DeckKey.Numpad3),
            (0x1C, true, DeckKey.NumpadEnter),
            (0x52, false, DeckKey.Numpad0),
            (0x53, false, DeckKey.NumpadDecimal),
        ];

        foreach (var testCase in cases)
        {
            var translated = KeyboardScanCodeTranslator.TryTranslate(
                testCase.ScanCode,
                testCase.Extended,
                out var key);

            Assert.IsTrue(translated, $"Scan code 0x{testCase.ScanCode:X2} was not translated.");
            Assert.AreEqual(testCase.Key, key);
        }

        CollectionAssert.AreEquivalent(
            DeckKeyCatalog.AssignableKeys.ToArray(),
            cases.Select(testCase => testCase.Key).ToArray());
    }

    [TestMethod]
    public void ExtendedFlagSeparatesNavigationBlockFromNumpad()
    {
        Assert.IsTrue(KeyboardScanCodeTranslator.TryTranslate(0x52, true, out var navigationKey));
        Assert.IsTrue(KeyboardScanCodeTranslator.TryTranslate(0x52, false, out var numpadKey));

        Assert.AreEqual(DeckKey.Insert, navigationKey);
        Assert.AreEqual(DeckKey.Numpad0, numpadKey);
    }

    [TestMethod]
    public void RejectsNumLockAndUnknownScanCodes()
    {
        Assert.IsFalse(KeyboardScanCodeTranslator.TryTranslate(
            KeyboardScanCodeTranslator.NumLockScanCode,
            false,
            out _));
        Assert.IsFalse(KeyboardScanCodeTranslator.TryTranslate(0xFFFF, false, out _));
    }
}
