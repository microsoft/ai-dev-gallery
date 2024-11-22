// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal class AudioRecorder : IDisposable
{
    private const int BufferSize = 32000; // Adjust buffer size if needed
    private readonly WaveInEvent waveIn;
    private readonly Action<byte[]> transcriptionCallback;
    private BlockingCollection<byte[]> audioQueue;
    private byte[] audioBuffer;
    private int bufferOffset;

    public AudioRecorder(Action<byte[]> transcriptionCallback)
    {
        this.transcriptionCallback = transcriptionCallback;
        audioQueue = [];
        waveIn = new WaveInEvent
        {
            BufferMilliseconds = 1000, // Increase buffer size in milliseconds
            WaveFormat = new WaveFormat(16000, 1) // Ensure correct format (16kHz, mono)
        };
        waveIn.DataAvailable += OnDataAvailable;
        audioBuffer = new byte[BufferSize];
        bufferOffset = 0;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        int bytesToCopy = e.BytesRecorded;
        int bytesCopied = 0;

        while (bytesToCopy > 0)
        {
            int spaceRemaining = BufferSize - bufferOffset;
            int bytesThisIteration = Math.Min(bytesToCopy, spaceRemaining);

            Buffer.BlockCopy(e.Buffer, bytesCopied, audioBuffer, bufferOffset, bytesThisIteration);

            bufferOffset += bytesThisIteration;
            bytesCopied += bytesThisIteration;
            bytesToCopy -= bytesThisIteration;

            if (bufferOffset >= BufferSize)
            {
                audioQueue.Add(audioBuffer);
                audioBuffer = new byte[BufferSize]; // Allocate a new buffer for the next data
                bufferOffset = 0;
            }
        }
    }

    public void ResetAudioQueue()
    {
        audioQueue.Dispose();
        audioQueue = [];
        audioBuffer = new byte[BufferSize];
        bufferOffset = 0;
    }

    public void StartRecording(CancellationToken cancellationToken = default)
    {
        ResetAudioQueue();

        waveIn.StartRecording();
        Task.Run(() => ProcessAudioQueue(cancellationToken), cancellationToken);
    }

    public void StopRecording()
    {
        waveIn.StopRecording();
        audioQueue.Add(audioBuffer);
    }

    public void Dispose()
    {
        waveIn?.StopRecording();
        waveIn?.Dispose();
        audioQueue?.Dispose();
    }

    private void ProcessAudioQueue(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (audioQueue.TryTake(out var audioData, TimeSpan.FromMilliseconds(1000)))
            {
                transcriptionCallback(audioData);
            }
        }
    }
}