---
applyTo: "AIDevGallery.SourceGenerator/**/*.cs"
---

# Source Generator Instructions

When reviewing or modifying code in `AIDevGallery.SourceGenerator/`:

## Overview

This project contains Roslyn incremental source generators that:
1. Generate `ScenarioType` enum from `scenarios.json`
2. Generate `SharedCodeEnum` from SharedCode files
3. Generate model type constants from `.model.json`/`.modelgroup.json`
4. Generate prompt template data from `promptTemplates.json`
5. Generate package dependency version constants

## Key Source Generators

| Generator | Purpose | Output |
|-----------|---------|--------|
| `ScenariosSourceGenerator` | Parses `scenarios.json` | `ScenarioType` enum |
| `SamplesSourceGenerator` | Scans sample classes | `SharedCodeEnum`, sample metadata |
| `ModelsSourceGenerator` | Parses model definitions | `ModelType` enum, model constants |
| `PromptTemplatesSourceGenerator` | Parses `promptTemplates.json` | Prompt template data |
| `DependencyVersionsSourceGenerator` | Reads package versions | Version constants |

## Development Notes

### Debugging Source Generators
- Use `Debugger.Launch()` for debugging (remove before commit)
- Check `obj/` folder for generated files
- Rebuild solution to see generator output

### Common Patterns
```csharp
[Generator(LanguageNames.CSharp)]
internal class MyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register source output
        context.RegisterSourceOutput(provider, Execute);
    }

    private void Execute(SourceProductionContext context, DataType data)
    {
        // Generate source code
        var source = GenerateSource(data);
        context.AddSource("MyGenerated.g.cs", source);
    }
}
```

## Code Review Checks

- No `Debugger.Launch()` or debug code committed
- Generated code compiles correctly
- Error diagnostics reported via `context.ReportDiagnostic()`
- Incremental generation properly configured (performance)
- File paths handled cross-platform (use `Path.Combine`)
- No blocking I/O in hot paths
- Proper null checks for Roslyn symbols

## File Structure

```
AIDevGallery.SourceGenerator/
├── ScenariosSourceGenerator.cs     # scenarios.json → ScenarioType
├── SamplesSourceGenerator.cs       # Sample classes → SharedCodeEnum
├── ModelsSourceGenerator.cs        # Model JSON → ModelType
├── PromptTemplatesSourceGenerator.cs
├── DependencyVersionsSourceGenerator.cs
├── Helpers.cs                      # Shared utilities
├── WellKnownTypeNames.cs           # Type name constants
├── WellKnownTypeSymbols.cs         # Roslyn symbol helpers
├── Diagnostics/                    # Diagnostic descriptors
├── Extensions/                     # Extension methods
└── Models/                         # Data models for generation
```

## Important Constraints

### Banned APIs
Some APIs are banned in source generators for performance:
- `#pragma warning disable RS1035` may be needed for file I/O

### Performance
- Use incremental generation (`IIncrementalGenerator`)
- Avoid regenerating on every keystroke
- Cache computed values appropriately

### Testing
- Test generated code compiles
- Test with edge cases (empty files, malformed JSON)
- Verify diagnostic messages are helpful
