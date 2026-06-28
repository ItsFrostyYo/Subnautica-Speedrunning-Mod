using System;
using System.IO;
using System.Reflection;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class PathLayout
    {
        public static string GetModRoot()
        {
            string explicitRoot = Environment.GetEnvironmentVariable("MOD_ROOT");
            if (!string.IsNullOrEmpty(explicitRoot))
            {
                return explicitRoot;
            }

            string runtimeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Directory.GetParent(runtimeDirectory).FullName;
        }

        public static string GetGameRoot(string modRoot)
        {
            string explicitRoot = Environment.GetEnvironmentVariable("MOD_GAME_ROOT");
            if (!string.IsNullOrEmpty(explicitRoot))
            {
                return explicitRoot;
            }

            return Directory.GetParent(modRoot).FullName;
        }
    }
}
