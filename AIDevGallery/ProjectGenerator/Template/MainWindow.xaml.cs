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
            _ => $"Error:\n{ex.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}"
        };
        var contenText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = msg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var copyTextButton = new Button
        {
            Content = new FontIcon
            {
                FontSize = 16,
                Glyph = "\uF0E3"
            },
            Tag = (ex, optionalMessage),
        };
        ToolTipService.SetToolTip(copyTextButton, "Copy exception info to clipboard");
        copyTextButton.Click += CopyText_Click;
        var copyTextStack = new StackPanel
        {
            Margin = new Thickness(8),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        copyTextStack.Children.Add(copyTextButton);
        var contentControl = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Width = 250,
            Height = 160
        };
        contentControl.Children.Add(contenText);
        contentControl.Children.Add(copyTextStack);
        ContentDialog exceptionDialog = new()
        {
            Title = "Error",
            Content = contentControl,
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

    private static void CopyText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is (Exception,string))
        {
            var (ex, optionalMessage) = ((Exception, string))button.Tag;
            CopyExceptionToClipboard(ex, optionalMessage);
        }
    }

    public static void CopyExceptionToClipboard(Exception ex, string optionalMessage)
    {
        string exceptionDetails = (string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n") +
            GetExceptionDetails(ex, optionalMessage);

        DataPackage dataPackage = new DataPackage();
        dataPackage.SetText(exceptionDetails);

        Clipboard.SetContent(dataPackage);
    }

    private static string GetExceptionDetails(Exception ex, string optionalMessage)
    {
        var innerExceptionData = ex.InnerException == null ? "" :
            $"Inner Exception:\n{GetExceptionDetails(ex.InnerException, optionalMessage)}";
        string details = $@"Message: {ex.Message}
StackTrace: {ex.StackTrace}
{innerExceptionData}";
        return details;
    }
}