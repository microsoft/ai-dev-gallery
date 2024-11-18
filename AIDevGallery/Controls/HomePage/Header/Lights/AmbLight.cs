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
        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

        // Create AmbientLight and set its properties
        AmbientLight ambientLight = compositor.CreateAmbientLight();
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