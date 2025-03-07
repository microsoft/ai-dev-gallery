using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using System;

namespace $safeprojectname$;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.RootFrame.Loaded += (sender, args) =>
        {
            RootFrame.Navigate(typeof(Sample));
        };
    }

    internal void ModelLoaded()
    {
        ProgressRingGrid.Visibility = Visibility.Collapsed;
    }

    internal async void ShowException(Exception ex, string? optionalMessage = null)
    {
        var msg = optionalMessage ?? ex switch
        {
            COMException
                when ex.Message.Contains("the rpc server is unavailable", StringComparison.CurrentCultureIgnoreCase) =>
                    "The WCL is in an unstable state.\nRebooting the machine will restart the WCL.",
            _ => $"Error:\n{ex.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}"
        };
        ContentDialog exceptionDialog = new()
        {
            Title = "Error",
            Content = msg,
            PrimaryButtonText = "OK",
            SecondaryButtonText = "Reload Sample",
            XamlRoot = Content.XamlRoot,
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
        };

        var result = await exceptionDialog.ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            RootFrame.Navigate(typeof(Sample));
        }
    }
}