// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AIDevGallery.Controls;

internal sealed partial class HeaderCarousel : UserControl
{
    private readonly Random random = new();
    private readonly DispatcherTimer selectionTimer = new() { Interval = TimeSpan.FromMilliseconds(4000) };
    private readonly DispatcherTimer deselectionTimer = new() { Interval = TimeSpan.FromMilliseconds(3000) };
    private readonly List<int> numbers = [];
    private HeaderTile? selectedTile;
    private int currentIndex;

    public HeaderCarousel()
    {
        this.InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeToEvents();
        ResetAndShuffle();
        SelectNextTile();
        selectionTimer.Tick += SelectionTimer_Tick;
        selectionTimer.Start();
    }

    private void SubscribeToEvents()
    {
        foreach (HeaderTile tile in TilePanel.Children)
        {
            tile.PointerEntered += Tile_PointerEntered;
            tile.PointerExited += Tile_PointerExited;
            tile.Click += Tile_Click;
        }
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HeaderTile tile)
        {
            tile.PointerExited -= Tile_PointerExited;
            App.MainWindow.NavigateToPage(App.FindScenarioById(tile.SampleID));
        }
    }

    private void SelectionTimer_Tick(object? sender, object e)
    {
        SelectNextTile();
    }

    private async void SelectNextTile()
    {
        if (TilePanel.Children[GetNextUniqueRandom()] is HeaderTile tile)
        {
            selectedTile = tile;
            GeneralTransform transform = selectedTile.TransformToVisual(TilePanel);
            Point point = transform.TransformPoint(new Point(0, 0));
            scrollViewer.ChangeView(point.X - (scrollViewer.ActualWidth / 2) + (selectedTile.ActualSize.X / 2), null, null);
            await Task.Delay(500);
            SetTileVisuals();
            deselectionTimer.Tick += DeselectionTimer_Tick;
            deselectionTimer.Start();
        }
    }

    private void DeselectionTimer_Tick(object? sender, object e)
    {
        if (selectedTile != null)
        {
            selectedTile.IsSelected = false;
            selectedTile = null;
        }

        deselectionTimer.Stop();
    }

    private void ResetAndShuffle()
    {
        numbers.Clear();
        for (int i = 0; i <= TilePanel.Children.Count - 1; i++)
        {
            numbers.Add(i);
        }

        // Shuffle the list
        for (int i = numbers.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (numbers[j], numbers[i]) = (numbers[i], numbers[j]);
        }

        currentIndex = 0;
    }

    private int GetNextUniqueRandom()
    {
        if (currentIndex >= numbers.Count)
        {
            ResetAndShuffle();
        }

        return numbers[currentIndex++];
    }

    private void Tile_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ((HeaderTile)sender).IsSelected = false;
        selectionTimer.Start();
    }

    private async void Tile_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        selectedTile = (HeaderTile)sender;
        selectionTimer.Stop();
        deselectionTimer.Stop();

        bool isPrevious = false;
        foreach (HeaderTile t in TilePanel.Children)
        {
            if (t != selectedTile && t.IsSelected)
            {
                t.IsSelected = false;
                isPrevious = true;
            }
        }

        if (isPrevious)
        {
            await Task.Delay(1000);
        }

        // Wait for the animation of a potential other tile to finish
        await Task.Delay(500);
        SetTileVisuals();
    }

    private void SetTileVisuals()
    {
        if (selectedTile != null)
        {
            selectedTile.IsSelected = true;
            BackDropImage.ImageUrl = new Uri(selectedTile.ImageUrl);

            if (selectedTile.Foreground is LinearGradientBrush brush)
            {
                AnimateTitleGradient(brush);
            }
        }
    }

    private void AnimateTitleGradient(LinearGradientBrush brush)
    {
        //// Create a storyboard to hold the animations
        Storyboard storyboard = new();

        int i = 0;
        foreach (GradientStop stop in brush.GradientStops)
        {
            ColorAnimation colorAnimation1 = new()
            {
                To = stop.Color,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(colorAnimation1, AnimatedGradientBrush.GradientStops[i]);
            Storyboard.SetTargetProperty(colorAnimation1, "Color");
            storyboard.Children.Add(colorAnimation1);
            i++;
        }

        storyboard.Begin();
    }
}