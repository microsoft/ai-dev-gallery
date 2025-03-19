// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class AIToolkitActionClickedEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string ToolkitActionQueryName { get; }

    public string ModelName { get; }

    private AIToolkitActionClickedEvent(string toolkitActionQueryName, string modelName)
    {
        ToolkitActionQueryName = toolkitActionQueryName;
        ModelName = modelName;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings
    }

    public static void Log(string toolkitActionQueryName, string modelName)
    {
        TelemetryFactory.Get<ITelemetry>().Log("AIToolkitActionClicked_Event", LogLevel.Critical, new AIToolkitActionClickedEvent(toolkitActionQueryName, modelName));
    }
}