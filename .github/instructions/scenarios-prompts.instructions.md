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
- `Description`: Brief description of the scenario
- `Instructions`: User instructions for the sample UI
- `Id`: Unique kebab-case identifier (e.g., `"summarize-text"`)

## Optional Fields

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
    "System": "<|system|>\n{{system}}<|end|>\n",
    "User": "<|user|>\n{{user}}<|end|>\n<|assistant|>\n",
    "Stop": ["<|end|>", "<|user|>"]
  }
}
```

## Required Prompt Template Fields

- `System`: Template for system prompt with `{{system}}` placeholder
- `User`: Template for user prompt with `{{user}}` placeholder  
- `Stop`: Array of stop sequences

## Code Review Checks

### For scenarios.json
- `Id` is unique across all scenarios
- `Id` uses kebab-case (lowercase with hyphens)
- No duplicate scenario names within a category
- Icon codes are valid Segoe MDL2 glyphs
- Instructions are clear and user-friendly
- Category grouping is logical

### For promptTemplates.json
- Template name matches what models reference
- Contains `{{system}}` and `{{user}}` placeholders
- Stop sequences are correct for the model family
- Special tokens match model's training format

## Common Issues

- **Changes not appearing**: Forgot to rebuild after editing
- **ScenarioType not found**: Scenario name doesn't match expected format
- **Prompt format mismatch**: Wrong template causes garbled output
- **Missing stop sequences**: Model generates excessive tokens

## Icon Reference

Common Segoe MDL2 icons:
- `\uE8BD` - Document (text)
- `\uE8D4` - Edit
- `\uEB9F` - Lightbulb (generate)
- `\uE8F2` - Chat
- `\uF2B7` - Translate
- `\uE9D5` - Sentiment
- `\uE8B9` - Image
