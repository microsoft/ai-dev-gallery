// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Windows.AI.Generative;
using System;
using System.Collections.Generic;

namespace Utils;
internal static class WcrCompatibilityChecker
{
    private static readonly Dictionary<ModelType, Func<bool>> CompatibilityCheckers = new Dictionary<ModelType, Func<bool>>
    {
        {
            ModelType.PhiSilica, LanguageModel.IsAvailable
        }
    };

    public static WcrApiAvailability GetApiAvailability(ModelType type)
    {
        if (!CompatibilityCheckers.TryGetValue(type, out Func<bool>? isAvailable))
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