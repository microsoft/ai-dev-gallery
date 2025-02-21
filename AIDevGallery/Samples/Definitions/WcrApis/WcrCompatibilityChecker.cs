// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Vision;
using System;
using System.Collections.Generic;

namespace AIDevGallery.Samples;
internal static class WcrCompatibilityChecker
{
    private static readonly Dictionary<ModelType, Func<bool>> CompatibilityCheckers = new Dictionary<ModelType, Func<bool>>
    {
        {
            ModelType.PhiSilica, LanguageModel.IsAvailable
        },
        {
            ModelType.TextRecognitionOCR, TextRecognizer.IsAvailable
        },
        {
            ModelType.ImageScaler, ImageScaler.IsAvailable
        },
        {
            ModelType.BackgroundRemover, ImageObjectExtractor.IsAvailable
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.IsAvailable
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