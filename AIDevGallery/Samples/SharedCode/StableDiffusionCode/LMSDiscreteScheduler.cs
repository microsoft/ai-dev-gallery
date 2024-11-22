// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MathNet.Numerics;
using Microsoft.ML.OnnxRuntime.Tensors;
using NumSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode.StableDiffusionCode;

internal class LMSDiscreteScheduler
{
    private readonly int _numTrainTimesteps;
    private readonly List<float> _alphasCumulativeProducts;

    public Tensor<float> Sigmas { get; set; } = null!;
    public List<int> Timesteps { get; set; }
    private readonly List<Tensor<float>> derivatives;
    public float InitNoiseSigma { get; set; }
    private readonly string _predictionType;

    public LMSDiscreteScheduler(int num_train_timesteps = 1000, float beta_start = 0.00085f, float beta_end = 0.012f, string beta_schedule = "scaled_linear", string prediction_type = "epsilon", List<float>? trained_betas = null)
    {
        _numTrainTimesteps = num_train_timesteps;
        _predictionType = prediction_type;
        derivatives = [];
        Timesteps = [];

        var alphas = new List<float>();
        var betas = new List<float>();

        if (trained_betas != null)
        {
            betas = trained_betas;
        }
        else if (beta_schedule == "linear")
        {
            betas = Enumerable.Range(0, num_train_timesteps)
                              .Select(i => beta_start + (beta_end - beta_start) * i / (num_train_timesteps - 1))
                              .ToList();
        }
        else if (beta_schedule == "scaled_linear")
        {
            var start = (float)Math.Sqrt(beta_start);
            var end = (float)Math.Sqrt(beta_end);
            betas = np.linspace(start, end, num_train_timesteps)
                     .ToArray<float>()
                     .Select(x => x * x)
                     .ToList();
        }
        else
        {
            // Fallback to a default value
            betas = Enumerable.Repeat(beta_start, num_train_timesteps).ToList();
        }

        alphas = betas.Select(beta => 1 - beta).ToList();

        _alphasCumulativeProducts = alphas.Select((alpha, i) => alphas.Take(i + 1).Aggregate((a, b) => a * b)).ToList();
        var sigmas = _alphasCumulativeProducts.Select(alpha_prod => Math.Sqrt((1 - alpha_prod) / alpha_prod)).Reverse().ToList();

        InitNoiseSigma = (float)sigmas.Max();
    }

    public static double[] Interpolate(double[] timesteps, double[] range, List<double> sigmas)
    {
        var result = np.zeros(timesteps.Length + 1);

        for (int i = 0; i < timesteps.Length; i++)
        {
            int index = Array.BinarySearch(range, timesteps[i]);

            if (index >= 0)
            {
                result[i] = sigmas[index];
            }
            else if (index == -1)
            {
                result[i] = sigmas[0];
            }
            else if (index == -range.Length - 1)
            {
                result[i] = sigmas[^1];
            }
            else
            {
                index = ~index;
                double t = (timesteps[i] - range[index - 1]) / (range[index] - range[index - 1]);
                result[i] = sigmas[index - 1] + t * (sigmas[index] - sigmas[index - 1]);
            }
        }

        result = np.add(result, 0.000f);

        return result.ToArray<double>();
    }

    public DenseTensor<float> ScaleInput(DenseTensor<float> sample, int timestep)
    {
        int stepIndex = Timesteps.IndexOf(timestep);
        var sigma = Sigmas[stepIndex];
        sigma = (float)Math.Sqrt(Math.Pow(sigma, 2) + 1);

        sample = TensorHelper.DivideTensorByFloat([.. sample], sigma, sample.Dimensions.ToArray());
        return sample;
    }

    public double GetLmsCoefficient(int order, int t, int currentOrder)
    {
        double LmsDerivative(double tau)
        {
            double prod = 1.0;
            for (int k = 0; k < order; k++)
            {
                if (currentOrder == k)
                {
                    continue;
                }

                prod *= (tau - Sigmas[t - k]) / (Sigmas[t - currentOrder] - Sigmas[t - k]);
            }

            return prod;
        }

        double integratedCoeff = Integrate.OnClosedInterval(LmsDerivative, Sigmas![t], Sigmas[t + 1], 1e-4);

        return integratedCoeff;
    }

    public int[] SetTimesteps(int num_inference_steps)
    {
        double start = 0;
        double stop = _numTrainTimesteps - 1;
        double[] timesteps = np.linspace(start, stop, num_inference_steps).ToArray<double>();

        Timesteps = timesteps.Select(x => (int)x).Reverse().ToList();

        var sigmas = _alphasCumulativeProducts.Select(alpha_prod => Math.Sqrt((1 - alpha_prod) / alpha_prod)).Reverse().ToList();
        var range = np.arange(0, (double)sigmas.Count).ToArray<double>();
        sigmas = [.. Interpolate(timesteps, range, sigmas)];
        Sigmas = new DenseTensor<float>(sigmas.Count);
        for (int i = 0; i < sigmas.Count; i++)
        {
            Sigmas[i] = (float)sigmas[i];
        }

        return [.. Timesteps];
    }

    public DenseTensor<float> Step(
        Tensor<float> modelOutput,
        int timestep,
        Tensor<float> sample,
        int order = 4)
    {
        int stepIndex = Timesteps.IndexOf(timestep);
        var sigma = Sigmas[stepIndex];

        Tensor<float> predOriginalSample;

        float[] predOriginalSampleArray = new float[modelOutput.Length];
        var modelOutPutArray = modelOutput.ToArray();
        var sampleArray = sample.ToArray();

        if (_predictionType == "epsilon")
        {
            for (int i = 0; i < modelOutPutArray.Length; i++)
            {
                predOriginalSampleArray[i] = sampleArray[i] - sigma * modelOutPutArray[i];
            }

            predOriginalSample = TensorHelper.CreateTensor(predOriginalSampleArray, modelOutput.Dimensions.ToArray());
        }
        else if (_predictionType == "v_prediction")
        {
            Console.WriteLine($"Warning: prediction_type '{_predictionType}' is not implemented yet. Skipping step.");
            return new DenseTensor<float>(new float[modelOutput.Length], modelOutput.Dimensions.ToArray()); // or any other appropriate value
        }
        else
        {
            Console.WriteLine($"Warning: Unsupported prediction_type '{_predictionType}'. Must be one of 'epsilon', or 'v_prediction'. Skipping step.");
            return new DenseTensor<float>(new float[modelOutput.Length], modelOutput.Dimensions.ToArray()); // or any other appropriate value
        }

        var derivativeItemsArray = new float[sample.Length];

        for (int i = 0; i < modelOutPutArray.Length; i++)
        {
            derivativeItemsArray[i] = (sampleArray[i] - predOriginalSampleArray[i]) / sigma;
        }

        var derivativeItems = TensorHelper.CreateTensor(derivativeItemsArray, sample.Dimensions.ToArray());

        derivatives?.Add(derivativeItems);

        if (derivatives?.Count > order)
        {
            derivatives?.RemoveAt(0);
        }

        order = Math.Min(stepIndex + 1, order);
        var lmsCoeffs = Enumerable.Range(0, order).Select(currOrder => GetLmsCoefficient(order, stepIndex, currOrder)).ToArray();

        var revDerivatives = Enumerable.Reverse(derivatives!).ToList();
        var lmsCoeffsAndDerivatives = lmsCoeffs.Zip(revDerivatives, (lmsCoeff, derivative) => (lmsCoeff, derivative)).ToArray();

        var lmsDerProduct = new Tensor<float>[derivatives!.Count];

        for (int m = 0; m < lmsCoeffsAndDerivatives.Length; m++)
        {
            var (lmsCoeff, derivative) = lmsCoeffsAndDerivatives[m];
            lmsDerProduct[m] = TensorHelper.MultipleTensorByFloat([.. derivative], (float)lmsCoeff, derivative.Dimensions.ToArray());
        }

        var sumTensor = TensorHelper.SumTensors(lmsDerProduct, [1, 4, 64, 64]);
        var prevSample = TensorHelper.AddTensors([.. sample], [.. sumTensor], sample.Dimensions.ToArray());

        return prevSample;
    }
}