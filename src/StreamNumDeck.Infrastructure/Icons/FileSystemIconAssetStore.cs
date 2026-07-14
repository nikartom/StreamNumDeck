using System.Security.Cryptography;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.Infrastructure.Icons;

public sealed class FileSystemIconAssetStore : IIconAssetStore
{
    public const long MaximumFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpeg",
        ".jpg",
        ".png",
        ".webp",
    };

    private readonly string assetRoot;
    private readonly string assetRootWithSeparator;

    public FileSystemIconAssetStore(string assetRoot)
    {
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            throw new ArgumentException("An asset root is required.", nameof(assetRoot));
        }

        this.assetRoot = Path.GetFullPath(assetRoot);
        assetRootWithSeparator = this.assetRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? this.assetRoot
            : this.assetRoot + Path.DirectorySeparatorChar;
    }

    public async Task<IconReference> ImportAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("A source icon path is required.", nameof(sourceFilePath));
        }

        var fullSourcePath = Path.GetFullPath(sourceFilePath);
        var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidDataException($"Unsupported icon format: {extension}.");
        }

        var sourceInfo = new FileInfo(fullSourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("The selected icon file does not exist.", fullSourcePath);
        }

        if (sourceInfo.Length is <= 0 or > MaximumFileSize)
        {
            throw new InvalidDataException($"Icon files must be between 1 byte and {MaximumFileSize} bytes.");
        }

        byte[] hash;
        using (var source = new FileStream(
            fullSourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var algorithm = SHA256.Create();
            hash = algorithm.ComputeHash(source);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var fileName = $"{BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()}{extension}";
        var relativePath = Path.Combine("icons", fileName).Replace(Path.DirectorySeparatorChar, '/');
        var destinationPath = ResolveRelativePath(relativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDirectory);

        if (File.Exists(destinationPath))
        {
            return IconReference.CustomAsset(relativePath);
        }

        var tempPath = Path.Combine(destinationDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var source = new FileStream(
                fullSourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destination = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, 64 * 1024, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                destination.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(tempPath, destinationPath);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                // Another import completed the same content-addressed asset first.
            }

            return IconReference.CustomAsset(relativePath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public string ResolvePath(IconReference icon)
    {
        Guard.NotNull(icon, nameof(icon));

        if (icon.Kind is not IconKind.CustomAsset)
        {
            throw new ArgumentException("Only custom icon assets resolve to files.", nameof(icon));
        }

        return ResolveRelativePath(icon.Value);
    }

    private string ResolveRelativePath(string relativePath)
    {
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(assetRoot, normalizedPath));

        if (!fullPath.StartsWith(assetRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The icon path escapes the managed asset directory.");
        }

        return fullPath;
    }
}
