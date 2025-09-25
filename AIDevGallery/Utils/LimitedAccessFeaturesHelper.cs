// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Windows.ApplicationModel;

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
    /// Reads the AI Language Model token, preferring DefineConstants over environment variables
    /// </summary>
    /// <returns>The AI Language Model token string, or empty string if not available</returns>
    public static string GetAiLanguageModelToken()
    {
        // Prefer value from DefineConstants (from MSBuild) if present
        var defineConstantsToken = LafConstants.Token;
        if (!string.IsNullOrWhiteSpace(defineConstantsToken))
        {
            return defineConstantsToken;
        }

        // Fallback to User/Machine/Process environment variable. Return empty string if not set.
        var token = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_TOKEN_ENV);
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    }

    /// <summary>
    /// Builds the usage description for AI Language Model feature from DefineConstants/env
    /// </summary>
    private static string GetAiLanguageModelUsage()
    {
        var publisherId = LafConstants.PublisherId;
        if (string.IsNullOrWhiteSpace(publisherId))
        {
            publisherId = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_PUBLISHER_ENV);
        }

        publisherId = string.IsNullOrWhiteSpace(publisherId) ? string.Empty : publisherId;
        return $"{publisherId} has registered their use of {AI_LANGUAGE_MODEL_FEATURE_ID} with Microsoft and agrees to the terms of use.";
    }

    /// <summary>
    /// Gets the configured publisher identifier for AI Language Model feature from DefineConstants/env
    /// </summary>
    /// <returns>The publisher identifier string, or empty string if not configured</returns>
    public static string GetAiLanguageModelPublisherId()
    {
        var publisherId = LafConstants.PublisherId;
        if (string.IsNullOrWhiteSpace(publisherId))
        {
            publisherId = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_PUBLISHER_ENV);
        }

        return string.IsNullOrWhiteSpace(publisherId) ? string.Empty : publisherId;
    }

    /// <summary>
    /// Reads a value from DefineConstants by key; returns empty string if missing
    /// </summary>
    private static string GetDefineConstantsValue(string key)
    {
        try
        {
#if LAF_TOKEN
            if (string.Equals(key, "LAF_TOKEN", StringComparison.Ordinal))
            {
                return LAF_TOKEN;
            }
#endif
#if LAF_PUBLISHER_ID
            if (string.Equals(key, "LAF_PUBLISHER_ID", StringComparison.Ordinal))
            {
                return LAF_PUBLISHER_ID;
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read DefineConstants '{key}': {ex.Message}");
        }

        return string.Empty;
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