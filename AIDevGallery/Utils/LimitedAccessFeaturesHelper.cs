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
    // Lazy caches to avoid repeated environment/MSBuild reads and string allocations
    private static readonly Lazy<string> s_aiLanguageModelToken = new Lazy<string>(() =>
    {
        var defineConstantsToken = LafConstants.Token;
        if (!string.IsNullOrWhiteSpace(defineConstantsToken))
        {
            return defineConstantsToken;
        }

        var token = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_TOKEN_ENV);
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    });

    private static readonly Lazy<string> s_aiLanguageModelPublisherId = new Lazy<string>(() =>
    {
        var publisherId = LafConstants.PublisherId;
        if (string.IsNullOrWhiteSpace(publisherId))
        {
            publisherId = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_PUBLISHER_ENV);
        }

        // Validate publisher ID against package family name if both are available
        if (!string.IsNullOrWhiteSpace(publisherId))
        {
            try
            {
                string packageFamilyName = Package.Current.Id.FamilyName;
                if (!string.IsNullOrWhiteSpace(packageFamilyName))
                {
                    string[] familyNameParts = packageFamilyName.Split('_');
                    if (familyNameParts.Length >= 2)
                    {
                        string publisherHash = familyNameParts[1];
                        if (publisherHash != publisherId)
                        {
                            Debug.WriteLine($"Publisher ID mismatch: expected '{publisherHash}' but got '{publisherId}'");
                            return string.Empty;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid package family name format: '{packageFamilyName}'");
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to validate publisher ID against package family name: {ex.Message}");
                // Continue with the publisher ID even if validation fails
            }
        }

        return string.IsNullOrWhiteSpace(publisherId) ? string.Empty : publisherId;
    });

    private static readonly Lazy<string> s_aiLanguageModelUsage = new Lazy<string>(() =>
    {
        var publisherId = s_aiLanguageModelPublisherId.Value;
        return $"{publisherId} has registered their use of {AI_LANGUAGE_MODEL_FEATURE_ID} with Microsoft and agrees to the terms of use.";
    });

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
        return s_aiLanguageModelToken.Value;
    }

    /// <summary>
    /// Builds the usage description for AI Language Model feature from DefineConstants/env
    /// </summary>
    private static string GetAiLanguageModelUsage() => s_aiLanguageModelUsage.Value;

    /// <summary>
    /// Gets the configured publisher identifier for AI Language Model feature from DefineConstants/env
    /// </summary>
    /// <returns>The publisher identifier string, or empty string if not configured</returns>
    public static string GetAiLanguageModelPublisherId()
    {
        return s_aiLanguageModelPublisherId.Value;
    }

    /// <summary>
    /// Evaluates current feature status using the configured token and usage text
    /// </summary>
    /// <returns>The current LimitedAccessFeatureStatus value</returns>
    private static LimitedAccessFeatureStatus GetCurrentStatus()
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
            Debug.WriteLine($"Failed to evaluate AI Language Model Limited Access Feature status: {ex.Message}");
            return LimitedAccessFeatureStatus.Unknown;
        }
    }

    /// <summary>
    /// Attempts to unlock the AI Language Model Limited Access Feature
    /// </summary>
    /// <returns>True if the feature is available, false otherwise</returns>
    public static bool TryUnlockAILanguageModel()
    {
        var status = GetCurrentStatus();
        bool isAvailable = (status == LimitedAccessFeatureStatus.Available) ||
                           (status == LimitedAccessFeatureStatus.AvailableWithoutToken);

        if (isAvailable)
        {
            Debug.WriteLine("AI Language Model Limited Access Feature unlocked successfully");
        }
        else
        {
            Debug.WriteLine($"AI Language Model Limited Access Feature not available. Status: {status}");
        }

        return isAvailable;
    }

    /// <summary>
    /// Checks if the AI Language Model feature is available without attempting to unlock it
    /// </summary>
    /// <returns>True if the feature is available, false otherwise</returns>
    public static bool IsAILanguageModelAvailable()
    {
        var status = GetCurrentStatus();
        return (status == LimitedAccessFeatureStatus.Available) ||
               (status == LimitedAccessFeatureStatus.AvailableWithoutToken);
    }
}