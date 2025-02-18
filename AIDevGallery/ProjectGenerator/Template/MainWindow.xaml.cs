using Microsoft.UI.Xaml;

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
}