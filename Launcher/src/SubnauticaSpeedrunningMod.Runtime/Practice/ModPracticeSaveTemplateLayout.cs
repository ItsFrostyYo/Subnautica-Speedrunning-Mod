using System.IO;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal struct ModPracticeSaveTemplateLayout
    {
        public ModPracticeSaveTemplateLayout(string templateRootPath, string selectedVariantDirectoryPath, bool selectedVariantRequired)
        {
            TemplateRootPath = templateRootPath ?? string.Empty;
            SelectedVariantDirectoryPath = selectedVariantDirectoryPath ?? string.Empty;
            SelectedVariantRequired = selectedVariantRequired;
        }

        public string TemplateRootPath { get; private set; }

        public string SelectedVariantDirectoryPath { get; private set; }

        public bool SelectedVariantRequired { get; private set; }

        public bool HasSelectedVariant
        {
            get { return !string.IsNullOrEmpty(SelectedVariantDirectoryPath) && Directory.Exists(SelectedVariantDirectoryPath); }
        }
    }
}
