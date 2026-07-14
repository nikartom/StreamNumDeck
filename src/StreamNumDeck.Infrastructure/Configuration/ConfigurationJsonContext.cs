using System.Text.Json.Serialization;

namespace StreamNumDeck.Infrastructure.Configuration;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(ConfigurationEnvelope))]
internal sealed partial class ConfigurationJsonContext : JsonSerializerContext
{
}
