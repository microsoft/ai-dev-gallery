// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace AIDevGallery.Utils;

/// <summary>
/// Extended status to surface publisher/package validation issues
/// </summary>
internal enum LimitedAccessFeatureExtendedStatus
{
    None = 0,
    PublisherIdMismatch = 1,
    InvalidPackageFamilyNameFormat = 2,
    PublisherIdValidationFailed = 3,
}

/// <summary>
/// Combines base LAF status with an extended status for additional diagnostics
/// </summary>
internal sealed class LimitedAccessFeatureExtendedResult
{
    public LimitedAccessFeatureStatus BaseStatus { get; init; }
    public LimitedAccessFeatureExtendedStatus ExtendedStatus { get; init; }

    public override string ToString()
    {
        return ExtendedStatus != LimitedAccessFeatureExtendedStatus.None
            ? $"{BaseStatus} ({ExtendedStatus})"
            : BaseStatus.ToString();
    }
}

/// <summary>
/// Helper class for managing Limited Access Features
/// </summary>
internal static class LimitedAccessFeaturesHelper
{
    // Tracks the last publisher/package validation outcome for extended diagnostics
    private static LimitedAccessFeatureExtendedStatus lastPublisherValidationStatus = LimitedAccessFeatureExtendedStatus.None;

    // Lazy caches to avoid repeated environment/MSBuild reads and string allocations
    private static readonly Lazy<string> S_aiLanguageModelToken = new Lazy<string>(() =>
    {
        var defineConstantsToken = ""/* INJECT_LAF_TOKEN */;
        if (!string.IsNullOrWhiteSpace(defineConstantsToken))
        {
            return defineConstantsToken;
        }

        var token = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_TOKEN_ENV);
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    });

    private static readonly Lazy<string> S_aiLanguageModelPublisherId = new Lazy<string>(() =>
    {
        var publisherId = ""/* INJECT_LAF_PUBLISHER_ID */;
        if (string.IsNullOrWhiteSpace(publisherId))
        {
            publisherId = Environment.GetEnvironmentVariable(AI_LANGUAGE_MODEL_PUBLISHER_ENV);
        }

        // Validate publisher ID against package family name if both are available
        if (!string.IsNullOrWhiteSpace(publisherId))
        {
            try
            {
                // Reset any previous extended status; set only on actual validation issues
                lastPublisherValidationStatus = LimitedAccessFeatureExtendedStatus.None;
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
                            lastPublisherValidationStatus = LimitedAccessFeatureExtendedStatus.PublisherIdMismatch;
                            return string.Empty;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid package family name format: '{packageFamilyName}'");
                        lastPublisherValidationStatus = LimitedAccessFeatureExtendedStatus.InvalidPackageFamilyNameFormat;
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to validate publisher ID against package family name: {ex.Message}");
                lastPublisherValidationStatus = LimitedAccessFeatureExtendedStatus.PublisherIdValidationFailed;

                // Continue with the publisher ID even if validation fails
            }
        }

        return string.IsNullOrWhiteSpace(publisherId) ? string.Empty : publisherId;
    });

    private static readonly Lazy<string> S_aiLanguageModelUsage = new Lazy<string>(() =>
    {
        var publisherId = S_aiLanguageModelPublisherId.Value;
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
        return S_aiLanguageModelToken.Value;
    }

    /// <summary>
    /// Builds the usage description for AI Language Model feature from DefineConstants/env
    /// </summary>
    private static string GetAiLanguageModelUsage() => S_aiLanguageModelUsage.Value;

    /// <summary>
    /// Gets the configured publisher identifier for AI Language Model feature from DefineConstants/env
    /// </summary>
    /// <returns>The publisher identifier string, or empty string if not configured</returns>
    public static string GetAiLanguageModelPublisherId()
    {
        return S_aiLanguageModelPublisherId.Value;
    }

    /// <summary>
    /// Evaluates current feature status using the configured token and usage text
    /// </summary>
    /// <returns>The current LimitedAccessFeatureStatus value</returns>
    public static LimitedAccessFeatureStatus GetCurrentStatus()
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
    /// Evaluates current feature status and augments it with extended diagnostics, if any
    /// </summary>
    /// <returns>Combined base status and extended status</returns>
    public static LimitedAccessFeatureExtendedResult GetCurrentExtendedStatus()
    {
        var baseStatus = GetCurrentStatus();

        // If feature is available, extended status is none
        if (baseStatus == LimitedAccessFeatureStatus.Available ||
            baseStatus == LimitedAccessFeatureStatus.AvailableWithoutToken)
        {
            return new LimitedAccessFeatureExtendedResult
            {
                BaseStatus = baseStatus,
                ExtendedStatus = LimitedAccessFeatureExtendedStatus.None
            };
        }

        // Otherwise, surface any validation issues captured earlier
        var extended = lastPublisherValidationStatus;

        return new LimitedAccessFeatureExtendedResult
        {
            BaseStatus = baseStatus,
            ExtendedStatus = extended
        };
    }

    /// <summary>
    /// Returns an agreed error code representing the current extended status.
    /// This is intended for UI display without leaking detailed diagnostics.
    /// </summary>
    /// <returns></returns>
    public static string GetCurrentExtendedStatusCode()
    {
        var result = GetCurrentExtendedStatus();

        if (result.BaseStatus == LimitedAccessFeatureStatus.Available ||
            result.BaseStatus == LimitedAccessFeatureStatus.AvailableWithoutToken)
        {
            return "OK";
        }

        // Prefer extended diagnostic when available
        switch (result.ExtendedStatus)
        {
            case LimitedAccessFeatureExtendedStatus.PublisherIdMismatch:
                return "E_LAF_PUBLISHER_ID_MISMATCH";
            case LimitedAccessFeatureExtendedStatus.InvalidPackageFamilyNameFormat:
                return "E_LAF_INVALID_PFN_FORMAT";
            case LimitedAccessFeatureExtendedStatus.PublisherIdValidationFailed:
                return "E_LAF_PUBLISHER_ID_VALIDATION_FAILED";
            case LimitedAccessFeatureExtendedStatus.None:
            default:
                break;
        }

        // Fallback to base status mapping
        return MapBaseStatusToCode(result.BaseStatus);
    }

    private static string MapBaseStatusToCode(LimitedAccessFeatureStatus status)
    {
        // Map common base statuses to stable error codes
        return status switch
        {
            LimitedAccessFeatureStatus.Unavailable => "E_LAF_UNAVAILABLE",
            _ => "E_LAF_UNKNOWN",
        };
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