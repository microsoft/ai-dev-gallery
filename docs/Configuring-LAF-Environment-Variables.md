### Configuring LAF_TOKEN and LAF_PUBLISHER_ID environment variables

This repository reads Limited Access Feature (LAF) values from environment variables at runtime to avoid hardcoding sensitive data.

- Required environment variables:
  - `LAF_TOKEN`: The unlock token for `com.microsoft.windows.ai.languagemodel`.
  - `LAF_PUBLISHER_ID`: The publisher/identifier used to construct the usage description.

#### Background: What are Limited Access Features (LAF)?

Limited Access Features are Windows platform features that require explicit permission from Microsoft. Apps cannot use these features without a feature ID and a corresponding token. Access requests are made via the `Windows.ApplicationModel.LimitedAccessFeatures` API (see the official documentation).

- Docs: `https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.limitedaccessfeatures?view=winrt-26100`
- Method: `LimitedAccessFeatures.TryUnlockFeature(string featureId, string token, string usage)`

#### Set environment variables with PowerShell

- Session only (takes effect immediately in the current shell):

```powershell
$env:LAF_TOKEN = 'your-token-value'
$env:LAF_PUBLISHER_ID = 'your-publisher-id'
```

- Persist for current user (effective in new terminals/after restarting IDE):

```powershell
setx LAF_TOKEN "your-token-value"
setx LAF_PUBLISHER_ID "your-publisher-id"
```

- Persist for all users (requires elevated/admin shell):

```powershell
setx LAF_TOKEN "your-token-value" /M
setx LAF_PUBLISHER_ID "your-publisher-id" /M
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

#### Notes and best practices

- After setting User/Machine variables, restart Visual Studio/VS Code or open a new terminal so the app process inherits the updated environment.
- Never commit tokens to source control. If a token was ever exposed, rotate/revoke it via your issuance channel and set a new `LAF_TOKEN`.
- Ensure your OS meets the API requirements (Windows 10, version 1809 or newer). See the official docs for details.


