// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Text;

namespace AIDevGallery.Samples.SharedCode;

/// <summary>Produces append-only display deltas for one raw response.</summary>
internal sealed class TextWatermarker
{
    private const int Interval = 400;
    private const string Marker = "\u200B\u034F\u2060\u200B";

    private long _sinceMarker;
    private int _previous = -1;
    private char _pendingHighSurrogate;

    public static string Apply(string text)
    {
        var stream = new TextWatermarker();
        return stream.Append(text) + stream.Finish();
    }

    public string Append(string text)
    {
        if (_pendingHighSurrogate != '\0' && text.Length != 0)
        {
            text = _pendingHighSurrogate + text;
            _pendingHighSurrogate = '\0';
        }

        StringBuilder? result = null;
        int start = 0;
        int offset = 0;
        while (offset < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out int length);
            if (status == OperationStatus.NeedMoreData)
            {
                _pendingHighSurrogate = text[offset];
                break;
            }

            int current = status == OperationStatus.Done ? rune.Value : -1;
            if (_sinceMarker >= Interval && TextWatermarkingUnicodeData.IsSafeInsertionBoundary(_previous, current))
            {
                (result ??= new StringBuilder()).Append(text, start, offset - start).Append(Marker);
                start = offset;
                _sinceMarker = 0;
            }

            _previous = current;
            offset += length;
            _sinceMarker++;
        }

        return result?.Append(text, start, offset - start).ToString() ?? text[..offset];
    }

    public string Finish()
    {
        var tail = string.Empty;
        if (_pendingHighSurrogate != '\0')
        {
            // Preserve a truncated last code point rather than replacing or dropping user-visible text.
            tail = _pendingHighSurrogate.ToString();
            _pendingHighSurrogate = '\0';
            _sinceMarker++;
        }

        if (_sinceMarker >= Interval)
        {
            tail += "\n" + Marker;
            _sinceMarker = 0;
        }

        return tail;
    }
}

/// <summary>Display projection for views which keep the accumulated raw response, e.g. chat history.</summary>
internal sealed class WatermarkedText
{
    private TextWatermarker _stream = new();
    private string _raw = string.Empty;
    private string _display = string.Empty;

    public string Update(string text, bool isFinal = false)
    {
        // A parser may revise already-consumed text, e.g. by trimming a Markdown fence.
        if (!text.StartsWith(_raw, StringComparison.Ordinal))
        {
            _stream = new TextWatermarker();
            _raw = _display = string.Empty;
        }

        _display += _stream.Append(text[_raw.Length..]);
        _raw = text;
        if (isFinal)
        {
            _display += _stream.Finish();
        }

        return _display;
    }
}