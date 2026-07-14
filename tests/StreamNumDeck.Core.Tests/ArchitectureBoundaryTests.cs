using System.Reflection;

namespace StreamNumDeck.Core.Tests;

[TestClass]
public sealed class ArchitectureBoundaryTests
{
    private static readonly HashSet<string> ForbiddenReferences =
    [
        "Microsoft.UI.Xaml",
        "Microsoft.WindowsAppSDK",
        "StreamNumDeck.Infrastructure",
    ];

    [TestMethod]
    public void CoreAssembly_DoesNotReference_PlatformOrInfrastructureAssemblies()
    {
        var references = typeof(AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var violations = references.Intersect(ForbiddenReferences).Order().ToArray();

        Assert.HasCount(0, violations, $"Forbidden Core references: {string.Join(", ", violations)}");
    }
}
