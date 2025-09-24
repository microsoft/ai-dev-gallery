// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AIDevGallery.Controls;

internal sealed partial class WcrModelUnavailable : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(WcrModelUnavailable), new PropertyMetadata("Copilot+ PC required"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(WcrModelUnavailable), new PropertyMetadata("This Windows AI API requires a Copilot+ PC and Windows 11 Insider Preview Build 26120.3073."));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty LearnMoreTextProperty = DependencyProperty.Register(
        nameof(LearnMoreText), typeof(string), typeof(WcrModelUnavailable), new PropertyMetadata("Learn more"));

    public string LearnMoreText
    {
        get => (string)GetValue(LearnMoreTextProperty);
        set => SetValue(LearnMoreTextProperty, value);
    }

    public static readonly DependencyProperty LearnMoreUriProperty = DependencyProperty.Register(
        nameof(LearnMoreUri), typeof(Uri), typeof(WcrModelUnavailable), new PropertyMetadata(new Uri("https://learn.microsoft.com/windows/ai/apis/model-setup#prerequisites")));

    public Uri LearnMoreUri
    {
        get => (Uri)GetValue(LearnMoreUriProperty);
        set => SetValue(LearnMoreUriProperty, value);
    }

    public WcrModelUnavailable()
    {
        this.InitializeComponent();
    }
}