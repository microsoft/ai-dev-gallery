// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class OpenModelFolderEvent : EventBase
{
    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public string ModelUrl
    {
        get;
    }

    private OpenModelFolderEvent(string modelUrl)
    {
        ModelUrl = modelUrl;
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }

    public static void Log(string modelUrl)
    {
        TelemetryFactory.Get<ITelemetry>().Log("OpenModelFolder_Event", LogLevel.Critical, new OpenModelFolderEvent(modelUrl));
    }
}