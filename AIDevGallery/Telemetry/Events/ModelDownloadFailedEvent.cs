// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDownloadFailedEvent : EventBase
{
    internal ModelDownloadFailedEvent(string modelUrl, DateTime errorTime)
    {
        ModelUrl = modelUrl;
        ErrorTime = errorTime;
    }

    public string ModelUrl { get; private set; }

    public DateTime ErrorTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl, Exception ex)
    {
        var relatedActivityId = Guid.NewGuid();
        TelemetryFactory.Get<ITelemetry>().LogError("ModelDownloadFailed_Event", LogLevel.Critical, new ModelDownloadFailedEvent(modelUrl, DateTime.Now), relatedActivityId);
        TelemetryFactory.Get<ITelemetry>().LogException("ModelDownloadFailed_Event", ex, relatedActivityId);
    }
}