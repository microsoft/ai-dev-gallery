// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI.Generative;
using Microsoft.Windows.Management.Deployment;
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

    public static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>>> MakeAvailables = new Dictionary<ModelType, Func<IAsyncOperationWithProgress<PackageDeploymentResult, PackageDeploymentProgress>>>
    {
        {
            ModelType.PhiSilica, LanguageModel.MakeAvailableAsync
        },
        {
            ModelType.TextRecognitionOCR, TextRecognizer.MakeAvailableAsync
        },
        {
            ModelType.ImageScaler, ImageScaler.MakeAvailableAsync
        },
        {
            ModelType.BackgroundRemover, ImageObjectExtractor.MakeAvailableAsync
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.MakeAvailableAsync
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