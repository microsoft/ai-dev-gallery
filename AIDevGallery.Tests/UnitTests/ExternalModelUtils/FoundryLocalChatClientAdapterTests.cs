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
    public void ConvertToFoundryMessagesConvertsSimpleMessage()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "Hello, world!")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("user", result[0].Role);
        Assert.AreEqual("Hello, world!", result[0].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesConvertsMultipleMessages()
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
        Assert.AreEqual("system", result[0].Role);
        Assert.AreEqual("You are a helpful assistant.", result[0].Content);
        Assert.AreEqual("user", result[1].Role);
        Assert.AreEqual("What is the weather?", result[1].Content);
        Assert.AreEqual("assistant", result[2].Role);
        Assert.AreEqual("I don't have access to real-time weather data.", result[2].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesHandlesEmptyText()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, (string?)null)
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("user", result[0].Role);
        Assert.AreEqual(string.Empty, result[0].Content);
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
    public void ConvertToFoundryMessagesPreservesMultipleUserMessages()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.User, "First question"),
            new(ChatRole.User, "Second question"),
            new(ChatRole.User, "Third question")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.TrueForAll(m => m.Role == "user"));
        Assert.AreEqual("First question", result[0].Content);
        Assert.AreEqual("Second question", result[1].Content);
        Assert.AreEqual("Third question", result[2].Content);
    }

    [TestMethod]
    public void ConvertToFoundryMessagesHandlesCustomRoles()
    {
        // Arrange
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(new ChatRole("tool"), "Tool output")
        };

        // Act
        var result = InvokeConvertToFoundryMessages(messages);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("tool", result[0].Role);
        Assert.AreEqual("Tool output", result[0].Content);
    }

    /// <summary>
    /// Uses reflection to invoke the private static ConvertToFoundryMessages method.
    /// </summary>
    private static List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> InvokeConvertToFoundryMessages(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var adapterType = Type.GetType("AIDevGallery.ExternalModelUtils.FoundryLocal.FoundryLocalChatClientAdapter, AIDevGallery");
        Assert.IsNotNull(adapterType, "FoundryLocalChatClientAdapter type not found");

        var method = adapterType.GetMethod(
            "ConvertToFoundryMessages",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "ConvertToFoundryMessages method not found");

        var result = method.Invoke(null, new object[] { messages });
        return (List<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage>)result!;
    }
}


