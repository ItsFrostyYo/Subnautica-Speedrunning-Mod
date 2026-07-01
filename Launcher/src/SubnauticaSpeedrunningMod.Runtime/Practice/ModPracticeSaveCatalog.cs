using System;
using System.Collections.Generic;
using System.IO;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeSaveCatalog
    {
        public const string SaveFilesFolderName = "SaveFiles";
        public const string AnyPercentSurvivalGlitchedCategoryId = "Any% Survival Glitched";
        private const string HotbarLayoutPreviewFileName = "HotbarLayout.png";

        private static readonly ModPracticeSaveDefinition[] Definitions =
        {
            new ModPracticeSaveDefinition(
                AnyPercentSurvivalGlitchedCategoryId,
                "QEPQuickdeath",
                "QEP Quickdeath",
                timerEnabled: false,
                startsWithSuperSeaglide: false),
            new ModPracticeSaveDefinition(
                AnyPercentSurvivalGlitchedCategoryId,
                "CureClip",
                "Cure Clip",
                timerEnabled: false,
                startsWithSuperSeaglide: false),
            new ModPracticeSaveDefinition(
                AnyPercentSurvivalGlitchedCategoryId,
                "ASGEndSection",
                "End Section",
                timerEnabled: true,
                startsWithSuperSeaglide: false)
        };

        public static IList<ModPracticeSaveDefinition> GetPrimaryCategoryDefinitions()
        {
            List<ModPracticeSaveDefinition> results = new List<ModPracticeSaveDefinition>();
            for (int i = 0; i < Definitions.Length; i++)
            {
                ModPracticeSaveDefinition definition = Definitions[i];
                if (Directory.Exists(GetInstalledSavePath(definition)))
                {
                    results.Add(definition);
                }
            }

            return results;
        }

        public static string GetPrimaryCategoryDisplayName()
        {
            return AnyPercentSurvivalGlitchedCategoryId;
        }

        public static bool TryGetDefinition(string categoryId, string saveId, out ModPracticeSaveDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (string.Equals(Definitions[i].CategoryId, categoryId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Definitions[i].SaveId, saveId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = Definitions[i];
                    return true;
                }
            }

            definition = default(ModPracticeSaveDefinition);
            return false;
        }

        public static string GetInstalledSavePath(ModPracticeSaveDefinition definition)
        {
            string modRoot = PathLayout.GetModRoot();
            return Path.Combine(Path.Combine(Path.Combine(modRoot, SaveFilesFolderName), definition.CategoryId), definition.SaveId);
        }

        public static ModPracticeSaveTemplateLayout GetTemplateLayout(ModPracticeSaveDefinition definition, int layoutIndex)
        {
            string templateRootPath = GetInstalledSavePath(definition);
            string selectedVariantDirectoryPath = Path.Combine(
                templateRootPath,
                ModPracticeHotbarOptions.GetVariantDirectoryName(layoutIndex));

            bool hasAnyVariantDirectories = false;
            for (int i = 0; i < ModPracticeHotbarOptions.LayoutCountValue; i++)
            {
                string candidatePath = Path.Combine(
                    templateRootPath,
                    ModPracticeHotbarOptions.GetVariantDirectoryName(i));
                if (Directory.Exists(candidatePath))
                {
                    hasAnyVariantDirectories = true;
                    break;
                }
            }

            if (!hasAnyVariantDirectories)
            {
                selectedVariantDirectoryPath = string.Empty;
            }

            return new ModPracticeSaveTemplateLayout(
                templateRootPath,
                selectedVariantDirectoryPath,
                selectedVariantRequired: hasAnyVariantDirectories);
        }

        public static bool TryGetHotbarLayoutPreviewPath(int layoutIndex, out string previewPath)
        {
            previewPath = string.Empty;

            for (int i = 0; i < Definitions.Length; i++)
            {
                ModPracticeSaveTemplateLayout layout = GetTemplateLayout(Definitions[i], layoutIndex);
                if (!layout.HasSelectedVariant)
                {
                    continue;
                }

                string candidatePath = Path.Combine(layout.SelectedVariantDirectoryPath, HotbarLayoutPreviewFileName);
                if (File.Exists(candidatePath))
                {
                    previewPath = candidatePath;
                    return true;
                }
            }

            return false;
        }
    }

    internal struct ModPracticeSaveDefinition
    {
        public ModPracticeSaveDefinition(
            string categoryId,
            string saveId,
            string displayName,
            bool timerEnabled,
            bool startsWithSuperSeaglide)
        {
            CategoryId = categoryId ?? string.Empty;
            SaveId = saveId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            TimerEnabled = timerEnabled;
            StartsWithSuperSeaglide = startsWithSuperSeaglide;
        }

        public string CategoryId { get; private set; }

        public string SaveId { get; private set; }

        public string DisplayName { get; private set; }

        public bool TimerEnabled { get; private set; }

        public bool StartsWithSuperSeaglide { get; private set; }
    }
}
