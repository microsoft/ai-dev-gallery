// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Vision;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace AIDevGallery.Samples;
internal static class WcrApiHelpers
{
    private static readonly Dictionary<ModelType, Func<bool>> CompatibilityCheckers = new Dictionary<ModelType, Func<bool>>
    {
        {
            ModelType.PhiSilica, () => LanguageModel.GetReadyState() is not AIFeatureReadyState.DisabledByUser and
                                       not AIFeatureReadyState.NotSupportedOnCurrentSystem
        },
        {
            ModelType.TextRecognitionOCR, () => TextRecognizer.GetReadyState() is not AIFeatureReadyState.DisabledByUser and
                                                not AIFeatureReadyState.NotSupportedOnCurrentSystem
        },
        {
            ModelType.ImageScaler, () => ImageScaler.GetReadyState() is not AIFeatureReadyState.DisabledByUser and
                                         not AIFeatureReadyState.NotSupportedOnCurrentSystem
        },
        {
            ModelType.BackgroundRemover, () => ImageObjectExtractor.GetReadyState() is not AIFeatureReadyState.DisabledByUser and
                                               not AIFeatureReadyState.NotSupportedOnCurrentSystem
        },
        {
            ModelType.ImageDescription, () => ImageDescriptionGenerator.GetReadyState() is not AIFeatureReadyState.DisabledByUser and
                                              not AIFeatureReadyState.NotSupportedOnCurrentSystem
        }
    };

    public static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<AIFeatureReadyResult, double>>> MakeAvailables = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.EnsureReadyAsync
        },
        {
            ModelType.TextRecognitionOCR, TextRecognizer.EnsureReadyAsync
        },
        {
            ModelType.ImageScaler, ImageScaler.EnsureReadyAsync
        },
        {
            ModelType.BackgroundRemover, ImageObjectExtractor.EnsureReadyAsync
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.EnsureReadyAsync
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