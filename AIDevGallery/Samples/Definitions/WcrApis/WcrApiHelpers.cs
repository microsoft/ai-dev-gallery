// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.MachineLearning;
using Microsoft.Windows.AI.Speech;
using Microsoft.Windows.AI.Text;
using Microsoft.Windows.AI.Video;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace AIDevGallery.Samples;

internal static class WcrApiHelpers
{
    // The requirement sentence is derived from each API's SupportedHardwareAccelerators - https://learn.microsoft.com/en-us/windows/ai/apis/#supported-hardware
    private const string DefaultHardwareRequirement = "A Copilot+ PC is required.";
    private const string CopilotPlusOrGpuRequirement = "A Copilot+ PC, or a Windows 11 PC with a supported GPU, is required.";
    private const string CopilotPlusOrCpuRequirement = "A Copilot+ PC, or a Windows 11 PC with a supported CPU, is required.";
    private const string DefaultSupportedHardwareUrl = "https://learn.microsoft.com/windows/ai/apis/#supported-hardware";

    private static readonly HashSet<ModelType> LanguageModelBacked = new()
    {
        ModelType.PhiSilica,
        ModelType.PhiSilicaLora,
        ModelType.TextSummarizer,
        ModelType.TextRewriter,
        ModelType.TextToTableConverter,
        ModelType.DescribeYourChange
    };

    private static readonly HashSet<ModelType> ImageGeneratorBacked = new()
    {
        ModelType.SDXL,
        ModelType.RestyleImage,
        ModelType.ColoringBook
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
            ModelType.DescribeYourChange, LanguageModel.GetReadyState
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
        },
        {
            ModelType.SpeechRecognition, SpeechRecognitionModel.GetReadyState
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
            ModelType.DescribeYourChange, LanguageModel.EnsureReadyAsync
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
        },
        {
            ModelType.SpeechRecognition, EnsureSpeechRecognitionModelReadyAsync
        }
    };

    // SpeechRecognitionModel.EnsureReadyAsync reports progress as SpeechRecognitionModelProgress,
    // so adapt it to the IAsyncOperationWithProgress<AIFeatureReadyResult, double> shape the gallery expects.
    private static IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureSpeechRecognitionModelReadyAsync()
    {
        return AsyncInfo.Run<AIFeatureReadyResult, double>(async (cancellationToken, progress) =>
        {
            progress.Report(0);
            var catalog = ExecutionProviderCatalog.GetDefault();
            await catalog.EnsureAndRegisterCertifiedAsync().AsTask(cancellationToken);

            var inner = SpeechRecognitionModel.EnsureReadyAsync();
            inner.Progress = (_, p) => progress.Report(p.Progress);
            using var registration = cancellationToken.Register(() => inner.Cancel());
            return await inner;
        });
    }

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

                if (IsImageGeneratorBacked(type))
                {
                    return "Not supported on this system. This feature is only available on devices enrolled in the Windows Insider Program (Dev or Beta channel).";
                }

                return "Not supported on this system.";
            default:
                return string.Empty;
        }
    }

    public static bool IsImageGeneratorBacked(ModelType type)
    {
        return ImageGeneratorBacked.Contains(type);
    }

    // Returns the hardware requirement info - https://learn.microsoft.com/en-us/windows/ai/apis/#supported-hardware
    public static (string Requirement, string LearnMoreUri) GetHardwareRequirementInfo(ModelType type)
    {
        if (!ModelTypeHelpers.ApiDefinitionDetails.TryGetValue(type, out var apiDefinition))
        {
            return (DefaultHardwareRequirement, DefaultSupportedHardwareUrl);
        }

        var learnMoreUri = string.IsNullOrEmpty(apiDefinition.SupportedHardwareUrl)
            ? DefaultSupportedHardwareUrl
            : apiDefinition.SupportedHardwareUrl!;

        var runsOnGpu = apiDefinition.SupportedHardwareAccelerators?.Contains(HardwareAccelerator.GPU) == true;
        var runsOnCpu = apiDefinition.SupportedHardwareAccelerators?.Contains(HardwareAccelerator.CPU) == true;
        var requirement = (runsOnGpu, runsOnCpu) switch
        {
            (true, true) => string.Empty,
            (true, false) => CopilotPlusOrGpuRequirement,
            (false, true) => CopilotPlusOrCpuRequirement,
            _ => DefaultHardwareRequirement,
        };

        return (requirement, learnMoreUri);
    }
}