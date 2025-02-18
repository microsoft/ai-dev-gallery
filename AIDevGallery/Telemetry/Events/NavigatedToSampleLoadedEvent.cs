// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class NavigatedToSampleLoadedEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string Name { get; private set; }

    public DateTime CompleteTime { get; private set; }

    private NavigatedToSampleLoadedEvent(string name, DateTime completeTime)
    {
        Name = name;
        CompleteTime = completeTime;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string name)
    {
        TelemetryFactory.Get<ITelemetry>().Log("NavigatedToSampleLoaded_Event", LogLevel.Critical, new NavigatedToSampleLoadedEvent(name, DateTime.Now));
    }
}