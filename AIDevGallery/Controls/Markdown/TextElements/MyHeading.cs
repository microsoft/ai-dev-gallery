// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyHeading : IAddChild
{
    private Paragraph _paragraph;
    private HeadingBlock? _headingBlock;
    private HtmlNode? _htmlNode;
    private MarkdownConfig _config;

    public bool IsHtml => _htmlNode != null;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyHeading(HeadingBlock headingBlock, MarkdownConfig config)
    {
        _headingBlock = headingBlock;
        _paragraph = new Paragraph();
        _config = config;

        SetHProperties(headingBlock.Level);
    }

    public MyHeading(HtmlNode htmlNode, MarkdownConfig config)
    {
        _htmlNode = htmlNode;
        _paragraph = new Paragraph();
        _config = config;

        var align = _htmlNode.GetAttribute("align", "left");
        _paragraph.TextAlignment = align switch
        {
            "left" => TextAlignment.Left,
            "right" => TextAlignment.Right,
            "center" => TextAlignment.Center,
            "justify" => TextAlignment.Justify,
            _ => TextAlignment.Left,
        };

#pragma warning disable CA1305 // Specify IFormatProvider
        SetHProperties(int.Parse(htmlNode.Name.Substring(1)));
#pragma warning restore CA1305 // Specify IFormatProvider
    }

    private void SetHProperties(int level)
    {
        _paragraph.FontSize = level switch
        {
            1 => _config.Themes.H1FontSize,
            2 => _config.Themes.H2FontSize,
            3 => _config.Themes.H3FontSize,
            4 => _config.Themes.H4FontSize,
            5 => _config.Themes.H5FontSize,
            _ => _config.Themes.H6FontSize,
        };
        _paragraph.Foreground = _config.Themes.HeadingForeground;
        _paragraph.FontWeight = level switch
        {
            1 => _config.Themes.H1FontWeight,
            2 => _config.Themes.H2FontWeight,
            3 => _config.Themes.H3FontWeight,
            4 => _config.Themes.H4FontWeight,
            5 => _config.Themes.H5FontWeight,
            _ => _config.Themes.H6FontWeight,
        };
        _paragraph.Margin = level switch
        {
            1 => _config.Themes.H1Margin,
            2 => _config.Themes.H2Margin,
            3 => _config.Themes.H3Margin,
            4 => _config.Themes.H4Margin,
            5 => _config.Themes.H5Margin,
            _ => _config.Themes.H6Margin,
        };
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            _paragraph.Inlines.Add(inlineChild);
        }
    }
}