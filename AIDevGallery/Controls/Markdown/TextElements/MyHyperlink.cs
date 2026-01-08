// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
using System;
using Windows.Foundation;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyHyperlink : IAddChild
{
    private Hyperlink _hyperlink;
    private LinkInline? _linkInline;
    private HtmlNode? _htmlNode;
    private string? _baseUrl;

    public event TypedEventHandler<Hyperlink, HyperlinkClickEventArgs> ClickEvent
    {
        add
        {
            _hyperlink.Click += value;
        }
        remove
        {
            _hyperlink.Click -= value;
        }
    }

    public bool IsHtml => _htmlNode != null;

    public TextElement TextElement
    {
        get => _hyperlink;
    }

    public MyHyperlink(LinkInline linkInline, string? baseUrl)
    {
        _baseUrl = baseUrl;
        var url = linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() ?? linkInline.Url : linkInline.Url;
        _linkInline = linkInline;
        _hyperlink = new Hyperlink();
    }

    public MyHyperlink(HtmlNode htmlNode, string? baseUrl)
    {
        _baseUrl = baseUrl;
        var url = htmlNode.GetAttribute("href", "#");
        _htmlNode = htmlNode;
        _hyperlink = new Hyperlink();
    }

    public void AddChild(IAddChild child)
    {
        // Hyperlink cannot contain InlineUIContainer - this is a WinUI limitation
        if (child.TextElement is not Microsoft.UI.Xaml.Documents.Inline inlineChild || inlineChild is InlineUIContainer)
        {
            return;
        }

        try
        {
            _hyperlink.Inlines.Add(inlineChild);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception when adding {inlineChild.GetType().Name}: {ex.GetType().Name} - {ex.Message}");
        }
    }
}