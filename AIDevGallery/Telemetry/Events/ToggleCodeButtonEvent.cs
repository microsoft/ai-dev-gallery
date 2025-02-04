// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ToggleCodeButtonEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string Name { get; }
    public bool IsChecked { get; }

    private ToggleCodeButtonEvent(string name, bool isChecked)
    {
        Name = name;
        IsChecked = isChecked;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string name, bool isChecked)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ToggleCodeButton_Event", LogLevel.Critical, new ToggleCodeButtonEvent(name, isChecked));
    }
}