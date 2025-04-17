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
    private static readonly Dictionary<ModelType, Func<AIFeatureReadyState>> CompatibilityCheckers = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.GetReadyState
        },
        {
            ModelType.TextRecognitionOCR, TextRecognizer.GetReadyState
        },
        {
            ModelType.ImageScaler, ImageScaler.GetReadyState
        },
        {
            ModelType.BackgroundRemover, ImageObjectExtractor.GetReadyState
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.GetReadyState
        }
    };

    public static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<AIFeatureReadyResult, double>>> EnsureReadyFuncs = new()
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

    public static AIFeatureReadyState GetApiAvailability(ModelType type)
    {
        if (!CompatibilityCheckers.TryGetValue(type, out var getReadyStateFunction))
        {
            return AIFeatureReadyState.NotSupportedOnCurrentSystem;
        }

        return getReadyStateFunction();
    }

    public static string GetStringDescription(this AIFeatureReadyState state)
    {
        switch (state)
        {
            case AIFeatureReadyState.Ready:
                return "Ready";
            case AIFeatureReadyState.NotSupportedOnCurrentSystem:
                return "Not supported on this system.";
            case AIFeatureReadyState.DisabledByUser:
                return "API is disabled by the user in the Windows settings.";
            case AIFeatureReadyState.EnsureNeeded:
                return "API requires a model download or update.";
            default:
                return string.Empty;
        }
    }
}