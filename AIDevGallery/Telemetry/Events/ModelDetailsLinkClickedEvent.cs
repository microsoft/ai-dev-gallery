// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelDetailsLinkClickedEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string Link
    {
        get;
    }

    private ModelDetailsLinkClickedEvent(string link)
    {
        Link = link;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string link)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelDetailsLinkClicked_Event", LogLevel.Measure, new ModelDetailsLinkClickedEvent(link));
    }
}