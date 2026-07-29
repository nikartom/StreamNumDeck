using System.Globalization;

namespace StreamNumDeck.Wpf.Localization;

internal static class ApplicationCulture
{
    private static readonly string[] SupportedCultures =
    {
        "ar-SA", "cs-CZ", "de-DE", "en-US", "es-ES", "fr-FR", "hi-IN", "id-ID",
        "it-IT", "ja-JP", "ko-KR", "nl-NL", "pl-PL", "pt-BR", "ro-RO", "ru-RU",
        "sv-SE", "th-TH", "tr-TR", "uk-UA", "vi-VN", "zh-CN",
    };

    public static void ApplyCommandLineOverride(IReadOnlyList<string> arguments)
    {
        var cultureName = ResolveCommandLineOverride(arguments);
        if (cultureName is null)
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string ResolveCultureName(CultureInfo culture) =>
        ResolveSupportedCultureName(culture) ?? "en-US";

    internal static string? ResolveCommandLineOverride(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            string? requestedCulture = null;

            if (argument.Equals("--language", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("--lang", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < arguments.Count)
                {
                    requestedCulture = arguments[index + 1];
                }
            }
            else if (argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase))
            {
                requestedCulture = argument.Substring("--language=".Length);
            }
            else if (argument.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                requestedCulture = argument.Substring("--lang=".Length);
            }

            if (requestedCulture is not null)
            {
                return TryCreateCulture(requestedCulture, out var culture)
                    ? ResolveSupportedCultureName(culture)
                    : null;
            }
        }

        return null;
    }

    private static bool TryCreateCulture(string cultureName, out CultureInfo culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName.Trim());
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }

    private static string? ResolveSupportedCultureName(CultureInfo culture)
    {
        var exact = SupportedCultures.FirstOrDefault(candidate =>
            candidate.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return SupportedCultures.FirstOrDefault(candidate =>
            candidate.StartsWith(culture.TwoLetterISOLanguageName + "-", StringComparison.OrdinalIgnoreCase));
    }
}
