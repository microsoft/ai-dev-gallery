// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDownloadEnqueueEvent : EventBase
{
    internal ModelDownloadEnqueueEvent(string modelUrl, DateTime enqueuedTime)
    {
        ModelUrl = modelUrl;
        EnqueuedTime = enqueuedTime;
    }

    public string ModelUrl { get; private set; }

    public DateTime EnqueuedTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDownloadEnqueue_Event", LogLevel.Critical, new ModelDownloadEnqueueEvent(modelUrl, DateTime.Now));
    }
}