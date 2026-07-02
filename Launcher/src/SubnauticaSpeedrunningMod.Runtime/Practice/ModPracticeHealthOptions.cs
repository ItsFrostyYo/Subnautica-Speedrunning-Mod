using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeHealthOptions
    {
        private const string SelectedHealthIndexKey = "SubnauticaSpeedrunningMod.Practice.HealthIndex";
        private const int HealthCount = 2;

        public static int Count
        {
            get { return HealthCount; }
        }

        public static int GetSelectedHealthIndex()
        {
            return ClampIndex(PlayerPrefs.GetInt(SelectedHealthIndexKey, 0));
        }

        public static void SetSelectedHealthIndex(int healthIndex)
        {
            int clampedIndex = ClampIndex(healthIndex);
            PlayerPrefs.SetInt(SelectedHealthIndexKey, clampedIndex);
            PlayerPrefs.Save();
        }

        public static string GetDisplayName(int healthIndex)
        {
            return "Health " + (ClampIndex(healthIndex) + 1);
        }

        private static int ClampIndex(int healthIndex)
        {
            if (healthIndex < 0)
            {
                return 0;
            }

            if (healthIndex >= HealthCount)
            {
                return HealthCount - 1;
            }

            return healthIndex;
        }
    }
}
