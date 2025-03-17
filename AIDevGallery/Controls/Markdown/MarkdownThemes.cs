// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using FontWeight = Windows.UI.Text.FontWeight;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace CommunityToolkit.WinUI.Controls.MarkdownTextBlockRns;

internal sealed class MarkdownThemes : DependencyObject
{
    internal static MarkdownThemes Default { get; } = new();

    public Thickness Padding { get; set; } = new(8);

    public Thickness InternalMargin { get; set; } = new(4);

    public CornerRadius CornerRadius { get; set; } = new(4);

    public double H1FontSize { get; set; } = 22;

    public double H2FontSize { get; set; } = 20;

    public double H3FontSize { get; set; } = 18;

    public double H4FontSize { get; set; } = 16;

    public double H5FontSize { get; set; } = 14;

    public double H6FontSize { get; set; } = 12;

    public Brush HeadingForeground { get; set; } = Extensions.GetAccentColorBrush();

    public FontWeight H1FontWeight { get; set; } = FontWeights.Bold;

    public FontWeight H2FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H3FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H4FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H5FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H6FontWeight { get; set; } = FontWeights.Normal;

    public Thickness H1Margin { get; set; } = new(left: 0, top: 14, right: 0, bottom: 0);
    public Thickness H2Margin { get; set; } = new(left: 0, top: 14, right: 0, bottom: 0);
    public Thickness H3Margin { get; set; } = new(left: 0, top: 14, right: 0, bottom: 0);
    public Thickness H4Margin { get; set; } = new(left: 0, top: 14, right: 0, bottom: 0);
    public Thickness H5Margin { get; set; } = new(left: 0, top: 8, right: 0, bottom: 0);
    public Thickness H6Margin { get; set; } = new(left: 0, top: 8, right: 0, bottom: 0);

    public Brush InlineCodeBackground { get; set; } = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
    public Brush InlineCodeForeground { get; set; } = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    public Brush InlineCodeBorderBrush { get; set; } = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

    public Thickness InlineCodeBorderThickness { get; set; } = new (1);

    public CornerRadius InlineCodeCornerRadius { get; set; } = new (2);

    public Thickness InlineCodePadding { get; set; } = new(0);

    public double InlineCodeFontSize { get; set; } = 10;

    public FontWeight InlineCodeFontWeight { get; set; } = FontWeights.Normal;
}