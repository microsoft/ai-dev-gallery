// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using System.Threading;

namespace AIDevGallery.Models
{
    internal class SampleNavigationParameters(
            string modelPath,
            HardwareAccelerator hardwareAccelerator,
            LlmPromptTemplate? promptTemplate,
            CancellationToken loadingCanceledToken)
        : BaseSampleNavigationParameters(loadingCanceledToken)
    {
        public string ModelPath { get; } = modelPath;
        public HardwareAccelerator HardwareAccelerator { get; } = hardwareAccelerator;
        public LlmPromptTemplate? PromptTemplate { get; } = promptTemplate;

        protected override string ChatClientModelPath => ModelPath;
        protected override LlmPromptTemplate? ChatClientPromptTemplate => PromptTemplate;
    }
}