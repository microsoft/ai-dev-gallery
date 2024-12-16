// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry.Internal;
using System;

namespace AIDevGallery.Telemetry.Events;

internal class ModelCacheMovedEvent : EventBase
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly string newPath;
#pragma warning restore IDE0052 // Remove unread private members

    private ModelCacheMovedEvent(string newPath)
    {
        this.newPath = newPath;
    }

    public override PartA_PrivTags PartA_PrivTags => throw new NotImplementedException();

    public static void Log(string newPath)
    {
        TelemetryFactory.Get<ITelemetry>().Log("ModelCacheMoved_Event", LogLevel.Measure, new ModelCacheMovedEvent(newPath));
    }

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
        // No sensitive strings to replace.
    }
}