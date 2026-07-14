namespace StreamNumDeck.Core.Icons;

public interface IIconAssetStore
{
    Task<IconReference> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default);

    string ResolvePath(IconReference icon);
}
