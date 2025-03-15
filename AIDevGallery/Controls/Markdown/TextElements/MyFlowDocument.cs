// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;
using Markdig.Syntax;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyFlowDocument : IAddChild
{
    private HtmlNode? _htmlNode;
    private RichTextBlock _richTextBlock = new RichTextBlock();
    private MarkdownObject? _markdownObject;

    // useless property
    public TextElement TextElement { get; set; } = new Run();

    public RichTextBlock RichTextBlock
    {
        get => _richTextBlock;
        set => _richTextBlock = value;
    }

    public bool IsHtml => _htmlNode != null;

    public MyFlowDocument()
    {
        _richTextBlock.LineHeight = 30;
    }

    public MyFlowDocument(MarkdownObject markdownObject)
        : base()
    {
        _markdownObject = markdownObject;
    }

    public MyFlowDocument(HtmlNode node)
        : base()
    {
        _htmlNode = node;
    }

    public void AddChild(IAddChild child)
    {
        TextElement element = child.TextElement;
        if (element != null)
        {
            if (element is Microsoft.UI.Xaml.Documents.Block block)
            {
                _richTextBlock.Blocks.Add(block);
            }
            else if (element is Inline inline)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(inline);
                _richTextBlock.Blocks.Add(paragraph);
            }
        }
    }
}