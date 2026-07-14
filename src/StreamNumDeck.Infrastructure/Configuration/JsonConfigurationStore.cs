using System.Text.Json;
using System.Text.Json.Serialization;
using StreamNumDeck.Core.Configuration;

namespace StreamNumDeck.Infrastructure.Configuration;

public sealed class JsonConfigurationStore : IConfigurationStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ConfigurationPaths paths;
    private readonly Func<AppConfiguration> createDefaultConfiguration;
    private readonly ConfigurationJsonContext jsonContext;
    private bool disposed;

    public JsonConfigurationStore(
        ConfigurationPaths paths,
        Func<AppConfiguration>? createDefaultConfiguration = null)
    {
        this.paths = Guard.NotNull(paths, nameof(paths));
        this.createDefaultConfiguration = createDefaultConfiguration ?? (static () => AppConfiguration.CreateDefault());

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        jsonContext = new ConfigurationJsonContext(options);
    }

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(paths.DirectoryPath);

            if (!File.Exists(paths.ConfigurationFilePath))
            {
                if (File.Exists(paths.BackupFilePath))
                {
                    var backupConfiguration = await ReadAsync(paths.BackupFilePath, cancellationToken).ConfigureAwait(false);
                    await WriteAsync(backupConfiguration, createBackup: false, cancellationToken).ConfigureAwait(false);
                    return backupConfiguration;
                }

                var defaultConfiguration = createDefaultConfiguration();
                await WriteAsync(defaultConfiguration, createBackup: false, cancellationToken).ConfigureAwait(false);
                return defaultConfiguration;
            }

            try
            {
                return await ReadAsync(paths.ConfigurationFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception primaryFailure) when (IsRecoverableReadFailure(primaryFailure))
            {
                if (!File.Exists(paths.BackupFilePath))
                {
                    throw new ConfigurationLoadException(
                        "The configuration file is invalid and no backup is available.",
                        primaryFailure);
                }

                try
                {
                    var recoveredConfiguration = await ReadAsync(paths.BackupFilePath, cancellationToken).ConfigureAwait(false);
                    await WriteAsync(recoveredConfiguration, createBackup: false, cancellationToken).ConfigureAwait(false);
                    return recoveredConfiguration;
                }
                catch (Exception backupFailure) when (IsRecoverableReadFailure(backupFailure))
                {
                    throw new ConfigurationLoadException(
                        "Both the configuration file and its backup are invalid.",
                        primaryFailure,
                        backupFailure);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Guard.NotDisposed(disposed, this);
        Guard.NotNull(configuration, nameof(configuration));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(paths.DirectoryPath);
            await WriteAsync(configuration, createBackup: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        gate.Dispose();
        disposed = true;
    }

    private async Task<AppConfiguration> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("schemaVersion", out var schemaVersionElement) ||
            !schemaVersionElement.TryGetInt32(out var schemaVersion))
        {
            throw new InvalidDataException("The configuration schema version is missing or invalid.");
        }

        if (schemaVersion != ConfigurationSchema.CurrentVersion)
        {
            throw new UnsupportedConfigurationVersionException(
                schemaVersion,
                ConfigurationSchema.CurrentVersion);
        }

        var envelope = document.RootElement.Deserialize(jsonContext.ConfigurationEnvelope);

        if (envelope is null)
        {
            throw new InvalidDataException("The configuration document is empty.");
        }

        return envelope.Configuration ?? throw new InvalidDataException("The configuration payload is missing.");
    }

    private async Task WriteAsync(
        AppConfiguration configuration,
        bool createBackup,
        CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(
            paths.DirectoryPath,
            $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var envelope = new ConfigurationEnvelope(ConfigurationSchema.CurrentVersion, configuration);
                await JsonSerializer
                    .SerializeAsync(stream, envelope, jsonContext.ConfigurationEnvelope, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(paths.ConfigurationFilePath))
            {
                File.Replace(
                    tempFilePath,
                    paths.ConfigurationFilePath,
                    createBackup ? paths.BackupFilePath : null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, paths.ConfigurationFilePath);
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static bool IsRecoverableReadFailure(Exception exception) => exception is
        JsonException or
        InvalidDataException or
        UnsupportedConfigurationVersionException or
        ArgumentException or
        NotSupportedException;
}
