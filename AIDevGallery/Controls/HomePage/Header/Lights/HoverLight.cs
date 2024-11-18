// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace AIDevGallery.Controls;

internal partial class HoverLight : XamlLight
{
    private ExpressionAnimation? _lightPositionExpression;
    private Vector3KeyFrameAnimation? _offsetAnimation;
    private static readonly string Id = typeof(HoverLight).FullName!;

    protected override void OnConnected(UIElement targetElement)
    {
        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

        // Create SpotLight and set its properties
        SpotLight spotLight = compositor.CreateSpotLight();
        spotLight.InnerConeAngleInDegrees = 50f;
        spotLight.InnerConeColor = Colors.FloralWhite;
        spotLight.OuterConeAngleInDegrees = 20f;
        spotLight.ConstantAttenuation = 1f;
        spotLight.LinearAttenuation = 0.253f;
        spotLight.QuadraticAttenuation = 0.58f;

        // Associate CompositionLight with XamlLight
        CompositionLight = spotLight;

        // Define resting position Animation
        Vector3 restingPosition = new(200, 200, 400);
        CubicBezierEasingFunction cbEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.3f, 0.7f), new Vector2(0.9f, 0.5f));
        _offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        _offsetAnimation.InsertKeyFrame(1, restingPosition, cbEasing);
        _offsetAnimation.Duration = TimeSpan.FromSeconds(0.5f);

        spotLight.Offset = restingPosition;

        // Define expression animation that relates light's offset to pointer position
        CompositionPropertySet hoverPosition = ElementCompositionPreview.GetPointerPositionPropertySet(targetElement);
        _lightPositionExpression = compositor.CreateExpressionAnimation("Vector3(hover.Position.X, hover.Position.Y, height)");
        _lightPositionExpression.SetReferenceParameter("hover", hoverPosition);
        _lightPositionExpression.SetScalarParameter("height", 100.0f);

        // Configure pointer entered/ exited events
        targetElement.PointerMoved += TargetElement_PointerMoved;
        targetElement.PointerExited += TargetElement_PointerExited;

        // Add UIElement to the Light's Targets
        AddTargetElement(GetId(), targetElement);
    }

    private void MoveToRestingPosition()
    {
        // Start animation on SpotLight's Offset
        CompositionLight?.StartAnimation("Offset", _offsetAnimation);
    }

    private void TargetElement_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (CompositionLight != null)
        {
            // touch input is still UI thread-bound as of the Creator's Update
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
            {
                Vector2 offset = e.GetCurrentPoint((UIElement)sender).Position.ToVector2();

                if (CompositionLight is SpotLight light)
                {
                    light.Offset = new Vector3(offset.X, offset.Y, 15);
                }
            }
            else
            {
                // Get the pointer's current position from the property and bind the SpotLight's X-Y Offset
                CompositionLight.StartAnimation("Offset", _lightPositionExpression);
            }
        }
    }

    private void TargetElement_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Move to resting state when pointer leaves targeted UIElement
        MoveToRestingPosition();
    }

    protected override void OnDisconnected(UIElement oldElement)
    {
        // Dispose Light and Composition resources when it is removed from the tree
        RemoveTargetElement(GetId(), oldElement);
        CompositionLight.Dispose();

        _lightPositionExpression?.Dispose();
        _offsetAnimation?.Dispose();
    }

    protected override string GetId()
    {
        return Id;
    }
}