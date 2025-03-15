// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Extensions.Tables;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyTable : IAddChild
{
    private Table _table;
    private Paragraph _paragraph;
    private MyTableUIElement _tableElement;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyTable(Table table)
    {
        _table = table;
        _paragraph = new Paragraph();
        var row = table.FirstOrDefault() as TableRow;
        var column = row == null ? 0 : row.Count;

        _tableElement = new MyTableUIElement(
            column,
            table.Count,
            1,
            new SolidColorBrush(Colors.Gray));

        var inlineUIContainer = new InlineUIContainer();
        inlineUIContainer.Child = _tableElement;
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        if (child is MyTableCell cellChild)
        {
            var cell = cellChild.Container;

            Grid.SetColumn(cell, cellChild.ColumnIndex);
            Grid.SetRow(cell, cellChild.RowIndex);
            Grid.SetColumnSpan(cell, cellChild.ColumnSpan);
            Grid.SetRowSpan(cell, cellChild.RowSpan);

            _tableElement.Children.Add(cell);
        }
    }
}