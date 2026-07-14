using System.Collections.Immutable;

namespace StreamNumDeck.Core.Deck;

public sealed record DeckLayer
{
    public DeckLayer(NumLockLayer mode, ImmutableDictionary<DeckKey, KeyAssignment> assignments)
    {
        Guard.NotNull(assignments, nameof(assignments));

        var missingKeys = DeckKeyCatalog.AssignableKeys.Where(key => !assignments.ContainsKey(key)).ToArray();
        var unknownKeys = assignments.Keys.Where(key => !DeckKeyCatalog.AssignableKeys.Contains(key)).ToArray();

        if (missingKeys.Length > 0 || unknownKeys.Length > 0 || assignments.Count != DeckKeyCatalog.AssignableKeys.Length)
        {
            throw new ArgumentException("A deck layer must contain exactly one assignment for every assignable key.", nameof(assignments));
        }

        if (assignments.Values.Any(static assignment => assignment is null))
        {
            throw new ArgumentException("Deck assignments cannot be null.", nameof(assignments));
        }

        Mode = mode;
        Assignments = assignments;
    }

    public NumLockLayer Mode { get; }

    public ImmutableDictionary<DeckKey, KeyAssignment> Assignments { get; }

    public static DeckLayer CreateEmpty(NumLockLayer mode) => new(
        mode,
        DeckKeyCatalog.AssignableKeys.ToImmutableDictionary(static key => key, static _ => KeyAssignment.Empty));

    public KeyAssignment GetAssignment(DeckKey key) => Assignments[key];

    public DeckLayer WithAssignment(DeckKey key, KeyAssignment assignment)
    {
        Guard.NotNull(assignment, nameof(assignment));

        if (!DeckKeyCatalog.AssignableKeys.Contains(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "The key is not assignable.");
        }

        return new DeckLayer(Mode, Assignments.SetItem(key, assignment));
    }
}
