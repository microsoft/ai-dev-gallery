// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class TextWatermarkingTests
{
    private const string Mark = "\u200B\u034F\u2060\u200B";

    [TestMethod]
    public void MarksOnlyWhenDueAndFinalizesAnUnbrokenTailOnce()
    {
        var shortText = new string('a', 399);
        Assert.AreEqual(shortText, TextWatermarker.Apply(shortText));
        var prefix = shortText + " ";
        var marker = new WatermarkedText();
        Assert.AreEqual(prefix, marker.Update(prefix));
        Assert.AreEqual(prefix + "\n" + Mark, TextWatermarker.Apply(prefix));

        var tail = new string('\u0E01', 400);
        var raw = prefix + tail + "\n";
        Assert.AreEqual(prefix + Mark + tail + "\n", marker.Update(raw));
        var expected = prefix + Mark + tail + "\n\n" + Mark;
        Assert.AreEqual(expected, marker.Update(raw, isFinal: true));
        Assert.AreEqual(expected, marker.Update(raw, isFinal: true));
    }

    [TestMethod]
    public void StreamedUtf16MatchesOneShotEvenAcrossSplitHanSurrogates()
    {
        const string han = "\U00020000";
        var interval = string.Concat(Enumerable.Repeat(han, 400));
        var raw = interval + interval + han;
        var marker = new TextWatermarker();
        string display = string.Empty;
        foreach (char unit in raw)
        {
            display += marker.Append(unit.ToString());
            Assert.IsFalse(display.Length != 0 && char.IsHighSurrogate(display[^1]));
        }

        var expected = interval + Mark + interval + Mark + han;
        Assert.AreEqual(expected, display + marker.Finish());
        Assert.AreEqual(expected, TextWatermarker.Apply(raw));
        Assert.AreEqual("\uD840", TextWatermarker.Apply("\uD840"));
    }

    [TestMethod]
    [DataRow(" \u0301")] // Combining mark
    [DataRow(" \u093E")] // Spacing mark
    [DataRow(" \u200D")] // ZWJ
    [DataRow(" \U0001F3FD")] // Emoji modifier
    [DataRow(" \u2060")] // Format code point
    [DataRow("\r\n")] // Never split CRLF
    public void WaitsForAVisibleSafeBoundary(string unsafePair)
    {
        var prefix = new string('a', 399) + unsafePair + "\u3000";
        Assert.AreEqual(prefix + Mark + "end", TextWatermarker.Apply(prefix + "end"));
    }

    [TestMethod]
    public void ChatDisplaysMarkedAnswerAndReasoningButKeepsHistoryRaw()
    {
        var raw = new string('a', 399) + " next";
        var message = new Message(raw, DateTime.UnixEpoch, ChatRole.Assistant, new WatermarkedText())
        {
            ThinkContent = new string('b', 400)
        };
        message.Complete();
        Assert.AreEqual(raw.Insert(400, Mark), message.DisplayContent);
        Assert.AreEqual(message.ThinkContent + "\n" + Mark, message.DisplayThinkContent);
        Assert.AreEqual(raw, message.Content);
        Assert.AreEqual(new string('b', 400), message.ThinkContent);
    }

    [TestMethod]
    [DataRow("ONNX", true)]
    [DataRow("FoundryLocal", true)]
    [DataRow("Ollama", true)]
    [DataRow("AionInstruct", false)]
    [DataRow("PhiSilica", false)]
    public async Task DisplayPolicyUsesTheProviderNotTheModelName(string provider, bool shouldMark)
    {
        var first = new string('x', 399) + " ";

        // An existing sequence from a provider must not be stripped.
        var second = "answer" + Mark;
        using var client = new StubClient(provider, first, second);
        var prompt = new ChatMessage(ChatRole.User, first + "prompt");
        var deltas = new List<string>();
        await client.StreamDisplayTextAsync([prompt], null, deltas.Add);

        var expected = first + (shouldMark ? Mark : string.Empty) + second;
        CollectionAssert.AreEqual(new[] { first, (shouldMark ? Mark : string.Empty) + second }, deltas);
        Assert.AreEqual(expected, client.GetDisplayText(first + second));
        Assert.AreEqual(first + "prompt", prompt.Text);
    }

    [TestMethod]
    public async Task StructuredOutputIsNotChangedAndFinalFallbackIsNotAModelToken()
    {
        var json = "{\"answer\": \"" + new string('x', 400) + "\"}";
        using var client = new StubClient("FoundryLocal", json);
        string display = string.Empty;
        int tokens = 0;
        await client.StreamDisplayTextAsync([], new() { ResponseFormat = ChatResponseFormat.Json }, s => display += s);
        Assert.AreEqual(json, display);
        display = string.Empty;
        await client.StreamDisplayTextAsync([], null, s => display += s, () => tokens++);
        Assert.AreEqual(json + "\n" + Mark, display);
        Assert.AreEqual(1, tokens);
    }

    [TestMethod]
    public async Task StoppingAResponsePublishesTheDueFallbackWithoutSwallowingCancellation()
    {
        var prefix = new string('a', 400);
        using var client = new StubClient("FoundryLocal", prefix, "not generated");
        using var stop = new CancellationTokenSource();
        var deltas = new List<string>();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => client.StreamDisplayTextAsync(
            [],
            null,
            text =>
            {
                deltas.Add(text);
                stop.Cancel();
            },
            cancellationToken: stop.Token));
        CollectionAssert.AreEqual(new[] { prefix, "\n" + Mark }, deltas);
    }

    [TestMethod]
    [DataRow("Chat", true)]
    [DataRow("Smart Paste", true)]
    [DataRow("Whisper Live Transcription", false)]
    [DataRow("Describe Image", false)]
    public void ExportIncludesWatermarkerAndItsUnicodeTables(string name, bool usesChatClient)
    {
        var sample = SampleDetails.Samples.Single(s => s.Name == name);
        var modelType = sample.Model1Types[0];
        var files = sample.GetAllSharedCode(new() { [modelType] = new("test", "model", "model", 1, HardwareAccelerator.CPU) });
        Assert.AreEqual(usesChatClient, files.Contains(SharedCodeEnum.ChatClientTextOutput));
        CollectionAssert.Contains(files, SharedCodeEnum.TextWatermarker);
        CollectionAssert.Contains(files, SharedCodeEnum.TextWatermarkingUnicodeData);
    }

    private sealed class StubClient(string provider, params string[] chunks) : IChatClient
    {
        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata(provider, defaultModelId: "AionInstruct") : null;

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}