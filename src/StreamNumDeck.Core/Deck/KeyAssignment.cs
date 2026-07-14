using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.Core.Deck;

public sealed record KeyAssignment
{
    public const int MaxLabelLength = 40;

    public KeyAssignment(string label, IconReference icon, ActionDefinition action)
    {
        Guard.NotNull(icon, nameof(icon));
        Guard.NotNull(action, nameof(action));

        label = label?.Trim() ?? string.Empty;

        if (label.Length > MaxLabelLength)
        {
            throw new ArgumentOutOfRangeException(nameof(label), $"Labels cannot exceed {MaxLabelLength} characters.");
        }

        Label = label;
        Icon = icon;
        Action = action;
    }

    public string Label { get; }

    public IconReference Icon { get; }

    public ActionDefinition Action { get; }

    public static KeyAssignment Empty { get; } = new(
        string.Empty,
        IconReference.BuiltIn("square"),
        new NoActionDefinition());
}
