// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using Microsoft.ML.Tokenizers;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIDevGallery.Pages;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class WCROverview : Page
{
    ObservableCollection<TempWCRAPI> apis;
    public WCROverview()
    {
        this.InitializeComponent();
       apis = new();

        apis.Add(new TempWCRAPI() { Type = ModelType.PhiSilica, Name = "Phi Silica", Glyph = "\uE8F2", Description = "Phi Silica is a model that predicts the silica content of a sample based on the chemical composition of the sample." });
        apis.Add(new TempWCRAPI() { Type = ModelType.TextRecognitionOCR, Name = "Text Recognition (OCR)", Glyph = "\uE7A8", Description = "Text Recognition (OCR) is a model that recognizes text in an image." });
        apis.Add(new TempWCRAPI() { Type = ModelType.BackgroundRemover, Glyph = "\uED61", Name = "Background Remover", Description = "Background Remover is a model that removes the background from an image." });
        apis.Add(new TempWCRAPI() { Type = ModelType.ImageDescription, Glyph = "\uE7C5", Name = "Image Description", Description = "Image Description is a model that generates a description of an image." });
        apis.Add(new TempWCRAPI() { Type = ModelType.ImageScaler, Glyph = "\uE799", Name = "Image Scaler", Description = "Image Scaler is a model that scales an image." });
    }

    private void APIViewer_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {

    }
}

public class TempWCRAPI
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Glyph { get; set; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    internal ModelType Type { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}