// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Controls;

internal partial class SamplesCarousel : UserControl
{
    public SamplesCarousel()
    {
        this.InitializeComponent();
        SetupSampleView();
    }

    private void SetupSampleView()
    {
        if (App.AppData.MostRecentlyUsedItems.Count > 0)
        {
            RecentItem.Visibility = Visibility.Visible;

            foreach (var item in App.AppData.MostRecentlyUsedItems)
            {
                RowSample s = new()
                {
                    Title = item.DisplayName,
                    Icon = new FontIcon() { Glyph = item.Icon },
                    Description = item.Description,
                    Id = item.ItemId
                };
                RecentItemsRow.SampleCards.Add(s);
            }
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.AppData.MostRecentlyUsedItems.Count > 0)
        {
            FilterBar.SelectedItem = FilterBar.Items[0];
        }
        else
        {
            FilterBar.SelectedItem = FilterBar.Items[1];
        }
    }
}