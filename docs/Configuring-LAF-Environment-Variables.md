### Configuring LAF_TOKEN and LAF_PUBLISHER_ID

> Important
>
> This document is intended for contributors to AI Dev Gallery. It explains how to configure LAF values (via MSBuild properties/AssemblyMetadata or environment variables) for this repository’s own development/build/CI workflows.
>
> If you need a Limited Access Features (LAF) token for your own application, please visit the official Microsoft entry point to learn more and request access: [aka.ms/laffeatures](https://aka.ms/laffeatures). The example values here (e.g., "E1t/*********") are for this repository’s development only and are not valid for your app.

This repository now reads Limited Access Feature (LAF) values from two sources, in this order:

1. Build-time AssemblyMetadata injected via MSBuild properties — preferred for packaged/CI builds
2. Environment variables at runtime — fallback when no build-time value is provided

This allows injecting secrets during build without committing them to source control, while still supporting local development via environment variables.

Note: Build-time AssemblyMetadata takes precedence over runtime environment variables.
For local development, we recommend configuring environment variables; for CI/packaged builds, prefer passing MSBuild properties to inject AssemblyMetadata.

- Required values (provided via MSBuild properties/AssemblyMetadata or environment variables):
  - `LAF_TOKEN`: The unlock token for `com.microsoft.windows.ai.languagemodel`.
  - `LAF_PUBLISHER_ID`: The publisher/identifier used to construct the usage description.

#### Set environment variables for local development (recommended)

- Session only (takes effect immediately in the current shell):

```powershell
$env:LAF_TOKEN = 'E1t/*********'
$env:LAF_PUBLISHER_ID = '1v8m*******'
```

- Persist for current user (effective in new terminals/after restarting IDE):

```powershell
setx LAF_TOKEN "E1t/*********"
setx LAF_PUBLISHER_ID "1v8m*******"
```

- Persist for all users (requires elevated/admin shell):

```powershell
setx LAF_TOKEN "E1t/*********" /M
setx LAF_PUBLISHER_ID "1v8m*******" /M
```

#### Configure via MSBuild properties and AssemblyMetadata (CI/packaged builds)

The project accepts MSBuild properties named `LafToken` and `LafPublisherId`, which are mapped into `AssemblyMetadata` with keys `LAF_TOKEN` and `LAF_PUBLISHER_ID` respectively. The app reads these values at runtime via `AssemblyMetadata`.

- Pass values from the command line:

```powershell
dotnet build -p:LafToken="E1t/*********" -p:LafPublisherId="1v8m*******"
```

- Or set environment variables before building, then pass them to MSBuild properties explicitly:

```powershell
$env:LAF_TOKEN = 'E1t/*********'
$env:LAF_PUBLISHER_ID = '1v8m*******'
# Pass env vars to MSBuild properties explicitly
dotnet build -p:LafToken="$env:LAF_TOKEN" -p:LafPublisherId="$env:LAF_PUBLISHER_ID"
```

In Azure Pipelines, we pass the pipeline variables to MSBuild properties during build, for example:

```powershell
dotnet build AIDevGallery `
  /p:LafToken=$(LAF_TOKEN) `
  /p:LafPublisherId=$(LAF_PUBLISHER_ID)
```

#### Verify

```powershell
# In the current session:
$env:LAF_TOKEN
$env:LAF_PUBLISHER_ID

# As stored for User/Machine (run in a new process):
[Environment]::GetEnvironmentVariable('LAF_TOKEN', 'User')
[Environment]::GetEnvironmentVariable('LAF_PUBLISHER_ID', 'User')
[Environment]::GetEnvironmentVariable('LAF_TOKEN', 'Machine')
[Environment]::GetEnvironmentVariable('LAF_PUBLISHER_ID', 'Machine')
```

#### Background: What are Limited Access Features (LAF)?

Limited Access Features are Windows platform features that require explicit permission from Microsoft. Apps cannot use these features without a feature ID and a corresponding token. Access requests are made via the `Windows.ApplicationModel.LimitedAccessFeatures` API (see the official documentation).

- Docs: [LimitedAccessFeatures (Windows docs)](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.limitedaccessfeatures?view=winrt-26100)
- Method: `LimitedAccessFeatures.TryUnlockFeature(string featureId, string token, string usage)`

#### Notes and best practices

- After setting User/Machine variables, restart Visual Studio/VS Code or open a new terminal so the app process inherits the updated environment.
- Ensure your OS meets the API requirements (Windows 10, version 1809 or newer). See the official docs for details.