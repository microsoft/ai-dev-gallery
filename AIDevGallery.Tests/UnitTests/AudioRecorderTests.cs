// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class AudioRecorderTests
{
    [TestMethod]
    public async Task StopPadsShortWhisperTailWithoutReplayingSamplesOnRestart()
    {
        const int oneSecond = 32000; // 16 kHz, mono, PCM16
        using var input = new TestWaveIn();
        using var recorder = new AudioRecorder(input);
        var pcm = new byte[oneSecond + 160]; // Reproduces an 80-sample tail that fails Whisper's reflect padding.
        Array.Fill(pcm, (byte)31);
        var first = await RecordAsync(pcm);
        var second = await RecordAsync([23, 7]);
        var empty = await RecordAsync([]);

        Assert.AreEqual(2, first.Count);
        CollectionAssert.AreEqual(pcm[..oneSecond], first[0]);
        byte[] expectedTail = new byte[oneSecond];
        Array.Fill(expectedTail, (byte)31, 0, 160);
        CollectionAssert.AreEqual(expectedTail, first[1]);
        Assert.AreEqual(1, second.Count);
        byte[] expectedRestart = new byte[oneSecond];
        expectedRestart[0] = 23;
        expectedRestart[1] = 7;
        CollectionAssert.AreEqual(expectedRestart, second[0]);
        Assert.AreEqual(0, empty.Count);

        async Task<List<byte[]>> RecordAsync(byte[] data)
        {
            var stream = recorder.StartRecording(CancellationToken.None);
            input.Push(data);
            recorder.StopRecording();
            List<byte[]> chunks = [];
            await foreach (var chunk in stream)
            {
                chunks.Add(chunk);
            }

            return chunks;
        }
    }

    private sealed class TestWaveIn : IWaveIn
    {
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public WaveFormat WaveFormat { get; set; } = new WaveFormat(16000, 1);
        public void StartRecording()
        {
        }

        public void Push(byte[] pcm) => DataAvailable?.Invoke(this, new WaveInEventArgs(pcm, pcm.Length));
        public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
        public void Dispose()
        {
        }
    }
}