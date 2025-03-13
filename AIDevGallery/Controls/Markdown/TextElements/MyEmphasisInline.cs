// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using System;
using Windows.UI.Text;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyEmphasisInline : IAddChild
{
    private Span _span;
    private EmphasisInline _markdownObject;

    private bool _isBold;
    private bool _isItalic;
    private bool _isStrikeThrough;

    public TextElement TextElement
    {
        get => _span;
    }

    public MyEmphasisInline(EmphasisInline emphasisInline)
    {
        _span = new Span();
        _markdownObject = emphasisInline;
    }

    public void AddChild(IAddChild child)
    {
        try
        {
            if (child is MyInlineText inlineText)
            {
                _span.Inlines.Add((Run)inlineText.TextElement);
            }
            else if (child is MyEmphasisInline emphasisInline)
            {
                if (emphasisInline._isBold) { SetBold(); }
                if (emphasisInline._isItalic) { SetItalic(); }
                if (emphasisInline._isStrikeThrough) { SetStrikeThrough(); }
                _span.Inlines.Add(emphasisInline._span);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(MyEmphasisInline)}.{nameof(AddChild)}: {ex.Message}");
        }
    }

    public void SetBold()
    {
        #if WINUI3
        _span.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        #elif WINUI2
        _span.FontWeight = Windows.UI.Text.FontWeights.Bold;
        #endif

        _isBold = true;
    }

    public void SetItalic()
    {
        _span.FontStyle = FontStyle.Italic;
        _isItalic = true;
    }

    public void SetStrikeThrough()
    {
        #if WINUI3
        _span.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        #elif WINUI2
        _span.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        #endif

        _isStrikeThrough = true;
    }

    public void SetSubscript()
    {
        _span.SetValue(Typography.VariantsProperty, FontVariants.Subscript);
    }

    public void SetSuperscript()
    {
        _span.SetValue(Typography.VariantsProperty, FontVariants.Superscript);
    }
}