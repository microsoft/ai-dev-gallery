// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Text;
#if ENABLE_VIDEO_API
using Microsoft.Windows.AI.Video;
#endif
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace AIDevGallery.Samples;

internal static class WcrApiHelpers
{
    private static readonly HashSet<ModelType> LanguageModelBacked = new()
    {
        ModelType.PhiSilica,
#if ENABLE_EXPERIMENTAL_API
        ModelType.PhiSilicaLora,
#endif
        ModelType.TextSummarizer,
        ModelType.TextRewriter,
        ModelType.TextToTableConverter
    };
    private static readonly Dictionary<ModelType, Func<AIFeatureReadyState>> CompatibilityCheckers = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.GetReadyState
        },
#if ENABLE_EXPERIMENTAL_API
        {
            ModelType.PhiSilicaLora, LanguageModel.GetReadyState
        },
#endif
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
#if ENABLE_IMAGE_FOREGROUND_API
        {
            ModelType.ForegroundExtractor, ImageForegroundExtractor.GetReadyState
        },
#endif
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.GetReadyState
        },
        {
            ModelType.ObjectRemover, ImageObjectRemover.GetReadyState
        },
#if ENABLE_IMAGE_GENERATOR_API
        {
            ModelType.SDXL, ImageGenerator.GetReadyState
        },
        {
            ModelType.RestyleImage, ImageGenerator.GetReadyState
        },
        {
            ModelType.ColoringBook, ImageGenerator.GetReadyState
        },
#endif
#if ENABLE_VIDEO_API
        {
            ModelType.VideoSuperRes, VideoScaler.GetReadyState
        }
#endif
    };

    public static readonly Dictionary<ModelType, Func<IAsyncOperationWithProgress<AIFeatureReadyResult, double>>> EnsureReadyFuncs = new()
    {
        {
            ModelType.PhiSilica, LanguageModel.EnsureReadyAsync
        },
#if ENABLE_EXPERIMENTAL_API
        {
            ModelType.PhiSilicaLora, LanguageModel.EnsureReadyAsync
        },
#endif
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
#if ENABLE_IMAGE_FOREGROUND_API
        {
            ModelType.ForegroundExtractor, ImageForegroundExtractor.EnsureReadyAsync
        },
#endif
        {
            ModelType.ObjectRemover, ImageObjectRemover.EnsureReadyAsync
        },
        {
            ModelType.ImageDescription, ImageDescriptionGenerator.EnsureReadyAsync
        },
#if ENABLE_IMAGE_GENERATOR_API
        {
            ModelType.SDXL, ImageGenerator.EnsureReadyAsync
        },
        {
            ModelType.RestyleImage, ImageGenerator.EnsureReadyAsync
        },
        {
            ModelType.ColoringBook, ImageGenerator.EnsureReadyAsync
        },
#endif
#if ENABLE_VIDEO_API
        {
            ModelType.VideoSuperRes, VideoScaler.EnsureReadyAsync
        }
#endif
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