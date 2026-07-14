using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;
using StreamNumDeck.Wpf.Presentation;

namespace StreamNumDeck.Wpf.ViewModels;

public sealed class DeckKeyTileViewModel(DeckKey key) : ObservableObject
{
    private string label = DeckPresentation.GetPhysicalLabel(key);
    private string glyph = DeckPresentation.GetGlyph("plus");
    private ImageSource? customIconSource;
    private Visibility glyphVisibility = Visibility.Visible;
    private Visibility customIconVisibility = Visibility.Collapsed;
    private Visibility iconAreaVisibility = Visibility.Visible;
    private Visibility labelVisibility = Visibility.Visible;
    private Thickness labelMargin = new(0, 4, 0, 0);
    private double iconDimension = 28;
    private double glyphFontSize = 20;

    public string Label { get => label; private set => SetProperty(ref label, value); }
    public string Glyph { get => glyph; private set => SetProperty(ref glyph, value); }
    public ImageSource? CustomIconSource { get => customIconSource; private set => SetProperty(ref customIconSource, value); }
    public Visibility GlyphVisibility { get => glyphVisibility; private set => SetProperty(ref glyphVisibility, value); }
    public Visibility CustomIconVisibility { get => customIconVisibility; private set => SetProperty(ref customIconVisibility, value); }
    public Visibility IconAreaVisibility { get => iconAreaVisibility; private set => SetProperty(ref iconAreaVisibility, value); }
    public Visibility LabelVisibility { get => labelVisibility; private set => SetProperty(ref labelVisibility, value); }
    public Thickness LabelMargin { get => labelMargin; private set => SetProperty(ref labelMargin, value); }
    public double IconDimension { get => iconDimension; private set => SetProperty(ref iconDimension, value); }
    public double GlyphFontSize { get => glyphFontSize; private set => SetProperty(ref glyphFontSize, value); }

    public void Update(KeyAssignment assignment, IIconAssetStore iconAssetStore)
    {
        var hasUserLabel = !string.IsNullOrWhiteSpace(assignment.Label);
        var isEmpty = !hasUserLabel
                      && assignment.Action is NoActionDefinition
                      && assignment.Icon.Kind == IconKind.BuiltIn
                      && string.Equals(assignment.Icon.Value, "square", StringComparison.Ordinal);
        var hasVisibleIcon = assignment.Icon.Kind != IconKind.BuiltIn
                             || !string.Equals(assignment.Icon.Value, "blank", StringComparison.Ordinal);
        var isIconOnly = !isEmpty && !hasUserLabel && hasVisibleIcon;

        Label = isEmpty ? DeckPresentation.GetPhysicalLabel(key) : assignment.Label;
        LabelVisibility = isEmpty || hasUserLabel ? Visibility.Visible : Visibility.Collapsed;
        IconDimension = isIconOnly ? 40 : 28;
        GlyphFontSize = isIconOnly ? 30 : 20;

        if (assignment.Icon.Kind == IconKind.CustomAsset)
        {
            var path = iconAssetStore.ResolvePath(assignment.Icon);
            CustomIconSource = File.Exists(path) ? TryLoadImage(path) : null;
            CustomIconVisibility = CustomIconSource is null ? Visibility.Collapsed : Visibility.Visible;
            GlyphVisibility = CustomIconSource is null ? Visibility.Visible : Visibility.Collapsed;
            IconAreaVisibility = Visibility.Visible;
            LabelMargin = new Thickness(0, 4, 0, 0);
            Glyph = DeckPresentation.GetGlyph("plus");
            return;
        }

        CustomIconSource = null;
        CustomIconVisibility = Visibility.Collapsed;
        var hasBuiltInIcon = !string.Equals(assignment.Icon.Value, "blank", StringComparison.Ordinal);
        GlyphVisibility = hasBuiltInIcon ? Visibility.Visible : Visibility.Collapsed;
        IconAreaVisibility = hasBuiltInIcon ? Visibility.Visible : Visibility.Collapsed;
        LabelMargin = hasBuiltInIcon ? new Thickness(0, 4, 0, 0) : new Thickness(0);
        Glyph = DeckPresentation.GetGlyph(assignment.Icon.Value);
    }

    private static ImageSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource? TryLoadImage(string path)
    {
        try
        {
            return LoadImage(path);
        }
        catch
        {
            return null;
        }
    }
}
