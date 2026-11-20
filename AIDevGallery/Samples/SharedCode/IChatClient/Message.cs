// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using System;

namespace AIDevGallery.Samples.SharedCode;

internal partial class Message : ObservableObject
{
    private readonly WatermarkedText? _watermarker;
    private WatermarkedText? _thinkWatermarker;
    private bool _isComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    public partial string Content { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThink))]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    [NotifyPropertyChangedFor(nameof(DisplayThinkContent))]
    public partial string ThinkContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(DisplayContent))]
    public partial bool IsPending { get; set; }

    public bool HasThink => !string.IsNullOrEmpty(ThinkContent);
    public bool IsThinking => (HasThink && string.IsNullOrEmpty(Content)) || IsPending;
    public string DisplayContent => IsThinking ? "<Thinking...>" : _watermarker?.Update(Content, _isComplete) ?? Content;
    public string DisplayThinkContent => _watermarker == null ? ThinkContent : (_thinkWatermarker ??= new()).Update(ThinkContent, _isComplete);
    public DateTime MsgDateTime { get; private set; }

    public ChatRole Role { get; set; }

    [ObservableProperty]
    public partial bool IsLastUserMessage { get; set; }

    public Message(string content, DateTime dateTime, ChatRole role, WatermarkedText? watermarker = null)
    {
        _watermarker = watermarker;
        Content = content;
        MsgDateTime = dateTime;
        Role = role;
    }

    public void Complete()
    {
        _isComplete = true;
        OnPropertyChanged(nameof(DisplayContent));
        OnPropertyChanged(nameof(DisplayThinkContent));
    }

    public override string ToString()
    {
        return $"{MsgDateTime} {Content}";
    }
}