// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using System.Collections;

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

    public static Visibility VisibleWhenEmpty(IEnumerable enumerable)
    {
        if (enumerable is null)
        {
            return Visibility.Visible;
        }
        else if (enumerable is ICollection collection)
        {
            return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            return enumerable.GetEnumerator().MoveNext() ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public static Visibility VisibleWhenNotEmpty(IEnumerable enumerable)
    {
        if (enumerable is null)
        {
            return Visibility.Collapsed;
        }
        else if (enumerable is ICollection collection)
        {
            return collection.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            return enumerable.GetEnumerator().MoveNext() ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}