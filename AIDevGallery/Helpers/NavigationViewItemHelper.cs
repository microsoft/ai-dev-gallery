// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Helpers;

internal static class NavigationViewItemHelper
{
    public static IEnumerable<NavigationViewItem> Flatten(this IEnumerable<NavigationViewItem> source)
    {
        foreach (var item in source)
        {
            foreach (var subItem in Flatten(item))
            {
                yield return subItem;
            }
        }
    }

    public static IEnumerable<NavigationViewItem> Flatten(this NavigationViewItem source)
    {
        yield return source;

        foreach (var item in source.MenuItems.OfType<NavigationViewItem>())
        {
            foreach (var subItem in Flatten(item))
            {
                yield return subItem;
            }
        }
    }

    public static bool ExpandToItem(this NavigationView navigationView, NavigationViewItem targetItem)
    {
        foreach (var childItem in navigationView.MenuItems.OfType<NavigationViewItem>())
        {
            if (childItem == targetItem)
            {
                return true;
            }

            if (childItem.ExpandToItem(targetItem) is true)
            {
                return true;
            }
        }

        return false;
    }

    public static bool ExpandToItem(this NavigationViewItem parentItem, NavigationViewItem targetItem)
    {
        if (parentItem == targetItem)
        {
            return true;
        }

        foreach (var childItem in parentItem.MenuItems.OfType<NavigationViewItem>())
        {
            if (childItem == targetItem)
            {
                parentItem.IsExpanded = true;
                return true;
            }

            if (childItem.ExpandToItem(targetItem) is true)
            {
                parentItem.IsExpanded = true;
                return true;
            }
        }

        return false;
    }
}