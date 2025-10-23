### AI Dev Gallery Telemetry Guide

This guide summarizes the telemetry implementation in this repo, the event catalog and how to view it locally.

---

## Overview
- **Provider name**: `Microsoft.Windows.AIDevGallery`
- **Implementation**: `EventSource` with Windows ETW TraceLogging
- **Code locations**:
  - `AIDevGallery/Telemetry/Telemetry.cs` (core logic)
  - `AIDevGallery/Telemetry/TelemetryFactory.cs` (singleton factory)
  - `AIDevGallery/Telemetry/ITelemetry.cs`, `AIDevGallery/Telemetry/LogLevel.cs`
  - `AIDevGallery/Telemetry/TelemetryEventSource.cs` (keywords/tags)
  - `AIDevGallery/Telemetry/PrivacyConsentHelpers.cs` (privacy-sensitive regions)
  - `AIDevGallery/Telemetry/Events/*.cs` (event data contracts and logging entry points)

---

## Collection and Write Flow
1) Product code calls static `Log(...)`/`LogError(...)`/`LogCritical(...)` methods defined under `Telemetry/Events/*.cs`; parameters are the public properties on the event type.
2) Calls go through the `ITelemetry` implementation (`Telemetry.cs`), which performs sensitive string replacement (see “Sensitive Information Handling”).
3) Based on `LogLevel` and `IsDiagnosticTelemetryOn`, events may upload or be logged locally only:
   - When `IsDiagnosticTelemetryOn == false`, non-error `Info`/`Measure` events are downgraded to `Local` (local-only).
   - `Critical` and error-level events are not downgraded.
4) Finally, events are written via `EventSource.Write(...)` using TraceLogging:
   - Provider: `Microsoft.Windows.AIDevGallery`
   - Keywords: `TelemetryKeyword` / `MeasuresKeyword` / `CriticalDataKeyword`

Key code snippet:

```text
// Provider name
private const string ProviderName = "Microsoft.Windows.AIDevGallery"; // in Telemetry.cs

// Downgrade (when diagnostics off, non-error Info/Measure → Local)
if (!IsDiagnosticTelemetryOn)
{
    if (!isError && (level == LogLevel.Measure || level == LogLevel.Info))
    {
        level = LogLevel.Local;
    }
}

// Final write to ETW
TelemetryEventSourceInstance.Write(eventName, ref telemetryOptions, ref activityId, ref relatedActivityId, ref data);
```

---

## View Telemetry Locally (Debug/Verification)

### Method 1: PerfView (recommended)
1) Run PerfView as Administrator.
2) Collect → Collect:
   - Uncheck Thread Time, CPU, .NET/CLR, Kernel (set Kernel Events to None under Advanced).
   - In Additional Providers, manually enter:
     - `Microsoft.Windows.AIDevGallery:0xFFFFFFFFFFFFFFFF:Verbose`
3) Start → exercise the app → Stop.
4) Open the produced etl.zip → Events; filter by Provider `Microsoft.Windows.AIDevGallery` to inspect events and their public properties.

Tip: If the provider is not in the dropdown, manually type the provider string above.

### Method 2: PerfView Listen (live)
- Run → Listen → Providers `Microsoft.Windows.AIDevGallery:0xFFFFFFFFFFFFFFFF:Verbose` → Start → exercise the app to see live events.

### Method 3: logman (command line, minimal)
```bat
logman start AIGalleryOnly -p Microsoft.Windows.AIDevGallery 0xFFFFFFFFFFFFFFFF 5 -ets
rem After exercising the app
logman stop AIGalleryOnly -ets
```
The resulting .etl can be opened with PerfView/WPA; or convert to CSV via `tracerpt`.

### Method 4: dotnet-trace (attach to process)
```bat
dotnet-trace collect --process-id <PID> --providers Microsoft.Windows.AIDevGallery:0xFFFFFFFFFFFFFFFF:Verbose
```

---

## Event Catalog (by topic)

- Navigation (Log)
  - `NavigatedToPage_Event`
  - `NavigatedToSample_Event`
  - `NavigatedToSampleLoaded_Event`

- Interaction/UI (Log)
  - `ButtonClicked_Event`
  - `ToggleCodeButton_Event`
  - `AIToolkitActionClicked_Event`

- Search & Result (Log)
  - `SearchModel_Event`
  - `DownloadSearchedModel_Event`

- Deep link / Links / Files (Log)
  - `DeepLinkActivated_Event`
  - `ModelDetailsLinkClicked_Event`
  - `OpenModelFolder_Event`

- Model download / model ops
  - Log: `ModelDownloadEnqueue_Event`, `ModelDownloadStart_Event`, `ModelDownloadComplete_Event`, `ModelDownloadCancel_Event`, `ModelDeleted_Event`
  - LogError: `ModelDownloadFailed_Event`

- WCR API
  - Log: `WcrApiDownloadRequested_Event`
  - LogError: `WcrApiDownloadFailed_Event`

- Cache maintenance (LogCritical)
  - `ModelCacheMoved_Event`
  - `ModelCacheDeleted_Event`

- Samples/Project (Log)
  - `SampleInteraction_Event`
  - `SampleProjectGenerated_Event`

- Others (via `LogException`, event name `ExceptionThrown`, includes exception name/message/stack; see next sections)

---

## Event Data & Sensitive Information Handling

- Event “public properties” are the fields written; they are defined on `[EventData]` classes under `Telemetry/Events/*.cs`.
- On singleton initialization, “well-known sensitive strings” are added for replacement:
  - User directory (e.g., `C:\Users\<name>` → `<UserDirectory>`)
  - User name / current user (→ `<UserName>` / `<CurrentUserName>`)
- `Log/LogError` call `ReplaceSensitiveStrings(...)` on the event. Most events implement no-op replacement; fields such as `ModelUrl`/`Uri`/`Query`/`Link` are only replaced when they contain known sensitive substrings.
- `LogException` replaces sensitive substrings in `Message` and `InnerException.Message`; type names and stack traces are recorded as-is for diagnostics.

---

## Region and Diagnostic Switch

- `AppData.IsDiagnosticDataEnabled` default: `true` for non-privacy-sensitive regions; `false` for privacy-sensitive regions (see `PrivacyConsentHelpers.IsPrivacySensitiveRegion()`).
- On app start, its value is assigned to `Telemetry.IsDiagnosticTelemetryOn`:
  - Non-error `Info`/`Measure` events downgrade to `Local` when `false`.
  - `Critical` and error events are not downgraded.

---

## FAQ

- Q: The PerfView provider list does not show `Microsoft.Windows.AIDevGallery`.
  - A: This is expected. EventSource-based custom providers often don’t appear in the dropdown; manually type `Microsoft.Windows.AIDevGallery:0xFFFFFFFFFFFFFFFF:Verbose` in Additional Providers.

- Q: I see other providers’ events in the etl.
  - A: Additional Providers is an “add” subscription; PerfView also captures Kernel/CLR by default. Disable defaults (Kernel=None) before collection, or filter by Provider while viewing.

---

## Change and Maintenance
- Adding a new event: create a type under `Telemetry/Events/` with `[EventData]`; public properties become event fields. Provide `public static void Log(...)` and use `TelemetryFactory.Get<ITelemetry>()` to write.
- Changing event fields can impact downstream processing/queries; avoid when possible. If required, update release notes and this document accordingly.


