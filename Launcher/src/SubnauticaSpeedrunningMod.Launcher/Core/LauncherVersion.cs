using System.Reflection;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Launcher;

internal static class LauncherVersion
{
    public static string DisplayVersion
    {
        get
        {
            var attribute = typeof(LauncherVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string value = attribute?.InformationalVersion ?? ModClientRelease.DisplayVersion;
            int plusIndex = value.IndexOf('+');
            return plusIndex >= 0 ? value.Substring(0, plusIndex) : value;
        }
    }
}
