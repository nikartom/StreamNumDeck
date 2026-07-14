using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.Core.Deck;

public sealed record DeckProfile
{
    public const int MaxNameLength = 80;

    public DeckProfile(
        Guid id,
        string name,
        DeckLayer numLockOff,
        DeckLayer numLockOn,
        IconReference? icon = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A profile identifier is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A profile name is required.", nameof(name));
        }

        name = name.Trim();

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name), $"Profile names cannot exceed {MaxNameLength} characters.");
        }

        Guard.NotNull(numLockOff, nameof(numLockOff));
        Guard.NotNull(numLockOn, nameof(numLockOn));

        if (numLockOff.Mode is not NumLockLayer.Off || numLockOn.Mode is not NumLockLayer.On)
        {
            throw new ArgumentException("Profile layers do not match their NumLock states.");
        }

        Id = id;
        Name = name;
        NumLockOff = numLockOff;
        NumLockOn = numLockOn;
        Icon = icon ?? IconReference.BuiltIn("broadcast");
    }

    public Guid Id { get; }

    public string Name { get; }

    public DeckLayer NumLockOff { get; }

    public DeckLayer NumLockOn { get; }

    public IconReference Icon { get; }

    public static DeckProfile CreateDefault(
        string name = "Основной стрим",
        IconReference? icon = null) => new(
        Guid.NewGuid(),
        name,
        DeckLayer.CreateEmpty(NumLockLayer.Off),
        DeckLayer.CreateEmpty(NumLockLayer.On),
        icon);

    public DeckLayer GetLayer(NumLockLayer layer) => layer is NumLockLayer.On ? NumLockOn : NumLockOff;

    public DeckProfile WithDetails(string name, IconReference icon) =>
        new(Id, name, NumLockOff, NumLockOn, icon);

    public DeckProfile Duplicate(string name) =>
        new(Guid.NewGuid(), name, NumLockOff, NumLockOn, Icon);

    public DeckProfile WithAssignment(NumLockLayer layer, DeckKey key, KeyAssignment assignment) => layer switch
    {
        NumLockLayer.Off => new DeckProfile(Id, Name, NumLockOff.WithAssignment(key, assignment), NumLockOn, Icon),
        NumLockLayer.On => new DeckProfile(Id, Name, NumLockOff, NumLockOn.WithAssignment(key, assignment), Icon),
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
    };
}
