using System.Threading;

namespace $safeprojectname$.SharedCode
{
    internal class SampleNavigationParameters
    {
        public CancellationToken CancellationToken { get; private set; }
        public string ModelPath => $modelPath$;
        public HardwareAccelerator HardwareAccelerator => $modelHardwareAccelerator$;
        $promptTemplate$
        public SampleNavigationParameters(CancellationToken loadingCanceledToken)
        {
            CancellationToken = loadingCanceledToken;
        }
    }
}