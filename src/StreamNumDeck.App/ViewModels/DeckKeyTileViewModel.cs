using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using StreamNumDeck.App.Presentation;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.App.ViewModels;

public partial class DeckKeyTileViewModel(DeckKey key) : ObservableObject
{
    public DeckKey Key { get; } = key;

    [ObservableProperty]
    public partial string Label { get; private set; } = DeckKeyPresentation.GetPhysicalLabel(key);

    [ObservableProperty]
    public partial string Glyph { get; private set; } = BuiltInIconCatalog.Get("square").Glyph;

    [ObservableProperty]
    public partial ImageSource? CustomIconSource { get; private set; }

    [ObservableProperty]
    public partial Visibility BuiltInIconVisibility { get; private set; } = Visibility.Visible;

    [ObservableProperty]
    public partial Visibility CustomIconVisibility { get; private set; } = Visibility.Collapsed;

    public void Update(KeyAssignment assignment, IIconAssetStore iconAssetStore)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(iconAssetStore);

        Label = string.IsNullOrWhiteSpace(assignment.Label)
            ? DeckKeyPresentation.GetPhysicalLabel(Key)
            : assignment.Label;

        if (assignment.Icon.Kind is IconKind.CustomAsset)
        {
            var path = iconAssetStore.ResolvePath(assignment.Icon);
            CustomIconSource = File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
            CustomIconVisibility = CustomIconSource is null ? Visibility.Collapsed : Visibility.Visible;
            BuiltInIconVisibility = CustomIconSource is null ? Visibility.Visible : Visibility.Collapsed;
            Glyph = BuiltInIconCatalog.Get("square").Glyph;
            return;
        }

        CustomIconSource = null;
        CustomIconVisibility = Visibility.Collapsed;
        BuiltInIconVisibility = Visibility.Visible;
        Glyph = BuiltInIconCatalog.Get(assignment.Icon.Value).Glyph;
    }
}
