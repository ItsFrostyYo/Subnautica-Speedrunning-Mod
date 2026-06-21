using System.Windows.Forms;

namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class LauncherAlert
{
    public static void ShowError(string title, string message)
    {
        try
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
        }
        catch
        {
            Console.Error.WriteLine(title);
            Console.Error.WriteLine(message);
        }
    }
}
