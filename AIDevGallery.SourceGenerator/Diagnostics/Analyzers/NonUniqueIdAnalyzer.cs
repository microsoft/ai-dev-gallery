// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace AIDevGallery.SourceGenerator.Diagnostics.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal class NonUniqueIdAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DiagnosticDescriptors.NonUniqueId];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static context =>
        {
            // Get the [GallerySample] attribute type symbol
            if (context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.GallerySampleAttribute) is not INamedTypeSymbol gallerySampleAttributeSymbol)
            {
                return;
            }

            var locations = new ConcurrentDictionary<string, Location>();

            context.RegisterSymbolAction(
                context =>
                {
                    if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsImplicitlyDeclared: false })
                    {
                        return;
                    }

                    if (context.Symbol.TryGetAttributeWithType(gallerySampleAttributeSymbol, out AttributeData? attribute) &&
                        attribute != null &&
                        attribute.NamedArguments.FirstOrDefault(a => a.Key == "Id").Value.Value is string id &&
                        !string.IsNullOrEmpty(id) &&
                        attribute.GetLocation() is Location location)
                    {
                        // Check if the id is unique
                        if (locations.TryAdd(id, location))
                        {
                            // ID is unique so far, do nothing
                            return;
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.NonUniqueId,
                                location,
                                [locations[id]],
                                id));
                        }
                    }
                },
                SymbolKind.NamedType);
        });
    }
}