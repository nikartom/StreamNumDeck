using System.Windows.Markup;

namespace StreamNumDeck.Wpf.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; }
    public string? Fallback { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        AppStrings.Get(Key, Fallback);
}
