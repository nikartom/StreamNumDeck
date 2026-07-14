using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using StreamNumDeck.App.Presentation;
using StreamNumDeck.App.Localization;
using StreamNumDeck.App.Views;
using StreamNumDeck.Core.Configuration;
using StreamNumDeck.Core.Deck;
using StreamNumDeck.Core.Icons;

namespace StreamNumDeck.App;

public sealed partial class MainPage : Page
{
    private readonly ConfigurationService configurationService;
    private readonly IIconAssetStore iconAssetStore;
    private readonly Dictionary<Guid, NavigationViewItem> profileItems = [];
    private bool suppressSelectionChanged;
    private bool selectionChangeInProgress;
    private Guid? editingProfileId;

    public IReadOnlyList<BuiltInIconOption> BuiltInIconOptions => BuiltInIconCatalog.Options;

    public MainPage()
    {
        configurationService = App.GetService<ConfigurationService>();
        iconAssetStore = App.GetService<IIconAssetStore>();
        InitializeComponent();
    }

    private async void ShellNavigation_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var configuration = await configurationService.GetAsync();
            RebuildProfileItems(configuration);
            SelectProfileItem(configuration.ActiveProfileId);
            NavigateToDeck();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(AppStrings.Get("Error_LoadProfiles"), exception.Message);
        }
    }

    private async void ShellNavigation_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (suppressSelectionChanged || selectionChangeInProgress)
        {
            return;
        }

        selectionChangeInProgress = true;
        try
        {
            switch (args.SelectedItemContainer?.Tag)
            {
                case "new-profile":
                    await CreateProfileAsync();
                    break;
                case "settings":
                    NavigateTo(typeof(SettingsPage));
                    break;
                case Guid profileId:
                    await ActivateProfileAsync(profileId);
                    break;
            }
        }
        catch (Exception exception)
        {
            await RestoreActiveProfileSelectionAsync();
            await ShowErrorAsync(AppStrings.Get("Error_ChangeProfile"), exception.Message);
        }
        finally
        {
            selectionChangeInProgress = false;
        }
    }

    private async Task CreateProfileAsync()
    {
        var draft = await ShowProfileDialogAsync(null);
        if (draft is null)
        {
            await RestoreActiveProfileSelectionAsync();
            return;
        }

        var profile = DeckProfile.CreateDefault(
            draft.Value.Name,
            draft.Value.Icon);
        var configuration = await configurationService.UpdateAsync(
            current => current.AddProfile(profile));

        RebuildProfileItems(configuration);
        SelectProfileItem(profile.Id);
        await ShowDeckProfileAsync(profile.Id);
    }

    private async Task ActivateProfileAsync(Guid profileId)
    {
        var configuration = await configurationService.GetAsync();
        if (configuration.ActiveProfileId != profileId)
        {
            configuration = await configurationService.UpdateAsync(
                current => current.WithActiveProfile(profileId));
        }

        SelectProfileItem(configuration.ActiveProfileId);
        await ShowDeckProfileAsync(configuration.ActiveProfileId);
    }

    internal async Task ShowProfileFromTrayAsync(Guid profileId)
    {
        var configuration = await configurationService.GetAsync();
        if (configuration.Profiles.All(profile => profile.Id != profileId))
        {
            return;
        }

        RebuildProfileItems(configuration);
        SelectProfileItem(profileId);
        await ShowDeckProfileAsync(profileId);
    }

    private void RebuildProfileItems(AppConfiguration configuration)
    {
        while (ShellNavigation.MenuItems.Count > 1)
        {
            ShellNavigation.MenuItems.RemoveAt(1);
        }

        profileItems.Clear();
        foreach (var profile in configuration.Profiles)
        {
            var item = new NavigationViewItem
            {
                Content = profile.Name,
                Icon = CreateProfileIcon(profile.Icon),
                Tag = profile.Id,
            };
            var editItem = new MenuFlyoutItem
            {
                Text = AppStrings.Get("Profile_Edit"),
                Icon = new SymbolIcon(Symbol.Edit),
            };
            editItem.Click += async (_, _) => await EditProfileSafelyAsync(profile.Id);
            var duplicateItem = new MenuFlyoutItem
            {
                Text = AppStrings.Get("Profile_Duplicate"),
                Icon = new SymbolIcon(Symbol.Copy),
            };
            duplicateItem.Click += async (_, _) => await DuplicateProfileSafelyAsync(profile.Id);
            var deleteItem = new MenuFlyoutItem
            {
                Text = AppStrings.Get("Profile_Delete"),
                Icon = new SymbolIcon(Symbol.Delete),
                IsEnabled = configuration.Profiles.Length > 1,
            };
            deleteItem.Click += async (_, _) => await DeleteProfileSafelyAsync(profile.Id);
            var contextMenu = new MenuFlyout();
            contextMenu.Items.Add(editItem);
            contextMenu.Items.Add(duplicateItem);
            contextMenu.Items.Add(new MenuFlyoutSeparator());
            contextMenu.Items.Add(deleteItem);
            item.ContextFlyout = contextMenu;
            ToolTipService.SetToolTip(item, AppStrings.Format("Profile_ContextHint", profile.Name));
            profileItems.Add(profile.Id, item);
            ShellNavigation.MenuItems.Add(item);
        }
    }

    private async Task EditProfileSafelyAsync(Guid profileId)
    {
        try
        {
            var configuration = await configurationService.GetAsync();
            var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);
            var draft = await ShowProfileDialogAsync(profile);
            if (draft is null)
            {
                return;
            }

            configuration = await configurationService.UpdateAsync(
                current => current.ReplaceProfile(profile.WithDetails(draft.Value.Name, draft.Value.Icon)));
            RebuildProfileItems(configuration);
            SelectProfileItem(configuration.ActiveProfileId);
            await ShowDeckProfileAsync(configuration.ActiveProfileId);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(AppStrings.Get("Error_EditProfile"), exception.Message);
        }
    }

    private async Task DuplicateProfileSafelyAsync(Guid profileId)
    {
        try
        {
            var configuration = await configurationService.GetAsync();
            var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);
            var duplicate = profile.Duplicate(CreateCopyName(profile.Name, configuration));
            configuration = await configurationService.UpdateAsync(current => current.AddProfile(duplicate));
            RebuildProfileItems(configuration);
            SelectProfileItem(duplicate.Id);
            await ShowDeckProfileAsync(duplicate.Id);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(AppStrings.Get("Error_DuplicateProfile"), exception.Message);
        }
    }

    private async Task DeleteProfileSafelyAsync(Guid profileId)
    {
        try
        {
            var configuration = await configurationService.GetAsync();
            var profile = configuration.Profiles.Single(candidate => candidate.Id == profileId);
            var confirmation = new ContentDialog
            {
                Title = AppStrings.Get("Profile_DeleteTitle"),
                Content = AppStrings.Format("Profile_DeleteMessage", profile.Name),
                PrimaryButtonText = AppStrings.Get("Common_Delete"),
                CloseButtonText = AppStrings.Get("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            if (await confirmation.ShowAsync() is not ContentDialogResult.Primary)
            {
                return;
            }

            configuration = await configurationService.UpdateAsync(current => current.RemoveProfile(profileId));
            RebuildProfileItems(configuration);
            SelectProfileItem(configuration.ActiveProfileId);
            await ShowDeckProfileAsync(configuration.ActiveProfileId);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(AppStrings.Get("Error_DeleteProfile"), exception.Message);
        }
    }

    private static string CreateCopyName(string sourceName, AppConfiguration configuration)
    {
        var suffix = AppStrings.Get("Profile_CopySuffix");
        var baseName = sourceName.Length + suffix.Length <= DeckProfile.MaxNameLength
            ? sourceName
            : sourceName[..(DeckProfile.MaxNameLength - suffix.Length)];
        var candidate = baseName + suffix;
        var number = 2;
        while (configuration.Profiles.Any(profile =>
                   string.Equals(profile.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
        {
            var numberedSuffix = AppStrings.Format("Profile_NumberedCopySuffix", number++);
            var availableLength = DeckProfile.MaxNameLength - numberedSuffix.Length;
            candidate = sourceName[..Math.Min(sourceName.Length, availableLength)] + numberedSuffix;
        }

        return candidate;
    }

    private async Task<(string Name, IconReference Icon)?> ShowProfileDialogAsync(DeckProfile? profile)
    {
        editingProfileId = profile?.Id;
        NewProfileDialog.Title = profile is null ? AppStrings.Get("Profile_New") : AppStrings.Get("Profile_Edit");
        NewProfileDialog.PrimaryButtonText = profile is null ? AppStrings.Get("Common_Create") : AppStrings.Get("Common_Save");
        ProfileNameTextBox.Text = profile?.Name ?? string.Empty;
        ProfileDialogError.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        ProfileIconGrid.SelectedItem = profile?.Icon.Kind is IconKind.BuiltIn
            ? BuiltInIconCatalog.Get(profile.Icon.Value)
            : BuiltInIconCatalog.Get("broadcast");
        NewProfileDialog.XamlRoot = XamlRoot;

        var result = await NewProfileDialog.ShowAsync();
        editingProfileId = null;
        if (result is not ContentDialogResult.Primary)
        {
            return null;
        }

        var icon = ProfileIconGrid.SelectedItem as BuiltInIconOption
            ?? BuiltInIconCatalog.Get("broadcast");
        return (ProfileNameTextBox.Text.Trim(), IconReference.BuiltIn(icon.Id));
    }

    private IconElement CreateProfileIcon(IconReference icon)
    {
        if (icon.Kind is IconKind.CustomAsset)
        {
            var path = iconAssetStore.ResolvePath(icon);
            if (File.Exists(path))
            {
                return new ImageIcon
                {
                    Source = new BitmapImage(new Uri(path)),
                };
            }
        }

        return new FontIcon
        {
            Glyph = BuiltInIconCatalog.Get(icon.Value).Glyph,
        };
    }

    private void SelectProfileItem(Guid profileId)
    {
        if (!profileItems.TryGetValue(profileId, out var item))
        {
            return;
        }

        suppressSelectionChanged = true;
        ShellNavigation.SelectedItem = item;
        suppressSelectionChanged = false;
    }

    private async Task RestoreActiveProfileSelectionAsync()
    {
        var configuration = await configurationService.GetAsync();
        SelectProfileItem(configuration.ActiveProfileId);
    }

    private void NavigateToDeck()
    {
        ContentFrame.Navigate(
            typeof(DeckPage),
            null,
            new SuppressNavigationTransitionInfo());
        ContentFrame.BackStack.Clear();
    }

    private async Task ShowDeckProfileAsync(Guid profileId)
    {
        if (ContentFrame.Content is DeckPage deckPage)
        {
            await deckPage.ShowProfileAsync(profileId);
            return;
        }

        NavigateToDeck();
    }

    private void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
            ContentFrame.BackStack.Clear();
        }
    }

    private async void NewProfileDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var name = ProfileNameTextBox.Text.Trim();
            var configuration = await configurationService.GetAsync();
            var error = name.Length == 0
                ? AppStrings.Get("Profile_NameRequired")
                : configuration.Profiles.Any(profile =>
                    profile.Id != editingProfileId
                    && string.Equals(profile.Name, name, StringComparison.CurrentCultureIgnoreCase))
                    ? AppStrings.Get("Profile_NameExists")
                    : null;

            if (error is null)
            {
                return;
            }

            args.Cancel = true;
            ProfileDialogError.Text = error;
            ProfileDialogError.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ProfileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ProfileDialogError.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = AppStrings.Get("Common_Close"),
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
