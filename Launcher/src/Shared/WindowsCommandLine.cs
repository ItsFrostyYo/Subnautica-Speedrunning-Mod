using System;
using System.Collections.Generic;
using System.Text;

namespace SubnauticaSpeedrunningMod.Shared
{
    internal static class WindowsCommandLine
    {
        // Windows argv quoting, including embedded quotes and trailing backslashes.
        internal static string Quote(string argument)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char ch in argument ?? string.Empty)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                slashes = 0;
                result.Append(ch);
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        internal static string[] GameArguments(string[] arguments)
        {
            var result = new List<string>();
            for (int i = 0; i < arguments.Length; i++)
            {
                string arg = arguments[i];
                if (string.Equals(arg, "--handoff-pid", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
                if (string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--skip-update-check", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(arg);
            }
            return result.ToArray();
        }
    }
}
