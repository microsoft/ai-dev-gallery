// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class FoundryLocalDownloadEvent : EventBase
{
    internal FoundryLocalDownloadEvent(string modelAlias, bool success, string? errorMessage, DateTime eventTime)
    {
        ModelAlias = modelAlias;
        Success = success;
        ErrorMessage = errorMessage ?? string.Empty;
        EventTime = eventTime;
    }

    public string ModelAlias { get; private set; }

    public bool Success { get; private set; }

    public string ErrorMessage { get; private set; }

    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelAlias, bool success, string? errorMessage = null)
    {
        if (success)
        {
            TelemetryFactory.Get<ITelemetry>().Log(
                "FoundryLocalDownload_Event",
                LogLevel.Critical,
                new FoundryLocalDownloadEvent(modelAlias, success, errorMessage, DateTime.Now));
        }
        else
        {
            var relatedActivityId = Guid.NewGuid();
            TelemetryFactory.Get<ITelemetry>().LogError(
                "FoundryLocalDownload_Event",
                LogLevel.Critical,
                new FoundryLocalDownloadEvent(modelAlias, success, errorMessage, DateTime.Now),
                relatedActivityId);
        }
    }
}
