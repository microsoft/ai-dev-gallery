// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Telemetry;
using AIDevGallery.Telemetry.Events;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AIDevGallery.Pages;

internal sealed partial class HomePage : Page
{
    public HomePage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        NavigatedToPageEvent.Log(nameof(HomePage));
    }

    private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!App.AppData.IsDiagnosticsMessageDismissed && PrivacyConsentHelpers.IsPrivacySensitiveRegion())
        {
            DiagnosticsInfoBar.IsOpen = true;
        }
    }

    private void DiagnosticsYesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        HandleDiagnosticsSetting(true);
    }

    private void DiagnosticsNoButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
}