using AIDevGallery.Models;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Pages;
public sealed partial class ModelOverviewPage : Page
{
    public ModelOverviewPage()
    {
        this.InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        InstalledModelsView.ItemsSource = App.ModelCache.Models;

        List<ModelType> rootModels = [.. ModelTypeHelpers.ModelGroupDetails.Keys];
    }

    private async void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await DownloadDialog.ShowAsync();
    }
}
