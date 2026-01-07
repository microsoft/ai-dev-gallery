---
applyTo: "AIDevGallery/Samples/**/scenarios.json,AIDevGallery/Samples/**/promptTemplates.json"
---

# Scenario and Prompt Template Instructions

When reviewing or modifying `scenarios.json` or `promptTemplates.json`:

## IMPORTANT: Source Generator Embedded Files

Both `scenarios.json` and `promptTemplates.json` are embedded at build time by Roslyn source generators. Changes to these files require a full rebuild to take effect - they are NOT loaded dynamically at runtime.

## scenarios.json Structure

```json
{
  "CategoryName": {
    "Name": "Category Display Name",
    "Icon": "\uE8BD",
    "Description": "Category description for the UI",
    "Scenarios": {
      "ScenarioName": {
        "Name": "Scenario Display Name",
        "Description": "What this scenario does",
        "Instructions": "User-facing instructions for using this sample",
        "Id": "kebab-case-unique-id",
        "Icon": "\uE8D4"
      }
    }
  }
}
```

## Required Scenario Fields

- `Name`: Display name shown in UI
- `Id`: Unique kebab-case identifier (e.g., `"summarize-text"`)

## Optional Fields

- `Description`: Brief description of the scenario
- `Instructions`: User instructions for the sample UI
- `Icon`: Segoe MDL2 glyph code (e.g., `"\uE8BD"`)

## Usage in Samples

The `ScenarioType` enum is auto-generated from `scenarios.json`. Reference in `[GallerySample]`:

```csharp
[GallerySample(
    Scenario = ScenarioType.TextSummarizeText,  // CategoryScenarioName format
    ...
)]
```

The naming pattern is `{Category}{ScenarioName}` in PascalCase.

## promptTemplates.json Structure

```json
{
  "TemplateName": {
    "system": "<|system|>\n{{CONTENT}}<|end|>\n",
    "user": "<|user|>\n{{CONTENT}}<|end|>\n",
    "assistant": "<|assistant|>\n{{CONTENT}}<|end|>\n",
    "stop": ["<|end|>", "<|user|>"]
  }
}
```

## Required Prompt Template Fields

- `user`: Template for user messages with `{{CONTENT}}` placeholder
- `stop`: Array of stop sequences

## Optional Prompt Template Fields

- `system`: Template for system prompt with `{{CONTENT}}` placeholder
- `assistant`: Template for assistant responses with `{{CONTENT}}` placeholder

## Code Review Checks

### For scenarios.json
- `Id` is unique across all scenarios
- `Id` uses kebab-case (lowercase with hyphens)
- No duplicate scenario names within a category
- Icon codes are valid glyphs
- Instructions are clear and user-friendly
- Category grouping is logical

### For promptTemplates.json
- Template name matches what models reference in `.modelgroup.json`
- Contains `{{CONTENT}}` placeholder in templates
- `user` and `stop` fields are required
- Stop sequences are correct for the model family
- Special tokens match model's training format

## Common Issues

- **Changes not appearing**: Forgot to rebuild after editing
- **ScenarioType not found**: Scenario name doesn't match expected format
- **Prompt format mismatch**: Wrong template causes garbled output
