// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models
{
    internal abstract class BaseSampleNavigationParameters(TaskCompletionSource sampleLoadedCompletionSource, CancellationToken loadingCanceledToken)
    {
        public CancellationToken CancellationToken { get; private set; } = loadingCanceledToken;

        protected abstract string ChatClientModelPath { get; }
        protected abstract LlmPromptTemplate? ChatClientPromptTemplate { get; }

        public void NotifyCompletion()
        {
            sampleLoadedCompletionSource.SetResult();
        }

        public async Task<IChatClient?> GetIChatClientAsync()
        {
            return await GenAIModel.CreateAsync(ChatClientModelPath, ChatClientPromptTemplate, CancellationToken).ConfigureAwait(false);
        }
    }
}