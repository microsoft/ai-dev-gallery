// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace AIDevGallery.Samples.SharedCode;

internal class AudioRecorder : IDisposable
{
    private const int BufferSize = 32000; // Adjust buffer size if needed
    private readonly IWaveIn waveIn;
    private Channel<byte[]>? audioQueue;
    private byte[] audioBuffer;
    private int bufferOffset;

    public AudioRecorder()
        : this(new WaveInEvent
        {
            BufferMilliseconds = 1000, // Increase buffer size in milliseconds
            WaveFormat = new WaveFormat(16000, 1) // Ensure correct format (16kHz, mono)
        })
    {
    }

    internal AudioRecorder(IWaveIn waveIn)
    {
        this.waveIn = waveIn;
        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += OnRecordingStopped;
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
                audioQueue!.Writer.TryWrite(audioBuffer);
                audioBuffer = new byte[BufferSize]; // Allocate a new buffer for the next data
                bufferOffset = 0;
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (bufferOffset > 0)
        {
            // Whisper's spectrogram cannot reflect-pad a very short tail. Keep the unused PCM samples silent.
            audioQueue?.Writer.TryWrite(audioBuffer);
            audioBuffer = new byte[BufferSize];
            bufferOffset = 0;
        }

        audioQueue?.Writer.TryComplete(e.Exception);
    }

    public IAsyncEnumerable<byte[]> StartRecording(CancellationToken cancellationToken)
    {
        audioQueue = Channel.CreateUnbounded<byte[]>();
        bufferOffset = 0;
        waveIn.StartRecording();
        return audioQueue.Reader.ReadAllAsync(cancellationToken);
    }

    public void StopRecording()
    {
        waveIn.StopRecording();
    }

    public void Dispose()
    {
        waveIn.Dispose();
    }
}