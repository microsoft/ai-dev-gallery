// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;

namespace AIDevGallery.Pages;

internal sealed partial class HomePage : Page
{
    private ObservableCollection<MostRecentlyUsedItem> mostRecentlyUsedItems = new ObservableCollection<MostRecentlyUsedItem>();

    public HomePage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        NavigatedToPageEvent.Log(nameof(HomePage));
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (!App.AppData.IsDiagnosticsMessageDismissed && PrivacyConsentHelpers.IsPrivacySensitiveRegion())
        {
            DiagnosticsInfoBar.IsOpen = true;
        }

        LoadRecentlyUsedItems();
    }

    private void LoadRecentlyUsedItems()
    {
        if (App.AppData.MostRecentlyUsedItems.Count > 0)
        {
            RecentPanel.Visibility = Visibility.Visible;

            foreach (var item in App.AppData.MostRecentlyUsedItems)
            {
                item.Description = item.Description;
                mostRecentlyUsedItems.Add(item);
            }
        }
    }

    private void DiagnosticsYesButton_Click(object sender, RoutedEventArgs e)
    {
        HandleDiagnosticsSetting(true);
    }

    private void DiagnosticsNoButton_Click(object sender, RoutedEventArgs e)
    {
        HandleDiagnosticsSetting(false);
    }

    private async void HandleDiagnosticsSetting(bool isEnabled)
    {
        DiagnosticsInfoBar.IsOpen = false;
        App.AppData.IsDiagnosticsMessageDismissed = true;
        App.AppData.IsDiagnosticDataEnabled = isEnabled;
        await App.AppData.SaveAsync();
    }

    private void MRUView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is MostRecentlyUsedItem mru)
        {
            App.MainWindow.Navigate(mru);
        }
    }
}