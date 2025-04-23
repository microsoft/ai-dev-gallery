// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace AIDevGallery.Utils;

internal static class XamlHelpers
{
    public static Visibility VisibleWhenNotNull(object obj)
    {
        if (obj is null)
        {
            return Visibility.Collapsed;
        }
        else
        {
            return Visibility.Visible;
        }
    }

    public static Visibility VisibleWhenNull(object obj)
    {
        if (obj is null)
        {
            return Visibility.Visible;
        }
        else
        {
            return Visibility.Collapsed;
        }
    }
}