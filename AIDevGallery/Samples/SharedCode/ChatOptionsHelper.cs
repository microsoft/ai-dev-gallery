// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;

namespace AIDevGallery.Samples.SharedCode;

internal static class ChatOptionsHelper
{
    // Search Options
    public const int DefaultTopK = 50;
    public const float DefaultTopP = 0.9f;
    public const float DefaultTemperature = 1;
    public const int DefaultMinLength = 0;
    public const int DefaultMaxLength = 1024;
    public const bool DefaultDoSample = false;

    public static ChatOptions GetDefaultChatOptions(this IChatClient? chatClient)
    {
        var chatOptions = chatClient?.GetService<ChatOptions>();
        return chatOptions ?? new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "min_length", DefaultMinLength },
                { "do_sample", DefaultDoSample },
            },
            MaxOutputTokens = DefaultMaxLength,
            Temperature = DefaultTemperature,
            TopP = DefaultTopP,
            TopK = DefaultTopK,
        };
    }
}