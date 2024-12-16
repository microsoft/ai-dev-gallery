// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace AIDevGallery.SourceGenerator;

/// <summary>
/// A simple helper providing quick access to known type symbols.
/// </summary>
internal sealed class WellKnownTypeSymbols
{
    /// <summary>
    /// The input <see cref="Compilation"/> instance.
    /// </summary>
    private readonly Compilation compilation;

    private INamedTypeSymbol? gallerySampleAttribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="WellKnownTypeSymbols"/> class.
    /// </summary>
    /// <param name="compilation">The input <see cref="Compilation"/> instance.</param>
    public WellKnownTypeSymbols(Compilation compilation)
    {
        this.compilation = compilation;
    }

    /// <summary>
    /// Gets the <see cref="INamedTypeSymbol"/> for <c>AIDevGallery.Samples.Attributes.GallerySampleAttribute</c>.
    /// </summary>
    public INamedTypeSymbol GallerySampleAttribute => Get(ref this.gallerySampleAttribute, WellKnownTypeNames.GallerySampleAttribute);

    /// <summary>
    /// Gets an <see cref="INamedTypeSymbol"/> instance with a specified fully qualified metadata name.
    /// </summary>
    /// <param name="storage">The backing storage to save the result.</param>
    /// <param name="fullyQualifiedMetadataName">The fully qualified metadata name of the <see cref="INamedTypeSymbol"/> instance to get.</param>
    /// <returns>The resulting <see cref="INamedTypeSymbol"/> instance.</returns>
    private INamedTypeSymbol Get(ref INamedTypeSymbol? storage, string fullyQualifiedMetadataName)
    {
        return storage ??= this.compilation.GetTypeByMetadataName(fullyQualifiedMetadataName)!;
    }
}