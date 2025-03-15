// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyHyperlinkButton : IAddChild
{
    private HyperlinkButton _hyperLinkButton;
    private InlineUIContainer _inlineUIContainer = new InlineUIContainer();
    private MyFlowDocument _flowDoc;
    private string? _baseUrl;
    private LinkInline? _linkInline;
    private HtmlNode? _htmlNode;

    public event RoutedEventHandler? ClickEvent
    {
        add
        {
            _hyperLinkButton.Click += value;
        }
        remove
        {
            _hyperLinkButton.Click -= value;
        }
    }

    public bool IsHtml => _htmlNode != null;

    public TextElement TextElement
    {
        get => _inlineUIContainer;
    }

    public MyHyperlinkButton(LinkInline linkInline, string? baseUrl)
        : this(linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() ?? linkInline.Url : linkInline.Url, baseUrl, null, linkInline)
    {
    }

    public MyHyperlinkButton(HtmlNode htmlNode, string? baseUrl)
        : this(htmlNode.GetAttribute("href", "#"), baseUrl, htmlNode, null)
    {
    }

    private MyHyperlinkButton(string? url, string? baseUrl, HtmlNode? htmlNode, LinkInline? linkInline)
    {
        _baseUrl = baseUrl;
        _htmlNode = htmlNode;
        _linkInline = linkInline;
        _hyperLinkButton = new HyperlinkButton
        {
            NavigateUri = Extensions.GetUri(url, baseUrl),
        };
        _hyperLinkButton.Padding = new Thickness(0);
        _hyperLinkButton.Margin = new Thickness(0);
        if (_htmlNode != null)
        {
            _flowDoc = new MyFlowDocument(_htmlNode);
        }
        else
        {
            _flowDoc = new MyFlowDocument(_linkInline!);
        }

        _inlineUIContainer.Child = _hyperLinkButton;
        _hyperLinkButton.Content = _flowDoc.RichTextBlock;
    }

    public void AddChild(IAddChild child)
    {
        _flowDoc.AddChild(child);
    }
}