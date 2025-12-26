// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class FoundryLocalErrorEvent : EventBase
{
    internal FoundryLocalErrorEvent(string operation, string modelIdentifier, string errorMessage, DateTime eventTime)
    {
        Operation = operation;
        ModelIdentifier = modelIdentifier;
        ErrorMessage = errorMessage ?? string.Empty;
        EventTime = eventTime;
    }

    public string Operation { get; private set; }

    public string ModelIdentifier { get; private set; }

    public string ErrorMessage { get; private set; }

    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string operation, string modelIdentifier, string errorMessage)
    {
        var relatedActivityId = Guid.NewGuid();
        TelemetryFactory.Get<ITelemetry>().LogError(
            "FoundryLocalError_Event",
            LogLevel.Critical,
            new FoundryLocalErrorEvent(operation, modelIdentifier, errorMessage, DateTime.Now),
            relatedActivityId);
    }
}