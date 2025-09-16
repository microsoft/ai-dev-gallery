// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Windows.ApplicationModel;
using System;
using System.Diagnostics;

namespace AIDevGallery.Utils;

/// <summary>
/// Helper class for managing Limited Access Features
/// </summary>
internal static class LimitedAccessFeaturesHelper
{
    /// <summary>
    /// Feature ID for AI Language Model
    /// </summary>
    private const string AI_LANGUAGE_MODEL_FEATURE_ID = "com.microsoft.windows.ai.languagemodel";
    
    /// <summary>
    /// Token for AI Language Model feature
    /// </summary>
    private const string AI_LANGUAGE_MODEL_TOKEN_ENV = "LAF_TOKEN";

    /// <summary>
    /// Publisher/Identifier used in the usage description, read from env var
    /// </summary>
    private const string AI_LANGUAGE_MODEL_PUBLISHER_ENV = "LAF_PUBLISHER_ID";

    /// <summary>
    /// Reads the AI Language Model token from environment variables
    /// </summary>
    private static string GetAiLanguageModelToken()
    {
        // Read from User/Machine/Process environment. Return empty string if not set.
        var token = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_TOKEN_ENV);
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    }

    /// <summary>
    /// Builds the usage description for AI Language Model feature from environment
    /// </summary>
    private static string GetAiLanguageModelUsage()
    {
        var publisherId = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_PUBLISHER_ENV);
        publisherId = string.IsNullOrWhiteSpace(publisherId) ? string.Empty : publisherId;
        return $"{publisherId} has registered their use of {AI_LANGUAGE_MODEL_FEATURE_ID} with Microsoft and agrees to the terms of use.";
    }

    /// <summary>
    /// Attempts to unlock the AI Language Model Limited Access Feature
    /// </summary>
    /// <returns>True if the feature is available, false otherwise</returns>
    public static bool TryUnlockAILanguageModel()
    {
        try
        {
            var access = LimitedAccessFeatures.TryUnlockFeature(
                AI_LANGUAGE_MODEL_FEATURE_ID,
                GetAiLanguageModelToken(),
                GetAiLanguageModelUsage());

            bool isAvailable = (access.Status == LimitedAccessFeatureStatus.Available) ||
                              (access.Status == LimitedAccessFeatureStatus.AvailableWithoutToken);

            if (isAvailable)
            {
                Debug.WriteLine("AI Language Model Limited Access Feature unlocked successfully");
            }
            else
            {
                Debug.WriteLine($"AI Language Model Limited Access Feature not available. Status: {access.Status}");
            }

            return isAvailable;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unlock AI Language Model Limited Access Feature: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if the AI Language Model feature is available without attempting to unlock it
    /// </summary>
    /// <returns>True if the feature is available, false otherwise</returns>
    public static bool IsAILanguageModelAvailable()
    {
        try
        {
            var access = LimitedAccessFeatures.TryUnlockFeature(
                AI_LANGUAGE_MODEL_FEATURE_ID,
                GetAiLanguageModelToken(),
                GetAiLanguageModelUsage());

            return (access.Status == LimitedAccessFeatureStatus.Available) ||
                   (access.Status == LimitedAccessFeatureStatus.AvailableWithoutToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check AI Language Model Limited Access Feature status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current status of the AI Language Model feature
    /// </summary>
    /// <returns>The current status of the feature</returns>
    public static LimitedAccessFeatureStatus GetAILanguageModelStatus()
    {
        try
        {
            var access = LimitedAccessFeatures.TryUnlockFeature(
                AI_LANGUAGE_MODEL_FEATURE_ID,
                GetAiLanguageModelToken(),
                GetAiLanguageModelUsage());

            return access.Status;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get AI Language Model Limited Access Feature status: {ex.Message}");
            return LimitedAccessFeatureStatus.Unknown;
        }
    }
}
