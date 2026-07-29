using System.Globalization;
using System.Xml.Linq;
using System.Windows.Resources;

namespace StreamNumDeck.Wpf.Localization;

public static class AppStrings
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Strings = new(LoadStrings);

    public static string CultureName { get; } = ApplicationCulture.ResolveCultureName(CultureInfo.CurrentUICulture);

    public static string Get(string key, string? fallback = null) =>
        Strings.Value.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : fallback ?? key;

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static IReadOnlyDictionary<string, string> LoadStrings()
    {
        var result = LoadCulture("en-US");
        if (!CultureName.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var pair in LoadCulture(CultureName))
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private static Dictionary<string, string> LoadCulture(string cultureName)
    {
        var uri = new Uri($"pack://application:,,,/Strings/{cultureName}/Resources.resw", UriKind.Absolute);
        StreamResourceInfo resource = System.Windows.Application.GetResourceStream(uri);
        using var stream = resource.Stream;
        var document = XDocument.Load(stream);
        return document.Root?
                   .Elements("data")
                   .Where(element => element.Attribute("name") is not null)
                   .ToDictionary(
                       element => element.Attribute("name")!.Value,
                       element => element.Element("value")?.Value ?? string.Empty,
                       StringComparer.Ordinal)
               ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
