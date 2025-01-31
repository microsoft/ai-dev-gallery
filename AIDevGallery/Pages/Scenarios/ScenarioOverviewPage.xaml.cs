// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
namespace AIDevGallery.Pages;

internal sealed partial class ScenarioOverviewPage : Page
{
    public ScenarioOverviewPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        allView.ItemsSource = ScenarioCategoryHelpers.AllScenarioCategories;
    }

    private void ItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is Scenario scenario)
        {
            App.MainWindow.NavigateToPage(scenario);
        }
    }
}