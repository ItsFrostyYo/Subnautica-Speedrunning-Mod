using System;
using System.IO;
using System.Reflection;

namespace SubnauticaSpeedrunningRanked.Runtime
{
    internal static class PathLayout
    {
        public static string GetRankedRoot()
        {
            string explicitRoot = Environment.GetEnvironmentVariable("RANKED_ROOT");
            if (!string.IsNullOrEmpty(explicitRoot))
            {
                return explicitRoot;
            }

            string runtimeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Directory.GetParent(runtimeDirectory).FullName;
        }

        public static string GetGameRoot(string rankedRoot)
        {
            string explicitRoot = Environment.GetEnvironmentVariable("RANKED_GAME_ROOT");
            if (!string.IsNullOrEmpty(explicitRoot))
            {
                return explicitRoot;
            }

            return Directory.GetParent(rankedRoot).FullName;
        }
    }
}
