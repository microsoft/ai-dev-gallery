// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Syntax;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyBlockContainer : IAddChild
{
    private ContainerBlock _containerBlock;
    private InlineUIContainer _inlineUIContainer;
    private MyFlowDocument _flowDocument;
    private Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyBlockContainer(ContainerBlock containerBlock)
    {
        _containerBlock = containerBlock;
        _inlineUIContainer = new InlineUIContainer();
        _flowDocument = new MyFlowDocument(containerBlock);
        _inlineUIContainer.Child = _flowDocument.RichTextBlock;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(_inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}