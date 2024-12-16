// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDeletedEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string ModelName { get; }

    private ModelDeletedEvent(string modelName)
    {
        ModelName = modelName;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string modelName)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDeleted_Event", LogLevel.Measure, new ModelDeletedEvent(modelName));
    }
}