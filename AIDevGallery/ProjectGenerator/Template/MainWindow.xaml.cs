using System.Threading;
using Microsoft.UI.Xaml;
using $safeprojectname$.SharedCode;

namespace $safeprojectname$
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.RootFrame.Loaded += (sender, args) =>
            {
                var sampleLoadingCts = new CancellationTokenSource();

                var localModelDetails = new $sampleNavigationParameterName$(sampleLoadingCts.Token);

                RootFrame.Navigate(typeof($MainSamplePage$), localModelDetails);
            };
        }

        internal void ModelLoaded()
        {
            ProgressRingGrid.Visibility = Visibility.Collapsed;
        }
    }
}
