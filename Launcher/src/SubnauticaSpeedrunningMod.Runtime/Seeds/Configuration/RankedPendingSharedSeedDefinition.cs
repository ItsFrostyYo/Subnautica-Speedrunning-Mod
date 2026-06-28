using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    [XmlRoot("ModPendingSharedSeed")]
    public sealed class ModPendingSharedSeedDefinition
    {
        [XmlElement("SeedId")]
        public string SeedId { get; set; }

        [XmlElement("SeedValue")]
        public string SeedValue { get; set; }

        [XmlElement("GameMode")]
        public string GameMode { get; set; }

        [XmlElement("Description")]
        public string Description { get; set; }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(SeedId))
            {
                SeedId = "Shared-Seed";
            }

            if (string.IsNullOrEmpty(GameMode))
            {
                GameMode = "Survival";
            }

            if (Description == null)
            {
                Description = string.Empty;
            }
        }
    }
}
