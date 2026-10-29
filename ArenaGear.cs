using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Reflection;
using Range = SemanticVersioning.Range;

namespace ArenaGear
{

    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.justnu.arenagear";
        public string Name { get; init; } = "JustNU-ArenaGear";
        public string Author { get; init; } = "JustNU";
        public List<string>? Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, Range>? ModDependencies { get; init; } = new()
        {
            { "com.wtt.commonlib", new Range("^3.0.0") }
        };
        public string? Url { get; init; }
        public string License { get; init; } = "Unlicense";
        public bool HasPrepatcher { get; init; } = false;
    }

    [Injectable(TypePriority = OnLoadOrder.Preload + 200)]
    public class ArenaGearServer(
        ModHelper modHelper,
        BotTable botTable,
        LocationTable locationTable,
        WTTServerCommonLib.WTTServerCommonLib wttCommon,
        ISptLogger<ArenaGearServer> logger) : IOnLoad
    {
        private ModConfig Config = new();
        private Dictionary<MongoId, ItemWeightsType> CustomItemWeights = new();

        public  Task OnLoadAsync(CancellationToken cancellationToken)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
            Config = modHelper.GetJsonDataFromFile<ModConfig>(System.IO.Path.Join(modPath), "config.jsonc");
            CustomItemWeights = modHelper.GetJsonDataFromFile<Dictionary<MongoId, ItemWeightsType>>(System.IO.Path.Join(modPath, "db"), "itemWeights.json");

            if (Config.verboseLogging) logger.Info($"ArenaGear Verbose Logging is on! Please turn it off unless you are trying to report an issue.");

            wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);

            if (Config.addToTraders) wttCommon.CustomAssortSchemeService.CreateCustomAssortSchemes(assembly);

            if (Config.AddAsLoot) AddLoot(cancellationToken);

            if (Config.addToBots) AddToBots(cancellationToken);

            if (Config.verboseLogging) logger.Info($"sanity check 1, on a scav: {botTable.Types["assault"].BotInventory.Equipment[SPTarkov.Server.Core.Models.Enums.EquipmentSlots.Backpack]["653277790e1cf7803b0812d4"]}.");
            if (Config.verboseLogging) logger.Info($"sanity check 2, on labs: {locationTable.Laboratory.StaticAmmo["Caliber762x51"].FirstOrDefault(entry => entry.Tpl == "67dd38986205be431305a0f7")}.");

            return Task.CompletedTask;
        }

        internal void AddLoot(CancellationToken cancellationToken)
        {
            foreach (var (locationId, locationData) in locationTable.GetDictionary())
            {
                foreach (var (customItemId, customWeightInfo) in CustomItemWeights)
                {
                    if (locationData?.StaticLoot != null)
                    {
                        locationData?.StaticLoot.AddTransformer(lazyloadedStaticLootData =>
                        {
                            foreach (var (containerId, containerDetails) in lazyloadedStaticLootData)
                            {
                                if (containerDetails.ItemDistribution.Any(entry => entry.Tpl == customWeightInfo.copyFrom))
                                {
                                    ItemDistribution itemToAdd = new();
                                    itemToAdd.Tpl = customItemId;
                                    var newWeight = containerDetails?.ItemDistribution?.FirstOrDefault(entry => entry.Tpl == customWeightInfo.copyFrom)?.RelativeProbability * customWeightInfo.weightMult;
                                    itemToAdd.RelativeProbability = (float)Math.Ceiling((decimal)newWeight);

                                    lazyloadedStaticLootData[containerId].ItemDistribution.AddItem(itemToAdd);

                                    if (Config.verboseLogging) logger.Info($"Added {customItemId} to container {containerId} on map {locationId} with weight off {itemToAdd.RelativeProbability}");
                                }
                            }

                            return lazyloadedStaticLootData;
                        });
                    }

                    if (locationData?.StaticAmmo != null)
                    {
                        foreach (var (calibre, calibreData) in locationData?.StaticAmmo)
                        {
                            if (calibreData.Any(entry => entry.Tpl == customWeightInfo.copyFrom))
                            {
                                StaticAmmoDetails itemToAdd = new();
                                itemToAdd.Tpl = customItemId;
                                var newWeight = calibreData.FirstOrDefault(entry => entry.Tpl == customWeightInfo.copyFrom)?.RelativeProbability * customWeightInfo.weightMult;
                                itemToAdd.RelativeProbability = (float)Math.Ceiling((decimal)newWeight);

                                locationData?.StaticAmmo[calibre].AddItem(itemToAdd);

                                if (Config.verboseLogging) logger.Info($"Added {customItemId} to {calibre} on map {locationId} with weight off {itemToAdd.RelativeProbability}");
                            }
                        }
                    }
                }
            }
        }

        internal void AddToBots(CancellationToken cancellationToken)
        {
            var bots = botTable.Types;

            if (CustomItemWeights == null) return;

            foreach (var (customItemId, customWeightInfo) in CustomItemWeights)
            {
                if (Config.verboseLogging) logger.Info($"Logging customItemId {customItemId}, {customWeightInfo.copyFrom}, {customWeightInfo.weightMult}");

                foreach (var (botType, data) in bots)
                {
                    var botEquipment = bots[botType]?.BotInventory.Equipment;
                    var botAmmo = bots[botType]?.BotInventory.Ammo;
                    var botItems = bots[botType]?.BotInventory.Items;
                    var botItemsVest = botItems?.TacticalVest;
                    var botItemsBackpack = botItems?.Backpack;
                    var botItemsPockets = botItems?.Pockets;
                    var botItemsSpecialLoot = botItems?.SpecialLoot;
                    var botItemsSecuredContainer = botItems?.SecuredContainer;
                    var botMods = bots[botType]?.BotInventory.Mods;

                    foreach (var (slot, slotData) in botEquipment)
                    {
                        if (botEquipment[slot].ContainsKey(customWeightInfo.copyFrom))
                        {
                            botEquipment[slot][customItemId] = Math.Ceiling(botEquipment[slot][customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                            if (Config.verboseLogging) logger.Info($"added {customItemId} to {slot} on {botType} with weight {Math.Ceiling(botEquipment[slot][customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                        }
                    }

                    foreach (var (calibre, calibreData) in botAmmo)
                    {
                        if (botAmmo[calibre].ContainsKey(customWeightInfo.copyFrom))
                        {
                            botAmmo[calibre][customItemId] = Math.Ceiling(botAmmo[calibre][customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                            if (Config.verboseLogging) logger.Info($"added {customItemId} to {calibre} on {botType} with weight {Math.Ceiling(botAmmo[calibre][customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                        }
                    }

                    if (botItemsVest.ContainsKey(customWeightInfo.copyFrom))
                    {
                        botItemsVest[customItemId] = Math.Ceiling(botItemsVest[customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                        if (Config.verboseLogging) logger.Info($"added {customItemId} to botItemsVest on {botType} with weight {Math.Ceiling(botItemsVest[customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                    }

                    if (botItemsBackpack.ContainsKey(customWeightInfo.copyFrom))
                    {
                        botItemsBackpack[customItemId] = Math.Ceiling(botItemsBackpack[customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                        if (Config.verboseLogging) logger.Info($"added {customItemId} to botItemsBackpack on {botType} with weight {Math.Ceiling(botItemsBackpack[customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                    }

                    if (botItemsPockets.ContainsKey(customWeightInfo.copyFrom))
                    {
                        botItemsPockets[customItemId] = Math.Ceiling(botItemsPockets[customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                        if (Config.verboseLogging) logger.Info($"added {customItemId} to botItemsPockets on {botType} with weight {Math.Ceiling(botItemsPockets[customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                    }

                    if (botItemsSpecialLoot.ContainsKey(customWeightInfo.copyFrom))
                    {
                        botItemsSpecialLoot[customItemId] = Math.Ceiling(botItemsSpecialLoot[customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                        if (Config.verboseLogging) logger.Info($"added {customItemId} to botItemsSpecialLoot on {botType} with weight {Math.Ceiling(botItemsSpecialLoot[customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                    }

                    if (botItemsSecuredContainer.ContainsKey(customWeightInfo.copyFrom))
                    {
                        botItemsSecuredContainer[customItemId] = Math.Ceiling(botItemsSecuredContainer[customWeightInfo.copyFrom] * customWeightInfo.weightMult);
                        if (Config.verboseLogging) logger.Info($"added {customItemId} to botItemsSecuredContainer on {botType} with weight {Math.Ceiling(botItemsSecuredContainer[customWeightInfo.copyFrom] * customWeightInfo.weightMult)}");
                    }
                       
                    foreach (var (modTpl, slotData) in botMods)
                    {
                        foreach (var (slot, slotItems) in slotData)
                        {
                            if (slotItems.Contains(customWeightInfo.copyFrom))
                            {
                                botMods[modTpl][slot].Add(customItemId);
                            }
                        }

                        if (modTpl == customWeightInfo.copyFrom)
                        {
                            botMods[customItemId] = botMods[modTpl];
                        }
                    }
                }
            }
        }
    }
}