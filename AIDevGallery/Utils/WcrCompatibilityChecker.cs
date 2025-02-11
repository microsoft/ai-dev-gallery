// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Windows.AI.Generative;
using System;
using System.Collections.Generic;

namespace Utils;
internal static class WcrCompatibilityChecker
{
    private static readonly Dictionary<string, Func<bool>> CompatibilityCheckers = new Dictionary<string, Func<bool>>
    {
        {
            "phi-silica", LanguageModel.IsAvailable
        }
    };

    public static WcrApiAvailability GetApiAvailability(string key)
    {
        if (!CompatibilityCheckers.TryGetValue(key, out Func<bool>? isAvailable))
        {
            return WcrApiAvailability.NotSupported;
        }

        try
        {
            return isAvailable() ? WcrApiAvailability.Available : WcrApiAvailability.NotAvailable;
        }
        catch
        {
            return WcrApiAvailability.NotSupported;
        }
    }
}

internal enum WcrApiAvailability
{
    Available,
    NotAvailable,
    NotSupported
}