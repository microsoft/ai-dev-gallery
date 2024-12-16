// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDownloadStartEvent : EventBase
{
    internal ModelDownloadStartEvent(string modelUrl, DateTime startTime)
    {
        ModelUrl = modelUrl;
        StartTime = startTime;
    }

    public string ModelUrl { get; private set; }

    public DateTime StartTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDownloadStart_Event", LogLevel.Measure, new ModelDownloadStartEvent(modelUrl, DateTime.Now));
    }
}