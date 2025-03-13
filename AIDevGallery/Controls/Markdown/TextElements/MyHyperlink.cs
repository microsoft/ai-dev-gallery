// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
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
        _hyperlink = new Hyperlink()
        {
            NavigateUri = Extensions.GetUri(url, baseUrl),
        };
    }

    public MyHyperlink(HtmlNode htmlNode, string? baseUrl)
    {
        _baseUrl = baseUrl;
        var url = htmlNode.GetAttribute("href", "#");
        _htmlNode = htmlNode;
        _hyperlink = new Hyperlink()
        {
            NavigateUri = Extensions.GetUri(url, baseUrl),
        };
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Microsoft.UI.Xaml.Documents.Inline inlineChild)
        {
            try
            {
                _hyperlink.Inlines.Add(inlineChild);
                // TODO: Add support for click handler
            }
            catch
            {
            }
        }
    }
}