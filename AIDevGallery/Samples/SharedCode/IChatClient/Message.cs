// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using System;

namespace AIDevGallery.Samples.SharedCode;

internal partial class Message : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    public partial string Content { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThink))]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    public partial string ThinkContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    public partial bool IsPending { get; set; }

    public bool HasThink => !string.IsNullOrEmpty(ThinkContent);
    public bool IsThinking => (HasThink && string.IsNullOrEmpty(Content)) || IsPending;
    public string DisplayContent => IsThinking ? "<Thinking...>" : Content;
    public DateTime MsgDateTime { get; private set; }

    public ChatRole Role { get; set; }

    [ObservableProperty]
    public partial bool IsLastUserMessage { get; set; }

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