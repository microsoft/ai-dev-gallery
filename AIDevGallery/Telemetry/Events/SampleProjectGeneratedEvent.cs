// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class SampleProjectGeneratedEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string SampleId { get; }
    public string Model1Id { get; }
    public string Model2Id { get; }
    public bool CopyModelLocally { get; }

    private SampleProjectGeneratedEvent(string sampleId, string model1Id, string model2Id, bool copyModelLocally)
    {
        SampleId = sampleId;
        Model1Id = model1Id;
        Model2Id = model2Id;
        CopyModelLocally = copyModelLocally;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string sampleId, string model1Id, string model2Id, bool copyModelLocally)
    {
        TelemetryFactory.Get<ITelemetry>().Log("SampleProjectGenerated_Event", LogLevel.Measure, new SampleProjectGeneratedEvent(sampleId, model1Id, model2Id, copyModelLocally));
    }
}