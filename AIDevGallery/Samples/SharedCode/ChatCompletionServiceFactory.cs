// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.IO;

namespace AIDevGallery.Samples.SharedCode;
internal static class ChatCompletionServiceFactory
{
    public static (IChatCompletionService ChatCompletionService, Kernel SemanticKernel) GetSemanticKernelChatCompletionService(string modelPath)
    {
#pragma warning disable SKEXP0070
        IKernelBuilder builder = Kernel.CreateBuilder().AddOnnxRuntimeGenAIChatCompletion(Path.GetFileNameWithoutExtension(modelPath), modelPath);
#pragma warning restore SKEXP0070

        Kernel kernel = builder.Build();

        return (kernel.GetRequiredService<IChatCompletionService>(), kernel);
    }
}