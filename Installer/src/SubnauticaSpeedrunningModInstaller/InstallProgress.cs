namespace SubnauticaSpeedrunningModInstaller;

internal sealed class InstallProgress
{
    public InstallProgress(int percent, string message)
    {
        Percent = percent;
        Message = message;
    }

    public int Percent { get; }
    public string Message { get; }
}
