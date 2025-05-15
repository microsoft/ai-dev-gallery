// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

internal class WhisperWrapper : IDisposable
{
    private readonly InferenceSession _inferenceSession;

    private bool _disposedValue;
    private bool _running;

    public static async Task<WhisperWrapper> CreateAsync(string modelPath, string preferedEp)
    {
        Microsoft.Windows.AI.MachineLearning.Infrastructure infrastructure = new();

        try
        {
            await infrastructure.DownloadPackagesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WARNING: Failed to download packages: {ex.Message}");
        }

        await infrastructure.RegisterExecutionProviderLibrariesAsync();

        using SessionOptions sessionOptions = new();
        sessionOptions.RegisterOrtExtensions();

        sessionOptions.AppendExecutionProviderFromEpName(preferedEp);

        var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{preferedEp}.onnx";

        if (!File.Exists(compiledModelPath))
        {
            using OrtModelCompilationOptions compilationOptions = new(sessionOptions);
            compilationOptions.SetInputModelPath(modelPath);
            compilationOptions.SetOutputModelPath(compiledModelPath);
            compilationOptions.CompileModel();
        }

        if (File.Exists(compiledModelPath))
        {
            modelPath = compiledModelPath;
        }

        InferenceSession inferenceSession = new InferenceSession(modelPath, sessionOptions);
        var whisper = new WhisperWrapper(inferenceSession);
        return whisper;
    }

    private WhisperWrapper(InferenceSession inferenceSession)
    {
        _inferenceSession = inferenceSession;
    }

    private static readonly int[] MinLengthMemoryCache = [0];
    private static readonly int[] MaxLengthMemoryCache = [448];
    private static readonly int[] NumBeamsMemoryCache = [2];
    private static readonly int[] NumReturnSequencesMemoryCache = [1];
    private static readonly float[] LengthPenaltyMemoryCache = [1.0f];
    private static readonly float[] RepetitionPenaltyMemoryCache = [1.0f];

    public Task<string> TranscribeAsync(byte[] pcmAudioData, string inputLanguage, TaskType taskType, bool includeTimestamps = true, CancellationToken cancellationToken = default)
    {
        // Convert byte array to float array and normalize
        float[] pcmAudioDataFloat = new float[pcmAudioData.Length / 2]; // PCM 16-bit, so divide length by 2
        for (int i = 0; i < pcmAudioDataFloat.Length; i++)
        {
            pcmAudioDataFloat[i] = BitConverter.ToInt16(pcmAudioData, i * 2) / 32768.0f; // Normalize to [-1, 1]
        }

        var audioTensor = new DenseTensor<float>(pcmAudioDataFloat, [1, pcmAudioDataFloat.Length]);
        var timestampsEnableTensor = new DenseTensor<int>(new[] { includeTimestamps ? 1 : 0 }, [1]);

        int task = (int)taskType;

        var langKey = LanguageCodes[inputLanguage];
        int langCode = GetLangId(langKey);
        var decoderInputIds = new int[] { 50258, langCode, task };
        var langAndModeTensor = new DenseTensor<int>(decoderInputIds, [1, 3]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("audio_pcm", audioTensor),
            NamedOnnxValue.CreateFromTensor("min_length", new DenseTensor<int>(MinLengthMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("max_length", new DenseTensor<int>(MaxLengthMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("num_beams", new DenseTensor<int>(NumBeamsMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("num_return_sequences", new DenseTensor<int>(NumReturnSequencesMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("length_penalty", new DenseTensor<float>(LengthPenaltyMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("repetition_penalty", new DenseTensor<float>(RepetitionPenaltyMemoryCache, [1])),
            NamedOnnxValue.CreateFromTensor("logits_processor", timestampsEnableTensor),
            NamedOnnxValue.CreateFromTensor("decoder_input_ids", langAndModeTensor)
        };

        return Task.Run(
            () =>
            {
                _running = true;
                using var results = _inferenceSession.Run(inputs);
                _running = false;

                if (cancellationToken.IsCancellationRequested)
                {
                    Dispose(true);
                    return string.Empty;
                }

                return results[0].AsTensor<string>().GetValue(0);
            },
            cancellationToken);
    }

    public static Dictionary<string, string> LanguageCodes { get; } = new()
    {
        { "English", "en" },
        { "Arabic", "ar" },
        { "Catalan", "ca" },
        { "Dutch", "nl" },
        { "French", "fr" },
        { "Hindi", "hi" },
        { "Indonesian", "id" },
        { "Italian", "it" },
        { "Japanese", "ja" },
        { "Korean", "ko" },
        { "Macedonian", "mk" },
        { "Mandarin", "zh" },
        { "Polish", "pl" },
        { "Portuguese", "pt" },
        { "Russian", "ru" },
        { "Serbian", "sr" },
        { "Spanish", "es" },
        { "Swedish", "sv" },
        { "Turkish", "tr" }
    };

    public static int GetLangId(string languageString)
    {
        Dictionary<string, int> langToId = new()
        {
            { "af", 50327 },
            { "am", 50334 },
            { "ar", 50272 },
            { "as", 50350 },
            { "az", 50304 },
            { "ba", 50355 },
            { "be", 50330 },
            { "bg", 50292 },
            { "bn", 50302 },
            { "bo", 50347 },
            { "br", 50309 },
            { "bs", 50315 },
            { "ca", 50270 },
            { "cs", 50283 },
            { "cy", 50297 },
            { "da", 50285 },
            { "de", 50261 },
            { "el", 50281 },
            { "en", 50259 },
            { "es", 50262 },
            { "et", 50307 },
            { "eu", 50310 },
            { "fa", 50300 },
            { "fi", 50277 },
            { "fo", 50338 },
            { "fr", 50265 },
            { "gl", 50319 },
            { "gu", 50333 },
            { "haw", 50352 },
            { "ha", 50354 },
            { "he", 50279 },
            { "hi", 50276 },
            { "hr", 50291 },
            { "ht", 50339 },
            { "hu", 50286 },
            { "hy", 50312 },
            { "id", 50275 },
            { "is", 50311 },
            { "it", 50274 },
            { "ja", 50266 },
            { "jw", 50356 },
            { "ka", 50329 },
            { "kk", 50316 },
            { "km", 50323 },
            { "kn", 50306 },
            { "ko", 50264 },
            { "la", 50294 },
            { "lb", 50345 },
            { "ln", 50353 },
            { "lo", 50336 },
            { "lt", 50293 },
            { "lv", 50301 },
            { "mg", 50349 },
            { "mi", 50295 },
            { "mk", 50308 },
            { "ml", 50296 },
            { "mn", 50314 },
            { "mr", 50320 },
            { "ms", 50282 },
            { "mt", 50343 },
            { "my", 50346 },
            { "ne", 50313 },
            { "nl", 50271 },
            { "nn", 50342 },
            { "no", 50288 },
            { "oc", 50328 },
            { "pa", 50321 },
            { "pl", 50269 },
            { "ps", 50340 },
            { "pt", 50267 },
            { "ro", 50284 },
            { "ru", 50263 },
            { "sa", 50344 },
            { "sd", 50332 },
            { "si", 50322 },
            { "sk", 50298 },
            { "sl", 50305 },
            { "sn", 50324 },
            { "so", 50326 },
            { "sq", 50317 },
            { "sr", 50303 },
            { "su", 50357 },
            { "sv", 50273 },
            { "sw", 50318 },
            { "ta", 50287 },
            { "te", 50299 },
            { "tg", 50331 },
            { "th", 50289 },
            { "tk", 50341 },
            { "tl", 50325 },
            { "tr", 50268 },
            { "tt", 50335 },
            { "ug", 50348 },
            { "uk", 50260 },
            { "ur", 50337 },
            { "uz", 50351 },
            { "vi", 50278 },
            { "xh", 50322 },
            { "yi", 50305 },
            { "yo", 50324 },
            { "zh", 50258 },
            { "zu", 50321 }
        };

        if (langToId.TryGetValue(languageString, out int langId))
        {
            return langId;
        }

        return 50259;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue && !_running)
        {
            if (disposing)
            {
                _inferenceSession.Dispose();
            }

            _disposedValue = true;
        }
    }

    public enum TaskType
    {
        Translate = 50358,
        Transcribe = 50359
    }
}