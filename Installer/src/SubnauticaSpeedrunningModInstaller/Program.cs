using System.Windows.Forms;

namespace SubnauticaSpeedrunningModInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += static (_, args) =>
        {
            MessageBox.Show(
                "The installer hit an unexpected error." + Environment.NewLine + Environment.NewLine + args.Exception.Message,
                "Installer Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        Application.Run(new MainForm());
    }
}
