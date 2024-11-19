// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;
using Windows.UI;
using WinUICommunity;

namespace AIDevGallery.Controls;

internal sealed partial class OpacityMaskView : RedirectVisualView
{
    public OpacityMaskView()
    {
        opacityMaskHost = new Rectangle();

        compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        opacityMaskVisualSurface = compositor.CreateVisualSurface();
        opacityMaskVisualBrush = compositor.CreateSurfaceBrush(opacityMaskVisualSurface);
        opacityMaskVisualBrush.HorizontalAlignmentRatio = 0;
        opacityMaskVisualBrush.VerticalAlignmentRatio = 0;
        opacityMaskVisualBrush.Stretch = CompositionStretch.None;

        maskBrush = compositor.CreateMaskBrush();
        maskBrush.Mask = opacityMaskVisualBrush;
        maskBrush.Source = ChildVisualBrush;

        RootVisual.Brush = maskBrush;
    }

    private readonly Rectangle opacityMaskHost;

    private readonly Compositor compositor;

    private readonly CompositionVisualSurface opacityMaskVisualSurface;
    private readonly CompositionSurfaceBrush opacityMaskVisualBrush;
    private readonly CompositionMaskBrush maskBrush;

    public Brush? OpacityMask
    {
        get => (Brush?)GetValue(OpacityMaskProperty);
        set => SetValue(OpacityMaskProperty, value);
    }

    public static readonly DependencyProperty OpacityMaskProperty =
        DependencyProperty.Register("OpacityMask", typeof(Brush), typeof(OpacityMaskView), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), OnOpacityMaskPropertyChanged));

    private static void OnOpacityMaskPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OpacityMaskView sender && !Equals(e.NewValue, e.OldValue))
        {
            if (sender.RedirectVisualAttached)
            {
                sender.opacityMaskHost.Fill = e.NewValue as Brush;
            }
            else
            {
                sender.opacityMaskHost.Fill = null;
            }
        }
    }

    protected override void OnDetachVisuals()
    {
        base.OnDetachVisuals();

        if (LayoutRoot != null)
        {
            if (opacityMaskHost != null)
            {
                opacityMaskHost.Fill = null;
            }

            opacityMaskVisualSurface.SourceVisual = null;

            if (OpacityMaskContainer != null)
            {
                OpacityMaskContainer.Visibility = Visibility.Collapsed;
                OpacityMaskContainer.Children.Remove(opacityMaskHost);
            }
        }
    }

    protected override void OnAttachVisuals()
    {
        base.OnAttachVisuals();

        if (LayoutRoot != null)
        {
            if (opacityMaskHost != null)
            {
                opacityMaskHost.Fill = OpacityMask;
                opacityMaskVisualSurface.SourceVisual = ElementCompositionPreview.GetElementVisual(opacityMaskHost);
            }

            if (OpacityMaskContainer != null)
            {
                OpacityMaskContainer.Visibility = Visibility.Visible;
                ElementCompositionPreview.GetElementVisual(OpacityMaskContainer).IsVisible = false;
                OpacityMaskContainer.Children.Add(opacityMaskHost);
            }
        }
    }

    protected override void OnUpdateSize()
    {
        base.OnUpdateSize();

        if (RedirectVisualAttached && LayoutRoot != null)
        {
            if (opacityMaskHost != null)
            {
                opacityMaskHost.Width = LayoutRoot.ActualWidth;
                opacityMaskHost.Height = LayoutRoot.ActualHeight;
            }

            opacityMaskVisualSurface.SourceSize = new Vector2((float)LayoutRoot.ActualWidth, (float)LayoutRoot.ActualHeight);
        }
    }
}