// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIDevGallery.Tests.UnitTests.ExternalModelUtils;

/// <summary>
/// Tests for FoundryLocalChatClientAdapter focusing on pure functions and data transformations.
/// Note: Integration tests requiring actual FoundryLocal SDK initialization are excluded.
/// </summary>
[TestClass]
public class FoundryLocalChatClientAdapterTests
{
    [TestMethod]
    public void ConvertToOpenAIMessagesConvertsMultipleMessagesWithDifferentRoles()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What is the weather?"),
            new(ChatRole.Assistant, "I don't have access to real-time weather data.")
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("system", result[0].Role);
        Assert.AreEqual("You are a helpful assistant.", result[0].Content);
        Assert.AreEqual("user", result[1].Role);
        Assert.AreEqual("What is the weather?", result[1].Content);
        Assert.AreEqual("assistant", result[2].Role);
        Assert.AreEqual("I don't have access to real-time weather data.", result[2].Content);
    }

    [TestMethod]
    public void ConvertToOpenAIMessagesHandlesNullTextAsEmptyString()
    {
        // Arrange - Critical: null content should be converted to empty string, not cause NullReferenceException
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, (string?)null)
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("user", result[0].Role);
        Assert.AreEqual(string.Empty, result[0].Content);
    }

    [TestMethod]
    public void ConvertToOpenAIMessagesHandlesEmptyList()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ConvertToOpenAIMessagesHandlesCustomRoles()
    {
        // Arrange - Important: custom roles like "tool" should be preserved
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(new ChatRole("tool"), "Tool output")
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("tool", result[0].Role);
        Assert.AreEqual("Tool output", result[0].Content);
    }

    /// <summary>
    /// Uses reflection to invoke the private static ConvertToOpenAIMessages method.
    /// </summary>
    private static List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> InvokeConvertToOpenAIMessages(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var adapterType = Type.GetType("AIDevGallery.ExternalModelUtils.FoundryLocal.FoundryLocalChatClientAdapter, AIDevGallery");
        Assert.IsNotNull(adapterType, "FoundryLocalChatClientAdapter type not found");

        var method = adapterType.GetMethod(
            "ConvertToOpenAIMessages",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "ConvertToOpenAIMessages method not found");

        var result = method.Invoke(null, new object[] { messages });
        return (List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage>)result!;
    }
}