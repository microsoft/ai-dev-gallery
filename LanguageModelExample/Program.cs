using Microsoft.UI.Xaml;

namespace LanguageModelExample
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var app = new App();
            });
        }
    }
} 