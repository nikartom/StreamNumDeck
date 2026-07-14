using StreamNumDeck.Core.Configuration;

namespace StreamNumDeck.Infrastructure.Configuration;

internal sealed record ConfigurationEnvelope(int SchemaVersion, AppConfiguration Configuration);
