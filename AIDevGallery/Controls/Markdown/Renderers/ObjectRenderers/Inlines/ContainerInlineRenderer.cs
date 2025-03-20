// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

﻿using Markdig.Syntax.Inlines;
﻿using System;

﻿namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class ContainerInlineRenderer : UWPObjectRenderer<ContainerInline>
{
    protected override void Write(WinUIRenderer renderer, ContainerInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

        foreach (var inline in obj)
        {
            renderer.Write(inline);
        }
    }
}