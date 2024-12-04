using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.UI.Xaml;

namespace $safeprojectname$
{
    public sealed partial class MainWindow : Window
    {
    // OgaHandle is responsible of cleaning up the library during shutdown
    private readonly OgaHandle _ogaHandle = new();

        public MainWindow()
        {
            this.InitializeComponent();
            this.RootFrame.Loaded += (sender, args) =>
            {
                RootFrame.Navigate(typeof($MainSamplePage$));
            };
        }

        internal void ModelLoaded()
        {
            ProgressRingGrid.Visibility = Visibility.Collapsed;
            (($MainSamplePage$)this.RootFrame.Content).Unloaded += (s, e) => _ogaHandle.Dispose();
        }
    }
}
