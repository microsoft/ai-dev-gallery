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
                RootFrame.Navigate(typeof($MainSamplePage$), new SampleNavigationParameters());
            };
        }

        internal void ModelLoaded()
        {
            ProgressRingGrid.Visibility = Visibility.Collapsed;
        }
    }
}
