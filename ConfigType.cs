using SPTarkov.Server.Core.Models.Common;

namespace ArenaGear
{
    public class ModConfig
    {
        public bool addToTraders { get; set; }
        public bool addToBots { get; set; }
        public bool AddAsLoot { get; set; }
        public bool verboseLogging { get; set; }
    }

    public class ItemWeightsType
    {
        public MongoId copyFrom { get; set; }
        public float weightMult { get; set; }
    }
}