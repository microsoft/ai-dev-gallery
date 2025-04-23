// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Telemetry.Events;

internal static class ModelCacheMovedEvent
{
    public static void Log()
    {
        TelemetryFactory.Get<ITelemetry>().LogCritical("ModelCacheMoved_Event");
    }
}