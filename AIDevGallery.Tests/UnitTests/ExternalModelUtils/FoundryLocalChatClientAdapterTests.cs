// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIDevGallery.Tests.UnitTests;

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

    [TestMethod]
    public void ConvertToOpenAIMessagesPreservesMessageOrder()
    {
        // Arrange - Critical: message order must be preserved for proper conversation flow
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "System message"),
            new(ChatRole.User, "First user message"),
            new(ChatRole.Assistant, "First assistant response"),
            new(ChatRole.User, "Second user message"),
            new(ChatRole.Assistant, "Second assistant response")
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(5, result.Count);
        Assert.AreEqual("system", result[0].Role);
        Assert.AreEqual("System message", result[0].Content);
        Assert.AreEqual("user", result[1].Role);
        Assert.AreEqual("First user message", result[1].Content);
        Assert.AreEqual("assistant", result[2].Role);
        Assert.AreEqual("First assistant response", result[2].Content);
        Assert.AreEqual("user", result[3].Role);
        Assert.AreEqual("Second user message", result[3].Content);
        Assert.AreEqual("assistant", result[4].Role);
        Assert.AreEqual("Second assistant response", result[4].Content);
    }

    [TestMethod]
    public void ConvertToOpenAIMessages_OnlyTextContent_IgnoresMultiModal()
    {
        // NOTE: This test documents current limitation - multi-modal content is not supported
        // The current implementation only handles text content via ChatMessage.Text property
        // Future enhancement may add image/audio support

        // Arrange - Message with only text content
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "Text only message")
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Text only message", result[0].Content);
    }

    [TestMethod]
    public void ConvertToOpenAIMessages_MultipleConsecutiveSameRole_Allowed()
    {
        // Arrange - Some models allow multiple messages from same role
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "First question"),
            new(ChatRole.User, "Second question"),
            new(ChatRole.Assistant, "Combined answer")
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("user", result[0].Role);
        Assert.AreEqual("user", result[1].Role);
        Assert.AreEqual("assistant", result[2].Role);
    }

    [TestMethod]
    public void ConvertToOpenAIMessages_VeryLongMessage_IsPreserved()
    {
        // Arrange - Test with a very long message
        var longContent = new string('A', 10000); // 10K characters
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, longContent)
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(longContent, result[0].Content);
        Assert.AreEqual(10000, result[0].Content.Length);
    }

    [TestMethod]
    public void ConvertToOpenAIMessages_SpecialCharacters_ArePreserved()
    {
        // Arrange - Test with special characters that might need escaping
        var specialContent = "Hello\nWorld\tWith\"Quotes\" and 'apostrophes' & symbols <>";
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, specialContent)
        };

        // Act
        var result = InvokeConvertToOpenAIMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(specialContent, result[0].Content);
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