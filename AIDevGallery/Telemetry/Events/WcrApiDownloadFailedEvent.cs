// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class WcrApiDownloadFailedEvent : EventBase
{
    internal WcrApiDownloadFailedEvent(ModelType apiType, string errorMessage, DateTime errorTime)
    {
        ApiType = apiType;
        ErrorMessage = errorMessage;
        ErrorTime = errorTime;
    }

    public ModelType ApiType { get; private set; }

    public DateTime ErrorTime { get; private set; }

    public string ErrorMessage { get; set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(ModelType apiType, string errorMessage)
    {
        var relatedActivityId = Guid.NewGuid();
        TelemetryFactory.Get<ITelemetry>().LogError("WcrApiDownloadFailed_Event", LogLevel.Critical, new WcrApiDownloadFailedEvent(apiType, errorMessage, DateTime.Now), relatedActivityId);
    }

    public static void Log(ModelType apiType, Exception ex)
    {
        var relatedActivityId = Guid.NewGuid();
        TelemetryFactory.Get<ITelemetry>().LogError("WcrApiDownloadFailed_Event", LogLevel.Critical, new WcrApiDownloadFailedEvent(apiType, ex.Message, DateTime.Now), relatedActivityId);
        TelemetryFactory.Get<ITelemetry>().LogException("WcrApiDownloadFailed_Event", ex, relatedActivityId);
    }
}