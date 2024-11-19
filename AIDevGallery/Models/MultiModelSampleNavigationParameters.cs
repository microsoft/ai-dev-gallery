// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.SharedCode;
using System.Threading;

namespace AIDevGallery.Models
{
    internal class MultiModelSampleNavigationParameters(
            string[] modelPaths,
            HardwareAccelerator[] hardwareAccelerators,
            LlmPromptTemplate?[] promptTemplates,
            CancellationToken loadingCanceledToken)
        : BaseSampleNavigationParameters(loadingCanceledToken)
    {
        public string[] ModelPaths { get; } = modelPaths;
        public HardwareAccelerator[] HardwareAccelerators { get; } = hardwareAccelerators;

        protected override string ChatClientModelPath => ModelPaths[0];
        protected override LlmPromptTemplate? ChatClientPromptTemplate => promptTemplates[0];
    }
}