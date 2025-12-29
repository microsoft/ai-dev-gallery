// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

/// <summary>
/// Telemetry event for successful Foundry Local operations.
/// Use this for tracking successful operations like model preparation, loading, etc.
/// </summary>
[EventData]
internal class FoundryLocalOperationEvent : EventBase
{
    internal FoundryLocalOperationEvent(
        string operation,
        string modelIdentifier,
        double? durationSeconds,
        DateTime eventTime)
    {
        Operation = operation;
        ModelIdentifier = modelIdentifier;
        DurationSeconds = durationSeconds ?? 0;
        EventTime = eventTime;
    }

    public string Operation { get; private set; }

    public string ModelIdentifier { get; private set; }

    public double DurationSeconds { get; private set; }

    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    /// <summary>
    /// Logs a successful Foundry Local operation.
    /// </summary>
    /// <param name="operation">Operation name in PascalCase (e.g., "ModelPrepare", "ModelLoad", "ModelDelete")</param>
    /// <param name="modelIdentifier">Model alias or ID</param>
    /// <param name="durationSeconds">Optional duration in seconds</param>
    public static void Log(string operation, string modelIdentifier, double? durationSeconds = null)
    {
        TelemetryFactory.Get<ITelemetry>().Log(
            "FoundryLocalOperation_Event",
            LogLevel.Info,
            new FoundryLocalOperationEvent(operation, modelIdentifier, durationSeconds, DateTime.Now));
    }
}

/// <summary>
/// Telemetry event for Foundry Local model download operations.
/// Tracks download success/failure, file size, and duration.
/// </summary>
[EventData]
internal class FoundryLocalDownloadEvent : EventBase
{
    internal FoundryLocalDownloadEvent(
        string modelAlias,
        bool success,
        string? errorMessage,
        long? fileSizeMb,
        double? durationSeconds,
        DateTime eventTime)
    {
        ModelAlias = modelAlias;
        Success = success;
        ErrorMessage = errorMessage ?? string.Empty;
        FileSizeMb = fileSizeMb ?? 0;
        DurationSeconds = durationSeconds ?? 0;
        EventTime = eventTime;
    }

    public string ModelAlias { get; private set; }

    public bool Success { get; private set; }

    public string ErrorMessage { get; private set; }

    public long FileSizeMb { get; private set; }

    public double DurationSeconds { get; private set; }

    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(
        string modelAlias,
        bool success,
        string? errorMessage = null,
        long? fileSizeMb = null,
        double? durationSeconds = null)
    {
        if (success)
        {
            TelemetryFactory.Get<ITelemetry>().Log(
                "FoundryLocalDownload_Event",
                LogLevel.Info,
                new FoundryLocalDownloadEvent(modelAlias, success, errorMessage, fileSizeMb, durationSeconds, DateTime.Now));
        }
        else
        {
            var relatedActivityId = Guid.NewGuid();
            TelemetryFactory.Get<ITelemetry>().LogError(
                "FoundryLocalDownload_Event",
                LogLevel.Critical,
                new FoundryLocalDownloadEvent(modelAlias, success, errorMessage, fileSizeMb, durationSeconds, DateTime.Now),
                relatedActivityId);
        }
    }
}

/// <summary>
/// Telemetry event for Foundry Local errors.
/// Operation naming convention: PascalCase action names (e.g., "ClientInitialization", "ModelDownload", "ModelPrepare")
/// </summary>
[EventData]
internal class FoundryLocalErrorEvent : EventBase
{
    internal FoundryLocalErrorEvent(
        string operation,
        string phase,
        string modelIdentifier,
        string errorMessage,
        DateTime eventTime)
    {
        Operation = operation;
        Phase = phase;
        ModelIdentifier = modelIdentifier;
        ErrorMessage = errorMessage ?? string.Empty;
        EventTime = eventTime;
    }

    public string Operation { get; private set; }

    public string Phase { get; private set; }

    public string ModelIdentifier { get; private set; }

    public string ErrorMessage { get; private set; }

    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string operation, string phase, string modelIdentifier, string errorMessage)
    {
        var relatedActivityId = Guid.NewGuid();
        TelemetryFactory.Get<ITelemetry>().LogError(
            "FoundryLocalError_Event",
            LogLevel.Critical,
            new FoundryLocalErrorEvent(operation, phase, modelIdentifier, errorMessage, DateTime.Now),
            relatedActivityId);
    }
}