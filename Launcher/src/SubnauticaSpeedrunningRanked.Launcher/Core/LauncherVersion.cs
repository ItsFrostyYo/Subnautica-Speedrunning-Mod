using System.Reflection;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class LauncherVersion
{
    public static string DisplayVersion
    {
        get
        {
            var attribute = typeof(LauncherVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string value = attribute?.InformationalVersion ?? RankedClientRelease.DisplayVersion;
            int plusIndex = value.IndexOf('+');
            return plusIndex >= 0 ? value.Substring(0, plusIndex) : value;
        }
    }
}
