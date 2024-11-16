using System.Threading;

namespace $safeprojectname$.SharedCode
{
    internal class MultiModelSampleNavigationParameters
    {
        public CancellationToken CancellationToken { get; private set; }
        public string[] ModelPaths => $modelPath$;
        public HardwareAccelerator[] HardwareAccelerators => $modelHardwareAccelerator$;
        $promptTemplate$
        public MultiModelSampleNavigationParameters(CancellationToken loadingCanceledToken)
        {
            CancellationToken = loadingCanceledToken;
        }
    }
}