// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AIDevGallery.Controls;

internal partial class HeaderTile : Button
{
    public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register(
    nameof(ImageUrl),
    typeof(string),
    typeof(HeaderTile),
    new PropertyMetadata(null));

    public string ImageUrl
    {
        get => (string)GetValue(ImageUrlProperty);
        set => SetValue(ImageUrlProperty, value);
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(HeaderTile), new PropertyMetadata(defaultValue: null, (d, e) => ((HeaderTile)d).IsHeaderChanged((string)e.OldValue, (string)e.NewValue)));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(HeaderTile), new PropertyMetadata(defaultValue: null));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty SampleIDProperty = DependencyProperty.Register(nameof(SampleID), typeof(string), typeof(HeaderTile), new PropertyMetadata(defaultValue: string.Empty));

    public string SampleID
    {
        get => (string)GetValue(SampleIDProperty);
        set => SetValue(SampleIDProperty, value);
    }

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(HeaderTile), new PropertyMetadata(defaultValue: false, (d, e) => ((HeaderTile)d).IsSelectedChanged((bool)e.OldValue, (bool)e.NewValue)));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public HeaderTile()
    {
        this.DefaultStyleKey = typeof(HeaderTile);
        SetAccesibileName();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
    }

    protected virtual void IsSelectedChanged(object oldValue, object newValue)
    {
        OnIsSelectedChanged();
    }

    private void OnIsSelectedChanged()
    {
        if (IsSelected)
        {
            Canvas.SetZIndex(this, 10);
            VisualStateManager.GoToState(this, "Selected", true);
            AnimationSet selectAnimation = [new ScaleAnimation() { To = "1.0", Duration = TimeSpan.FromMilliseconds(600) }, new OpacityDropShadowAnimation() { To = 0.4 }, new BlurRadiusDropShadowAnimation() { To = 24 }];
            selectAnimation.Start(this);
        }
        else
        {
            VisualStateManager.GoToState(this, "NotSelected", true);
            AnimationSet deselectAnimation = [new ScaleAnimation() { To = "0.8", Duration = TimeSpan.FromMilliseconds(350) }, new OpacityDropShadowAnimation() { To = 0.2 }, new BlurRadiusDropShadowAnimation() { To = 12 }];
            deselectAnimation.Completed += (s, e) =>
            {
                Canvas.SetZIndex(this, 0);
            };
            deselectAnimation.Start(this);
        }
    }

    protected virtual void IsHeaderChanged(string oldValue, string newValue)
    {
        SetAccesibileName();
    }

    private void SetAccesibileName()
    {
        if (!string.IsNullOrEmpty(Header))
        {
            AutomationProperties.SetName(this, Header);
        }
    }
}