using System.Collections.Immutable;
using System.Globalization;

namespace StreamNumDeck.Core.Actions;

public enum KeyboardMacroFormatError
{
    Empty,
    InvalidLine,
    TooManySteps,
}

public sealed class KeyboardMacroFormatException : FormatException
{
    public KeyboardMacroFormatException(
        KeyboardMacroFormatError error,
        string diagnosticMessage,
        int? lineNumber = null,
        Exception? innerException = null)
        : base(diagnosticMessage, innerException)
    {
        Error = error;
        LineNumber = lineNumber;
    }

    public KeyboardMacroFormatError Error { get; }

    public int? LineNumber { get; }
}

public static class KeyboardMacroTextCodec
{
    public static ImmutableArray<KeyboardMacroStep> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new KeyboardMacroFormatException(
                KeyboardMacroFormatError.Empty,
                "Enter at least one keyboard shortcut.");
        }

        var steps = ImmutableArray.CreateBuilder<KeyboardMacroStep>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                steps.Add(IsDelay(line)
                    ? ParseDelay(line)
                    : ParseChord(line));
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                throw new KeyboardMacroFormatException(
                    KeyboardMacroFormatError.InvalidLine,
                    $"Line {lineIndex + 1}: {exception.Message}",
                    lineIndex + 1,
                    exception);
            }
        }

        if (steps.Count == 0)
        {
            throw new KeyboardMacroFormatException(
                KeyboardMacroFormatError.Empty,
                "Enter at least one keyboard shortcut.");
        }

        if (steps.Count > KeyboardMacroActionDefinition.MaximumSteps)
        {
            throw new KeyboardMacroFormatException(
                KeyboardMacroFormatError.TooManySteps,
                $"A macro cannot contain more than {KeyboardMacroActionDefinition.MaximumSteps} steps.");
        }

        return steps.ToImmutable();
    }

    public static string Format(IEnumerable<KeyboardMacroStep> steps)
    {
        Guard.NotNull(steps, nameof(steps));

        return string.Join(
            Environment.NewLine,
            steps.Select(static step => step.Kind switch
            {
                KeyboardMacroStepKind.Delay => $"wait {step.DelayMilliseconds}",
                KeyboardMacroStepKind.KeyPress => FormatChord(step.Chord!),
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            }));
    }

    private static bool IsDelay(string line)
    {
        var firstSeparator = line.IndexOfAny(new[] { ' ', '\t' });
        var command = firstSeparator < 0 ? line : line.Substring(0, firstSeparator);
        return command.Equals("wait", StringComparison.OrdinalIgnoreCase)
            || command.Equals("delay", StringComparison.OrdinalIgnoreCase)
            || command.Equals("пауза", StringComparison.OrdinalIgnoreCase);
    }

    private static KeyboardMacroStep ParseDelay(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .ToArray();
        if (parts.Length is not 2
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
        {
            throw new FormatException("Use the format `wait 250`, where the number is milliseconds.");
        }

        return KeyboardMacroStep.Delay(milliseconds);
    }

    private static KeyboardMacroStep ParseChord(string line)
    {
        var parts = line.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .ToArray();
        if (parts.Length == 0)
        {
            throw new FormatException("No keyboard shortcut was found.");
        }

        var modifiers = MacroModifiers.None;
        MacroKey? key = null;

        foreach (var part in parts)
        {
            if (TryParseModifier(part, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    throw new FormatException($"Modifier `{part}` is specified more than once.");
                }

                modifiers |= modifier;
                continue;
            }

            if (key is not null)
            {
                throw new FormatException("A step can contain only one primary key.");
            }

            key = ParseKey(part);
        }

        return key is null
            ? throw new FormatException("No primary key is specified after the modifiers.")
            : KeyboardMacroStep.KeyPress(key.Value, modifiers);
    }

    private static bool TryParseModifier(string value, out MacroModifiers modifier)
    {
        modifier = Normalize(value) switch
        {
            "ctrl" or "control" => MacroModifiers.Control,
            "shift" => MacroModifiers.Shift,
            "alt" => MacroModifiers.Alt,
            "win" or "windows" or "meta" => MacroModifiers.Windows,
            _ => MacroModifiers.None,
        };

        return modifier is not MacroModifiers.None;
    }

    private static MacroKey ParseKey(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 1)
        {
            var character = normalized[0];
            if (character is >= 'a' and <= 'z')
            {
                return (MacroKey)((int)MacroKey.A + character - 'a');
            }

            if (character is >= '0' and <= '9')
            {
                return (MacroKey)((int)MacroKey.Digit0 + character - '0');
            }
        }

        if (normalized.Length is 2 or 3
            && normalized[0] == 'f'
            && int.TryParse(normalized.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            return (MacroKey)((int)MacroKey.F1 + functionKey - 1);
        }

        return normalized switch
        {
            "enter" or "return" => MacroKey.Enter,
            "esc" or "escape" => MacroKey.Escape,
            "tab" => MacroKey.Tab,
            "space" => MacroKey.Space,
            "backspace" or "back" => MacroKey.Backspace,
            "delete" or "del" => MacroKey.Delete,
            "insert" or "ins" => MacroKey.Insert,
            "home" => MacroKey.Home,
            "end" => MacroKey.End,
            "pageup" or "pgup" => MacroKey.PageUp,
            "pagedown" or "pgdn" => MacroKey.PageDown,
            "up" => MacroKey.Up,
            "down" => MacroKey.Down,
            "left" => MacroKey.Left,
            "right" => MacroKey.Right,
            "volumeup" => MacroKey.VolumeUp,
            "volumedown" => MacroKey.VolumeDown,
            "volumemute" or "mute" => MacroKey.VolumeMute,
            "mediaplaypause" or "playpause" => MacroKey.MediaPlayPause,
            "medianext" or "nexttrack" => MacroKey.MediaNext,
            "mediaprevious" or "previoustrack" => MacroKey.MediaPrevious,
            _ => throw new FormatException($"Unknown key `{value}`."),
        };
    }

    private static string FormatChord(KeyboardChord chord)
    {
        var parts = new List<string>(5);
        if (chord.Modifiers.HasFlag(MacroModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (chord.Modifiers.HasFlag(MacroModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (chord.Modifiers.HasFlag(MacroModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (chord.Modifiers.HasFlag(MacroModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(chord.Key));
        return string.Join("+", parts);
    }

    private static string FormatKey(MacroKey key) => key switch
    {
        >= MacroKey.A and <= MacroKey.Z => key.ToString(),
        >= MacroKey.Digit0 and <= MacroKey.Digit9 => ((int)key - (int)MacroKey.Digit0).ToString(CultureInfo.InvariantCulture),
        >= MacroKey.F1 and <= MacroKey.F24 => $"F{(int)key - (int)MacroKey.F1 + 1}",
        MacroKey.Escape => "Esc",
        MacroKey.PageUp => "PageUp",
        MacroKey.PageDown => "PageDown",
        MacroKey.VolumeUp => "VolumeUp",
        MacroKey.VolumeDown => "VolumeDown",
        MacroKey.VolumeMute => "VolumeMute",
        MacroKey.MediaPlayPause => "MediaPlayPause",
        MacroKey.MediaNext => "MediaNext",
        MacroKey.MediaPrevious => "MediaPrevious",
        _ => key.ToString(),
    };

    private static string Normalize(string value) => value
        .Trim()
        .Replace("-", string.Empty)
        .Replace("_", string.Empty)
        .Replace(" ", string.Empty)
        .ToLowerInvariant();
}
