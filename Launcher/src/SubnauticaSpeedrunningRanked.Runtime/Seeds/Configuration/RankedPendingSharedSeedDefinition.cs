using System.Xml.Serialization;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    [XmlRoot("RankedPendingSharedSeed")]
    public sealed class RankedPendingSharedSeedDefinition
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
