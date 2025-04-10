// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode;

internal class ImageNet
{
    public static Prediction[] GetSoftmax(IEnumerable<float> output)
    {
        float sum = output.Sum(x => (float)Math.Exp(x));
        IEnumerable<float> softmax = output.Select(x => (float)Math.Round((float)Math.Exp(x) / sum, 4));

        return softmax.Select((x, i) => new Prediction { Label = ImageNetLabels.Labels[i], Confidence = x })
                           .OrderByDescending(x => x.Confidence)
                           .Take(5)
                           .ToArray();
    }

    public static void DisplayPredictions(IEnumerable<Prediction> predictions, StackPanel PredictionsStackPanel)
    {
        // Clear previous predictions
        PredictionsStackPanel.Children.Clear();

        // Set headers
        Grid headerRow = new()
        {
            Margin = new Thickness(0, 8, 0, 8)
        };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Create a TextBlock for the label
        TextBlock labelHeader = new()
        {
            Text = "Label",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelHeader, 0);

        // Create a TextBlock for the confidence
        TextBlock confidenceHeader = new()
        {
            Text = "Confidence",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(confidenceHeader, 2);

        headerRow.Children.Add(labelHeader);
        headerRow.Children.Add(confidenceHeader);

        PredictionsStackPanel.Children.Add(headerRow);

        foreach (var prediction in predictions)
        {
            // Create a Grid to hold the label and the progress bar
            Grid predictionGrid = new()
            {
                Margin = new Thickness(0, 8, 0, 8)
            };
            predictionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            predictionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            predictionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            // Create a TextBlock for the label
            TextBlock labelTextBlock = new()
            {
                Text = prediction.Label,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300
            };
            Grid.SetColumn(labelTextBlock, 0);

            // Create a TextBlock for the percentage
            TextBlock confidenceTextBlock = new()
            {
                Text = $"{prediction.Confidence * 100} %",
                FontSize = 14,
                Margin = new Thickness(5, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(confidenceTextBlock, 2);

            predictionGrid.Children.Add(labelTextBlock);
            predictionGrid.Children.Add(confidenceTextBlock);

            PredictionsStackPanel.Children.Add(predictionGrid);
        }
    }
}