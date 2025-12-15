// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System;
using System.Diagnostics.Tracing;

namespace AIDevGallery.Telemetry.Events;

[EventData]
internal class ModelIntegrityVerificationFailedEvent : EventBase
{
    internal ModelIntegrityVerificationFailedEvent(string modelUrl, string fileName, string verificationType, string expectedValue, string actualValue)
    {
        ModelUrl = modelUrl;
        FileName = fileName;
        VerificationType = verificationType;
        ExpectedValue = expectedValue;
        ActualValue = actualValue;
        EventTime = DateTime.UtcNow;
    }

    public string ModelUrl { get; private set; }
    public string FileName { get; private set; }
    public string VerificationType { get; private set; }
    public string ExpectedValue { get; private set; }
    public string ActualValue { get; private set; }
    public DateTime EventTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string modelUrl, string fileName, string verificationType, string expectedValue, string actualValue)
    {
        TelemetryFactory.Get<ITelemetry>().LogError(
            "ModelIntegrityVerificationFailed_Event",
            LogLevel.Error,
            new ModelIntegrityVerificationFailedEvent(modelUrl, fileName, verificationType, expectedValue, actualValue));
    }
}