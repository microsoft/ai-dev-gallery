// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using System;

namespace AIDevGallery.Samples.SharedCode;

internal partial class Message : ObservableObject
{
    [ObservableProperty]
    public partial string Content { get; set; }
    public DateTime MsgDateTime { get; private set; }

    public ChatRole Role { get; set; }

    public Message(string content, DateTime dateTime, ChatRole role)
    {
        Content = content;
        MsgDateTime = dateTime;
        Role = role;
    }

    public override string ToString()
    {
        return $"{MsgDateTime} {Content}";
    }
}