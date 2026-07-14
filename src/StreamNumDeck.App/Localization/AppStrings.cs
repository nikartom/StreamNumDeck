using Windows.ApplicationModel.Resources;

namespace StreamNumDeck.App.Localization;

public static class AppStrings
{
    private static readonly ResourceLoader Loader = ResourceLoader.GetForViewIndependentUse();

    public static string Get(string key, string? fallback = null)
    {
        var value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? fallback ?? key : value;
    }

    public static string Format(string key, params object?[] arguments) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Get(key),
            arguments);
}
