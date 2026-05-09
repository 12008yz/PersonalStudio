using System.Windows.Forms;

namespace ScreenRecorder.VariantBSpike;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SpikeForm());
    }
}
