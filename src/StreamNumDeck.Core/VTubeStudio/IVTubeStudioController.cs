using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Settings;

namespace StreamNumDeck.Core.VTubeStudio;

public sealed record VTubeStudioHotkey(string Id, string Name, string Type);

public sealed record VTubeStudioModel(string Id, string Name, bool IsLoaded);

public sealed record VTubeStudioCatalog(
    string? CurrentModelName,
    IReadOnlyList<VTubeStudioHotkey> Hotkeys,
    IReadOnlyList<VTubeStudioModel> Models)
{
    public static VTubeStudioCatalog Empty { get; } = new(null, [], []);
}

public interface IVTubeStudioController
{
    Task<VTubeStudioCatalog> GetCatalogAsync(
        VTubeStudioConnectionSettings settings,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        VTubeStudioConnectionSettings settings,
        VTubeStudioActionDefinition action,
        CancellationToken cancellationToken = default);
}
