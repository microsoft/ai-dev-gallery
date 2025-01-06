// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Converters;

internal partial class RecentUsedItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate ScenarioTemplate { get; set; } = null!;

    public DataTemplate ModelTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is MostRecentlyUsedItem selectedItem && selectedItem.Type == MostRecentlyUsedItemType.Scenario)
        {
            return ScenarioTemplate;
        }
        else
        {
            return ModelTemplate;
        }
    }
}