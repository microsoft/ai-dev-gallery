// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Converters;

internal partial class SearchResultTemplateSelector : DataTemplateSelector
{
    public DataTemplate ScenarioTemplate { get; set; } = null!;

    public DataTemplate ModelTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is SearchResult selectedItem && selectedItem.Tag.GetType() == typeof(Scenario))
        {
            return ScenarioTemplate;
        }
        else
        {
            return ModelTemplate;
        }
    }
}