// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Telemetry.Events;

internal static class ModelCacheDeletedEvent
{
    public static void Log()
    {
        TelemetryFactory.Get<ITelemetry>().LogCritical("ModelCacheDeleted_Event");
    }
}