using System.Diagnostics;

namespace StreamNumDeck.Infrastructure.Compatibility;

internal static class PlatformCompatibility
{
    public static long TickCount64 =>
        Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
}
