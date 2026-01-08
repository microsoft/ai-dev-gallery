---
applyTo: "AIDevGallery/Samples/Definitions/**/*.json"
---

# Model and API Definition Instructions

When reviewing or modifying definition files in `AIDevGallery/Samples/Definitions/`:

## `.modelgroup.json` - Model Group Definition

Used for organizing model families with display ordering. Each file contains one or more model groups:

```json
{
  "GroupName": {
    "Id": "guid",
    "Name": "Display Name",
    "Icon": "\uE8BD",
    "Order": 1,
    "Models": {
      "FamilyName": {
        "Id": "slug",
        "Name": "Family Display Name",
        "Description": "...",
        "DocsUrl": "https://...",
        "ReadmeUrl": "https://...",
        "Models": {
          "VariationName": {
            "Id": "guid",
            "Name": "Model Name GPU",
            "Url": "https://huggingface.co/org/model/tree/main/variant",
            "HardwareAccelerator": "GPU",
            "Size": 1234567890,
            "License": "mit",
            "PromptTemplate": "Phi3"
          }
        }
      }
    }
  }
}
```

### Key Fields
- **Individual Model** (required): `Id` (GUID), `Url`, `HardwareAccelerator`, `License`
- **Language Models**: have `PromptTemplate` referencing `promptTemplates.json`
- **HardwareAccelerator values**: `"CPU"`, `"GPU"`, `"NPU"`, or array

## `apis.json` - Windows AI APIs

Located at `Definitions/WcrApis/apis.json`:

```json
{
  "WCRAPIs": {
    "Id": "guid",
    "Name": "Windows AI APIs",
    "Icon": "\uF4A5",
    "Apis": {
      "ApiName": {
        "Id": "guid",
        "Name": "API Display Name",
        "Description": "...",
        "Icon": "icon.svg",
        "IconGlyph": "\uE8F2",
        "ReadmeUrl": "https://...",
        "License": "ms-pl",
        "Category": "Category Name"
      }
    }
  }
}
```

## Code Review Checks

- JSON structure matches expected schema
- All GUIDs are unique across all definition files
- `HardwareAccelerator` matches model requirements
- Language models have valid `PromptTemplate`
- JSON is valid (no trailing commas, proper escaping)
- Icons reference existing files in `Assets/ModelIcons/`
