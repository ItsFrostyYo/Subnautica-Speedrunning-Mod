using System.Windows.Forms;

namespace SubnauticaSpeedrunningRanked.Updater;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        UpdateArguments options;
        try
        {
            options = UpdateArguments.Parse(args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Updater Arguments Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            return 2;
        }

        using var window = new UpdateProgressWindow(options);
        Application.Run(window);
        return window.ExitCode;
    }
}
