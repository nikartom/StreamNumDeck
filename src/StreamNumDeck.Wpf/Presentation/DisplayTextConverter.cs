using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace StreamNumDeck.Wpf.Presentation;

public sealed class DisplayTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        PropertyInfo? name = value.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
        return name?.GetValue(value)?.ToString() ?? value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
