using System.Globalization;
using System.Windows.Data;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.Wpf.Presentation;

public sealed class ProfileGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IconReference icon ? DeckPresentation.GetGlyph(icon.Value) : DeckPresentation.GetGlyph("broadcast");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
