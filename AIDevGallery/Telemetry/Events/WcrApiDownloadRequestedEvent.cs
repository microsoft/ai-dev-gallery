// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class WcrApiDownloadRequestedEvent : EventBase
{
    internal WcrApiDownloadRequestedEvent(ModelType apiType, string? sampleId, DateTime startTime)
    {
        ApiType = apiType;
        SampleId = sampleId;
        StartTime = startTime;
    }

    public ModelType ApiType { get; private set; }

    public DateTime StartTime { get; private set; }

    public string? SampleId { get; set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(ModelType apiType, string? sampleId = null)
    {
        TelemetryFactory.Get<ITelemetry>().Log("WcrApiDownloadRequested_Event", LogLevel.Critical, new WcrApiDownloadRequestedEvent(apiType, sampleId, DateTime.Now));
    }
}