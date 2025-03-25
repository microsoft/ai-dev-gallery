// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Extensions.Tables;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyTableRow : IAddChild
{
    private TableRow _tableRow;
    private StackPanel _stackPanel;
    private Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyTableRow(TableRow tableRow)
    {
        _tableRow = tableRow;
        _paragraph = new Paragraph();

        _stackPanel = new StackPanel();
        _stackPanel.Orientation = Orientation.Horizontal;
        var inlineUIContainer = new InlineUIContainer();
        inlineUIContainer.Child = _stackPanel;
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        if (child is MyTableCell cellChild)
        {
            var richTextBlock = new RichTextBlock();
            richTextBlock.Blocks.Add((Paragraph)cellChild.TextElement);
            _stackPanel.Children.Add(richTextBlock);
        }
    }
}