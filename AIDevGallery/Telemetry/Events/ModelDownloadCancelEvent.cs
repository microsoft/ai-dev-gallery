// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDownloadCancelEvent : EventBase
{
    internal ModelDownloadCancelEvent(string modelUrl, DateTime canceledTime)
    {
        ModelUrl = modelUrl;
        CanceledTime = canceledTime;
    }

    public string ModelUrl { get; private set; }

    public DateTime CanceledTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDownloadCancel_Event", LogLevel.Critical, new ModelDownloadCancelEvent(modelUrl, DateTime.Now));
    }
}