using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using System;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

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

    internal async void ShowException(Exception? ex, string? optionalMessage = null)
    {
        var msg = optionalMessage ?? ex switch
        {
            COMException
                when ex.Message.Contains("the rpc server is unavailable", StringComparison.CurrentCultureIgnoreCase) =>
                    "The WCL is in an unstable state.\nRebooting the machine will restart the WCL.",
            _ => $"Error:\n{ex?.Message ?? string.Empty}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}"
        };

        var errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = msg,
            IsTextSelectionEnabled = true,
        };

        ContentDialog exceptionDialog = new()
        {
            Title = "Something went wrong",
            Content = errorText,
            PrimaryButtonText = "Copy to clipboard",
            XamlRoot = App.MainWindow.Content.XamlRoot,
            CloseButtonText = "Close",
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
        };

        if (SampleParams != null)
        {
            exceptionDialog.SecondaryButtonText = "Reload";
        }

        var result = await exceptionDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            CopyExceptionToClipboard(ex, optionalMessage);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            RootFrame.Navigate(typeof(Sample));
        }
    }

    public static void CopyExceptionToClipboard(Exception? ex, string? optionalMessage)
    {
        string exceptionDetails = string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n";

        if (ex != null)
        {
            exceptionDetails += GetExceptionDetails(ex);
        }

        DataPackage dataPackage = new DataPackage();
        dataPackage.SetText(exceptionDetails);
        Clipboard.SetContent(dataPackage);
    }

    private static string GetExceptionDetails(Exception ex)
    {
        var innerExceptionData = ex.InnerException == null ? "" :
            $"Inner Exception:\n{GetExceptionDetails(ex.InnerException)}";
        string details = $@"Message: {ex.Message}
StackTrace: {ex.StackTrace}
{innerExceptionData}";
        return details;
    }
}