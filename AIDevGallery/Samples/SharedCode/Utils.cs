// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AIDevGallery.Samples.SharedCode;

internal static class Utils
{
    public static SolidColorBrush PhiMessageTypeToColor(ChatRole type)
    {
        return (type == ChatRole.User) ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(255, 68, 228, 255));
    }

    public static SolidColorBrush PhiMessageTypeToForeground(ChatRole type)
    {
        return (type == ChatRole.User) ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Color.FromArgb(255, 80, 80, 80));
    }

    public static Visibility BoolToVisibleInversed(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }
}