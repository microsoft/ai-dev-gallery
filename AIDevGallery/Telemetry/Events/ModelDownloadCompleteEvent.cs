// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDownloadCompleteEvent : EventBase
{
    internal ModelDownloadCompleteEvent(string modelUrl, DateTime completeTime)
    {
        ModelUrl = modelUrl;
        CompleteTime = completeTime;
    }

    public string ModelUrl { get; private set; }

    public DateTime CompleteTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDownloadComplete_Event", LogLevel.Critical, new ModelDownloadCompleteEvent(modelUrl, DateTime.Now));
    }
}