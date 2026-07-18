using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StreamNumDeck.Wpf.Localization;
using StreamNumDeck.Wpf.Presentation;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.Wpf;

public partial class ProfileEditorWindow : Window
{
    private string selectedIconId = "broadcast";

    public ProfileEditorWindow(DeckProfile? profile)
    {
        InitializeComponent();
        Title = AppStrings.Get(profile is null ? "Profile_New" : "Profile_Edit");
        NameLabel.Text = AppStrings.Get("ProfileName.Header", "Name");
        IconLabel.Text = AppStrings.Get("ProfileIcon.Text", "Icon");
        CancelButton.Content = AppStrings.Get("Common_Cancel", "Cancel");
        SaveButton.Content = AppStrings.Get(profile is null ? "Common_Create" : "Common_Save");

        selectedIconId = profile?.Icon.Kind == IconKind.BuiltIn ? profile.Icon.Value : "broadcast";
        IconListBox.ItemsSource = BuiltInIconCatalog.Options
            .OrderBy(option => option.Id == selectedIconId ? 0 : 1)
            .ToArray();
        IconListBox.SelectedValue = selectedIconId;
        ShowIcon(selectedIconId);

        NameTextBox.Text = profile?.Name ?? string.Empty;
        NameTextBox.SelectAll();
        NameTextBox.Focus();
    }

    public string ProfileName => NameTextBox.Text.Trim();
    public IconReference ProfileIcon => IconReference.BuiltIn(selectedIconId);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidationBorder.Visibility = Visibility.Collapsed;
    }

    private void IconListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IconListBox.SelectedItem is not BuiltInIconOption option)
        {
            return;
        }

        selectedIconId = option.Id;
        ShowIcon(option.Id);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileName.Length == 0)
        {
            ValidationText.Text = AppStrings.Get("Profile_NameRequired", "Enter a profile name.");
            ValidationBorder.Visibility = Visibility.Visible;
            NameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void ShowIcon(string iconId)
    {
        IconPreviewGlyph.Text = BuiltInIconCatalog.Get(iconId).Glyph;
    }
}
