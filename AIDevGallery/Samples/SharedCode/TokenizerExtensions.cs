// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode;

internal static class TokenizerExtensions
{
    public static IEnumerable<EmbeddingModelInput> EncodeBatch(
        this BertTokenizer tokenizer,
        IEnumerable<string> sentences,
        int maxTokenCount = 512)
    {
        // Initialize max sequence length
        var maxSequenceTokenCount = 0;

        // Tokenize each of the sentences
        var tokenizedSentences =
            sentences
                .Select(s =>
                {
                    var res = tokenizer.EncodeToIds(s, maxTokenCount, out var normalizedText, out var textLength);
                    maxSequenceTokenCount = Math.Max(maxSequenceTokenCount, res.Count);
                    return res;
                })
                .ToList();

        var paddedSentences = tokenizedSentences.Select(tokens => tokenizer.ApplyDynamicPadding(tokens, maxSequenceTokenCount));

        // Provide option to return token type ids. 0 = context, 1 = query
        // Since this is not a question answering scenario, token type ids are all 0
        var tokenTypeIds = paddedSentences.Select(_ => Enumerable.Repeat(0, maxSequenceTokenCount));

        // Provide option to truncate with truncation strategies
        // Truncation strategies can be set to longest_first, only_first, only_second, do_not_truncate
        // In this case, the strategy is to truncate from the end - 2
        // This allows for adding the CLS token at the beginning and the SEP token at the end

        // Provide option to return attention mask 0 = pad, 1 = token
        var attentionMask = paddedSentences.Select(tokens => tokens.Select(token => token == 0 ? 0 : 1));

        return paddedSentences.Select((sentence, idx) =>
        {
            var s = sentence.Select(t => (long)t).ToArray();
            var a = attentionMask.ElementAt(idx).Select(t => (long)t).ToArray();
            var t = tokenTypeIds.ElementAt(idx).Select(t => (long)t).ToArray();
            return new EmbeddingModelInput { InputIds = s, AttentionMask = a, TokenTypeIds = t };
        });
    }

    private static IEnumerable<int> ApplyDynamicPadding(
        this BertTokenizer tokenizer,
        IEnumerable<int> tokens,
        int sequenceLength)
    {
        return tokens.Concat(Enumerable.Repeat(tokenizer.PaddingTokenId, sequenceLength - tokens.Count()));
    }
}