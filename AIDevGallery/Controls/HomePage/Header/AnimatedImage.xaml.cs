// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace AIDevGallery.Controls;
internal partial class AnimatedImage : UserControl
{
    private AnimationSet? selectAnimation;

    public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register(
        nameof(ImageUrl),
        typeof(Uri),
        typeof(AnimatedImage),
        new PropertyMetadata(defaultValue: null, (d, e) => ((AnimatedImage)d).IsImageChanged((Uri)e.OldValue, (Uri)e.NewValue)));

    public Uri ImageUrl
    {
        get => (Uri)GetValue(ImageUrlProperty);
        set => SetValue(ImageUrlProperty, value);
    }

    public AnimatedImage()
    {
        this.InitializeComponent();
        this.Unloaded += AnimatedImage_Unloaded;
    }

    private void AnimatedImage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (selectAnimation != null)
        {
            selectAnimation.Completed -= SelectAnimation_Completed;
            selectAnimation = null;
        }
    }

    protected virtual void IsImageChanged(Uri oldValue, Uri newValue)
    {
        OnIsImageChanged();
    }

    private void OnIsImageChanged()
    {
        BottomImage.Source = new BitmapImage(this.ImageUrl);
        BottomImage.Opacity = 1;

        if (selectAnimation != null)
        {
            selectAnimation.Completed -= SelectAnimation_Completed;
        }

        selectAnimation = [new OpacityAnimation() { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(800) }];
        selectAnimation.Completed += SelectAnimation_Completed;
        selectAnimation.Start(TopImage);
    }

    private void SelectAnimation_Completed(object? sender, EventArgs e)
    {
        try
        {
            TopImage.Source = new BitmapImage(this.ImageUrl);
            TopImage.Opacity = 1;
        }
        catch
        {
        }
    }
}