namespace StreamNumDeck.Core.Icons;

public enum IconKind
{
    BuiltIn,
    CustomAsset,
}

public sealed record IconReference
{
    public const int MaxValueLength = 260;

    public IconReference(IconKind kind, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An icon value is required.", nameof(value));
        }

        value = value.Trim();

        if (value.Length > MaxValueLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Icon values cannot exceed {MaxValueLength} characters.");
        }

        if (kind is IconKind.CustomAsset && (Path.IsPathRooted(value) || value.Contains("..", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Custom icon assets must use a safe relative path.", nameof(value));
        }

        Kind = kind;
        Value = value;
    }

    public IconKind Kind { get; }

    public string Value { get; }

    public static IconReference BuiltIn(string iconName) => new(IconKind.BuiltIn, iconName);

    public static IconReference CustomAsset(string relativePath) => new(IconKind.CustomAsset, relativePath);
}
