// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;
using Markdig.Extensions.TaskLists;
using System;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Extensions;

internal class TaskListRenderer : UWPObjectRenderer<TaskList>
{
    protected override void Write(WinUIRenderer renderer, TaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(taskList);

        var checkBox = new MyTaskListCheckBox(taskList);
        renderer.WriteInline(checkBox);
    }
}