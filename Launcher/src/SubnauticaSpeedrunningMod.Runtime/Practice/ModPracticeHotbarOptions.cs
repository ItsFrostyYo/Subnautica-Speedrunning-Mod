using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeHotbarOptions
    {
        private const string SelectedLayoutIndexKey = "SubnauticaSpeedrunningMod.Practice.HotbarLayoutIndex";
        private const int LayoutCount = 3;

        public static int LayoutCountValue
        {
            get { return LayoutCount; }
        }

        public static int GetSelectedLayoutIndex()
        {
            return ClampIndex(PlayerPrefs.GetInt(SelectedLayoutIndexKey, 0));
        }

        public static void SetSelectedLayoutIndex(int layoutIndex)
        {
            int clampedIndex = ClampIndex(layoutIndex);
            PlayerPrefs.SetInt(SelectedLayoutIndexKey, clampedIndex);
            PlayerPrefs.Save();
        }

        public static string GetDisplayName(int layoutIndex)
        {
            return "Hotbar " + (ClampIndex(layoutIndex) + 1);
        }

        public static string GetVariantDirectoryName(int layoutIndex)
        {
            return "objects (" + GetDisplayName(layoutIndex) + ")";
        }

        private static int ClampIndex(int layoutIndex)
        {
            if (layoutIndex < 0)
            {
                return 0;
            }

            if (layoutIndex >= LayoutCount)
            {
                return LayoutCount - 1;
            }

            return layoutIndex;
        }
    }
}
