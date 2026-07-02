using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeTimeOfDayOptions
    {
        private const string SelectedTimeOfDayIndexKey = "SubnauticaSpeedrunningMod.Practice.TimeOfDayIndex";
        private static readonly string[] Labels = { "Day", "Night" };

        public static int Count
        {
            get { return Labels.Length; }
        }

        public static int GetSelectedIndex()
        {
            return ClampIndex(PlayerPrefs.GetInt(SelectedTimeOfDayIndexKey, 0));
        }

        public static void SetSelectedIndex(int index)
        {
            int clampedIndex = ClampIndex(index);
            PlayerPrefs.SetInt(SelectedTimeOfDayIndexKey, clampedIndex);
            PlayerPrefs.Save();
        }

        public static string GetDisplayName(int index)
        {
            return Labels[ClampIndex(index)];
        }

        private static int ClampIndex(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            if (index >= Labels.Length)
            {
                return Labels.Length - 1;
            }

            return index;
        }
    }
}
