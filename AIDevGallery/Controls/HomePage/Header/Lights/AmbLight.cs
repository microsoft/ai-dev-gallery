// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AIDevGallery.Controls;

internal partial class AmbLight : XamlLight
{
    private static readonly string Id = typeof(AmbLight).FullName!;

    protected override void OnConnected(UIElement newElement)
    {
        // Compositor is a shared system resource and should not be disposed
#pragma warning disable IDISP001 // Dispose created
        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();
#pragma warning restore IDISP001

        // Dispose previous CompositionLight if exists
        CompositionLight?.Dispose();

        // Create AmbientLight and set its properties
        // Ownership of ambientLight is transferred to CompositionLight property
#pragma warning disable IDISP001 // Dispose created
        AmbientLight ambientLight = compositor.CreateAmbientLight();
#pragma warning restore IDISP001
        ambientLight.Color = Colors.White;

        // Associate CompositionLight with XamlLight
        CompositionLight = ambientLight;

        // Add UIElement to the Light's Targets
        AddTargetElement(GetId(), newElement);
    }

    protected override void OnDisconnected(UIElement oldElement)
    {
        // Dispose Light when it is removed from the tree
        RemoveTargetElement(GetId(), oldElement);
        CompositionLight.Dispose();
    }

    protected override string GetId()
    {
        return Id;
    }
}