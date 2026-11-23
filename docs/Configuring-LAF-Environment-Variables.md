# LanguageModel access in AI Dev Gallery

LanguageModel samples no longer use LAF tokens or publisher identifiers. Local development,
packaged builds, CI, and exported sample projects do not require `LAF_TOKEN` or
`LAF_PUBLISHER_ID`. The `LafToken` and `LafPublisherId` build properties are no longer injected
into assembly metadata.

## Model readiness

Use `LanguageModel.GetReadyState()` to check availability. When the model is not ready, call
`LanguageModel.EnsureReadyAsync()` and check its result before creating the model. Continue to
handle unsupported hardware, user-disabled APIs, model downloads, and capability or package
identity failures. Removing token setup does not bypass these requirements.

For an independent app that still targets older SDK or Windows versions, verify its access
requirements before removing an existing unlock flow. Do not assume every older combination
supports token-free access.

## App Content Search is separate

The App Content Search samples in this repository target the experimental Windows App SDK
channel and do not require a token. Production App Content Search apps use Windows App SDK
2.5.1 or later and must request a token for `com.microsoft.windows.ai.appcontentindexer` and call
`LimitedAccessFeatures.TryUnlockFeature` before using the API. See
[Get started with App Content Search](https://learn.microsoft.com/windows/ai/apis/app-content-search-tutorial).

Never commit tokens for APIs that still require them. Retiring LanguageModel token inputs in
this repository does not authorize deleting shared secrets or configuration used by other apps.