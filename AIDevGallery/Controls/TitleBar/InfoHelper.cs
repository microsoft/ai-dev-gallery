// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using Windows.System.Profile;

namespace AIDevGallery.Controls;

internal static class InfoHelper
{
    public static Version SystemVersion { get; }

    static InfoHelper()
    {
        string systemVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
        ulong version = ulong.Parse(systemVersion, CultureInfo.InvariantCulture);
        SystemVersion = new Version(
            (int)((version & 0xFFFF000000000000L) >> 48),
            (int)((version & 0x0000FFFF00000000L) >> 32),
            (int)((version & 0x00000000FFFF0000L) >> 16),
            (int)(version & 0x000000000000FFFFL));
    }
}