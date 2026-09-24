// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace AIDevGallery.Tests.UnitTests;

/// <summary>
/// Tests for FoundryLocalChatClientAdapter focusing on pure functions and data transformations.
/// Note: Integration tests requiring actual FoundryLocal SDK initialization are excluded.
/// </summary>
[TestClass]
public class FoundryLocalChatClientAdapterTests
{
    [TestMethod]
    public void ConvertToFoundryMessagesConvertsMultipleMessagesWithDifferentRoles()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, "What is the weather?"),
            new(ChatRole.Assistant, "I don't have access to real-time weather data.")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(MessageRole.System, result[0].Role);
        Assert.AreEqual("You are a helpful assistant.", result[0].Content);
        Assert.AreEqual(MessageRole.User, result[1].Role);
        Assert.AreEqual("What is the weather?", result[1].Content);
        Assert.AreEqual(MessageRole.Assistant, result[2].Role);
        Assert.AreEqual("I don't have access to real-time weather data.", result[2].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesSkipsNullText()
    {
        // Arrange - Critical: null content should be converted to empty string, not cause NullReferenceException
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, (string?)null)
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesHandlesEmptyList()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesHandlesCustomRoles()
    {
        // Arrange - Important: custom roles like "tool" should be preserved
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(new ChatRole("tool"), "Tool output")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(MessageRole.Tool, result[0].Role);
        Assert.AreEqual("Tool output", result[0].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesPreservesMessageOrder()
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
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(MessageRole.System, result[0].Role);
        Assert.AreEqual("System message", result[0].Content);
        Assert.AreEqual(MessageRole.User, result[1].Role);
        Assert.AreEqual("First user message", result[1].Content);
        Assert.AreEqual(MessageRole.Assistant, result[2].Role);
        Assert.AreEqual("First assistant response", result[2].Content);
        Assert.AreEqual(MessageRole.User, result[3].Role);
        Assert.AreEqual("Second user message", result[3].Content);
        Assert.AreEqual(MessageRole.Assistant, result[4].Role);
        Assert.AreEqual("Second assistant response", result[4].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesPreservesTextContent()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "Text only message")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Text only message", result[0].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesMultipleConsecutiveSameRoleAllowed()
    {
        // Arrange - Some models allow multiple messages from same role
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "First question"),
            new(ChatRole.User, "Second question"),
            new(ChatRole.Assistant, "Combined answer")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(MessageRole.User, result[0].Role);
        Assert.AreEqual(MessageRole.User, result[1].Role);
        Assert.AreEqual(MessageRole.Assistant, result[2].Role);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesVeryLongMessageIsPreserved()
    {
        // Arrange - Test with a very long message
        var longContent = new string('A', 10000); // 10K characters
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, longContent)
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(longContent, result[0].Content);
        Assert.AreEqual(10000, result[0].Content.Length);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesSpecialCharactersArePreserved()
    {
        // Arrange - Test with special characters that might need escaping
        var specialContent = "Hello\nWorld\tWith\"Quotes\" and 'apostrophes' & symbols <>";
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, specialContent)
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(specialContent, result[0].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesRejectsUnsupportedRole()
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(new ChatRole("unsupported"), "Content")
        };

        Assert.ThrowsExactly<NotSupportedException>(() => InvokeConvertToFoundryMessages(messages));
    }

    [TestMethod]
    public void ConvertToFoundryMessagesRejectsNonTextContent()
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(
                ChatRole.User,
                [
                    new TextContent("Describe this image"),
                    new DataContent(new Uri("data:image/png;base64,AA=="), "image/png")
                ])
        };

        Assert.ThrowsExactly<NotSupportedException>(() => InvokeConvertToFoundryMessages(messages));
    }

    /// <summary>
    /// Uses reflection to invoke the private static ConvertToFoundryMessageData method.
    /// </summary>
    private static List<(MessageRole Role, string Content)> InvokeConvertToFoundryMessages(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var adapterType = Type.GetType("AIDevGallery.Samples.SharedCode.FoundryLocalChatClientAdapter, AIDevGallery");
        Assert.IsNotNull(adapterType, "FoundryLocalChatClientAdapter type not found");

        var method = adapterType.GetMethod(
            "ConvertToFoundryMessageData",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "ConvertToFoundryMessageData method not found");

        var result = new List<(MessageRole Role, string Content)>();
        foreach (var message in messages)
        {
            try
            {
                if (method.Invoke(null, [message]) is ValueTuple<MessageRole, string> messageData)
                {
                    result.Add(messageData);
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        return result;
    }
}