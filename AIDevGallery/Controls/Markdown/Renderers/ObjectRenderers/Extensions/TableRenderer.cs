// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Extensions.Tables;
using Microsoft.UI.Xaml;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Extensions;

internal class TableRenderer : UWPObjectRenderer<Table>
{
    protected override void Write(WinUIRenderer renderer, Table table)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(table);

        var myTable = new MyTable(table);

        renderer.Push(myTable);

        for (var rowIndex = 0; rowIndex < table.Count; rowIndex++)
        {
            var rowObj = table[rowIndex];
            var row = (TableRow)rowObj;

            for (var i = 0; i < row.Count; i++)
            {
                var cellObj = row[i];
                var cell = (TableCell)cellObj;
                var textAlignment = TextAlignment.Left;

                var columnIndex = i;

                if (table.ColumnDefinitions.Count > 0)
                {
                    columnIndex = cell.ColumnIndex < 0 || cell.ColumnIndex >= table.ColumnDefinitions.Count
                        ? i
                        : cell.ColumnIndex;
                    columnIndex = columnIndex >= table.ColumnDefinitions.Count ? table.ColumnDefinitions.Count - 1 : columnIndex;
                    var alignment = table.ColumnDefinitions[columnIndex].Alignment;
                    textAlignment = alignment switch
                    {
                        TableColumnAlign.Center => TextAlignment.Center,
                        TableColumnAlign.Left => TextAlignment.Left,
                        TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left,
                    };
                }

                var myCell = new MyTableCell(cell, textAlignment, row.IsHeader, columnIndex, rowIndex);

                renderer.Push(myCell);
                renderer.Write(cell);
                renderer.Pop();
            }
        }

        renderer.Pop();
    }
}