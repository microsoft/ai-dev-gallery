// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Text;
using Microsoft.Windows.AI.Video;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace AIDevGallery.Samples;

internal static class WcrApiHelpers
{
    private static readonly HashSet<ModelType> LanguageModelBacked = new()
    {
        ModelType.PhiSilica,
        ModelType.PhiSilicaLora,
        ModelType.TextSummarizer,
        ModelType.TextRewriter,
        ModelType.TextToTableConverter
    };
    private static readonly Dictionary<ModelType, Func<AIFeatureReadyState>> CompatibilityCheckers = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.GetReadyState
        },
        {
            ModelType.PhiSilicaLora, LanguageModel.GetReadyState
        },
        {
            ModelType.TextSummarizer, LanguageModel.GetReadyState
        },
        {
            ModelType.TextRewriter, LanguageModel.GetReadyState
        },
        {
            ModelType.TextToTableConverter, LanguageModel.GetReadyState
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
            ModelType.ForegroundExtractor, ImageForegroundExtractor.GetReadyState
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.GetReadyState
        },
        {
            ModelType.ObjectRemover, ImageObjectRemover.GetReadyState
        },
        {
            ModelType.SDXL, ImageGenerator.GetReadyState
        },
        {
            ModelType.RestyleImage, ImageGenerator.GetReadyState
        },
        {
            ModelType.ColoringBook, ImageGenerator.GetReadyState
        },
        {
            ModelType.VideoSuperRes, VideoScaler.GetReadyState
        }
    };

    public static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<AIFeatureReadyResult, double>>> EnsureReadyFuncs = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.EnsureReadyAsync
        },
        {
            ModelType.PhiSilicaLora, LanguageModel.EnsureReadyAsync
        },
        {
            ModelType.TextSummarizer, LanguageModel.EnsureReadyAsync
        },
        {
            ModelType.TextRewriter, LanguageModel.EnsureReadyAsync
        },
        {
            ModelType.TextToTableConverter, LanguageModel.EnsureReadyAsync
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
            ModelType.ForegroundExtractor, ImageForegroundExtractor.EnsureReadyAsync
        },
        {
            ModelType.ObjectRemover, ImageObjectRemover.EnsureReadyAsync
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.EnsureReadyAsync
        },
        {
            ModelType.SDXL, ImageGenerator.EnsureReadyAsync
        },
        {
            ModelType.RestyleImage, ImageGenerator.EnsureReadyAsync
        },
        {
            ModelType.ColoringBook, ImageGenerator.EnsureReadyAsync
        },
        {
            ModelType.VideoSuperRes, VideoScaler.EnsureReadyAsync
        }
    };

    // this is a workaround for GetReadyState not returning Ready after EnsureReadyAsync is called
    // for now, we will track when EnsureReadyAsync succeeds for each model to ensure we are not
    // blocking the samples from running until this bug is fixed
    public static readonly Dictionary<ModelType, bool> IsModelReadyWorkaround = new();

    public static AIFeatureReadyState GetApiAvailability(ModelType type)
    {
        if (!CompatibilityCheckers.TryGetValue(type, out var getReadyStateFunction))
        {
            return AIFeatureReadyState.NotSupportedOnCurrentSystem;
        }

        try
        {
            // Pre-check LAF availability for LanguageModel-backed APIs
            if (LanguageModelBacked.Contains(type) && !LimitedAccessFeaturesHelper.IsAILanguageModelAvailable())
            {
                return AIFeatureReadyState.NotSupportedOnCurrentSystem;
            }

            return getReadyStateFunction();
        }
        catch
        {
            return AIFeatureReadyState.NotSupportedOnCurrentSystem;
        }
    }

    public static string GetStringDescription(ModelType type, AIFeatureReadyState state)
    {
        switch (state)
        {
            case AIFeatureReadyState.Ready:
                return "Ready";
            case AIFeatureReadyState.NotReady:
                return "API requires a model download or update.";
            case AIFeatureReadyState.DisabledByUser:
                return "API is disabled by the user in the Windows settings.";
            case AIFeatureReadyState.NotSupportedOnCurrentSystem:
                if (LanguageModelBacked.Contains(type) && !LimitedAccessFeaturesHelper.IsAILanguageModelAvailable())
                {
                    return LimitedAccessFeaturesHelper.GetCurrentExtendedStatusCode();
                }

                return "Not supported on this system.";
            default:
                return string.Empty;
        }
    }
}