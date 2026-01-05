// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace AIDevGallery.Controls;

/// <summary>
/// A control that applies an opacity mask to its content.
/// </summary>
[TemplatePart(Name = RootGridTemplateName, Type = typeof(Grid))]
[TemplatePart(Name = MaskContainerTemplateName, Type = typeof(Border))]
[TemplatePart(Name = ContentPresenterTemplateName, Type = typeof(ContentPresenter))]
public sealed partial class OpacityMaskView : ContentControl, IDisposable
{
    // This is from Windows Community Toolkit Labs: https://github.com/CommunityToolkit/Labs-Windows/pull/491

    /// <summary>
    /// Identifies the <see cref="OpacityMask"/> property.
    /// </summary>
    public static readonly DependencyProperty OpacityMaskProperty =
        DependencyProperty.Register(nameof(OpacityMask), typeof(UIElement), typeof(OpacityMaskView), new PropertyMetadata(null, OnOpacityMaskChanged));

    private const string ContentPresenterTemplateName = "PART_ContentPresenter";
    private const string MaskContainerTemplateName = "PART_MaskContainer";
    private const string RootGridTemplateName = "PART_RootGrid";

#pragma warning disable IDISP002 // Compositor is provided by the system and should not be disposed
    private readonly Compositor _compositor = CompositionTarget.GetCompositorForCurrentThread();
#pragma warning restore IDISP002
    private CompositionBrush? _mask;
    private CompositionMaskBrush? _maskBrush;
    private SpriteVisual? _redirectVisual;
    private CompositionVisualSurface? _contentVisualSurface;
    private ExpressionAnimation? _contentSizeAnimation;
    private CompositionVisualSurface? _maskVisualSurface;
    private ExpressionAnimation? _maskSizeAnimation;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpacityMaskView"/> class.
    /// Creates a new instance of the <see cref="OpacityMaskView"/> class.
    /// </summary>
    public OpacityMaskView()
    {
        DefaultStyleKey = typeof(OpacityMaskView);
    }

    /// <summary>
    /// Gets or sets a <see cref="UIElement"/> as the opacity mask that is applied to alpha-channel masking for the rendered content of the content.
    /// </summary>
    public UIElement? OpacityMask
    {
        get => (UIElement?)GetValue(OpacityMaskProperty);
        set => SetValue(OpacityMaskProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Grid rootGrid = (Grid)GetTemplateChild(RootGridTemplateName);
        ContentPresenter contentPresenter = (ContentPresenter)GetTemplateChild(ContentPresenterTemplateName);
        Border maskContainer = (Border)GetTemplateChild(MaskContainerTemplateName);

        _maskBrush?.Dispose();
        _maskBrush = _compositor.CreateMaskBrush();
        _contentVisualSurface?.Dispose();
        _contentSizeAnimation?.Dispose();
        _maskBrush.Source = GetVisualBrush(contentPresenter, ref _contentVisualSurface, ref _contentSizeAnimation);
        _mask?.Dispose();
        _maskVisualSurface?.Dispose();
        _maskSizeAnimation?.Dispose();
        _mask = GetVisualBrush(maskContainer, ref _maskVisualSurface, ref _maskSizeAnimation);
        _maskBrush.Mask = OpacityMask is null ? null : _mask;

        _redirectVisual?.Dispose();
        _redirectVisual = _compositor.CreateSpriteVisual();
        _redirectVisual.RelativeSizeAdjustment = Vector2.One;
        _redirectVisual.Brush = _maskBrush;
        ElementCompositionPreview.SetElementChildVisual(rootGrid, _redirectVisual);
    }

    private static CompositionBrush GetVisualBrush(UIElement element, ref CompositionVisualSurface? visualSurface, ref ExpressionAnimation? sizeAnimation)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);

        Compositor compositor = visual.Compositor;

        // Create visual surface and animation
        visualSurface = compositor.CreateVisualSurface();
        visualSurface.SourceVisual = visual;
        sizeAnimation = compositor.CreateExpressionAnimation($"{nameof(visual)}.Size");
        sizeAnimation.SetReferenceParameter(nameof(visual), visual);
        visualSurface.StartAnimation(nameof(visualSurface.SourceSize), sizeAnimation);

        CompositionSurfaceBrush brush = compositor.CreateSurfaceBrush(visualSurface);

        visual.Opacity = 0;

        return brush;
    }

    private static void OnOpacityMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        OpacityMaskView self = (OpacityMaskView)d;
        if (self._maskBrush is not { } maskBrush)
        {
            return;
        }

        UIElement? opacityMask = (UIElement?)e.NewValue;
        maskBrush.Mask = opacityMask is null ? null : self._mask;
    }

    /// <summary>
    /// Disposes the composition resources.
    /// </summary>
    public void Dispose()
    {
        _contentVisualSurface?.Dispose();
        _contentSizeAnimation?.Dispose();
        _maskVisualSurface?.Dispose();
        _maskSizeAnimation?.Dispose();
        _mask?.Dispose();
        _maskBrush?.Dispose();
        _redirectVisual?.Dispose();
    }
}