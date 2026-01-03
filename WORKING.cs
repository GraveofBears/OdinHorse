using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using CreatureManager;
using HarmonyLib;
using ItemManager;
using LocalizationManager;
using PieceManager;
using ServerSync;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace OdinHorse;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
public class OdinHorse : BaseUnityPlugin
{
    private const string ModName = "OdinHorse";
    private const string ModVersion = "1.3.8";
    private const string ModGUID = "Raelaziel.OdinHorse";

    #region Config

    private static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

    private static ConfigEntry<Toggle> serverConfigLocked = null!;

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

    private enum Toggle
    {
        On = 1,
        Off = 0
    }

    #endregion

    internal static Creature raeHorse;

    internal static ConfigEntry<bool> ServerConfigLocked = null!;
    internal static ConfigEntry<int> HorseRunningSpeed = null!;
    internal static ConfigEntry<int> HorseHealth = null!;
    internal static ConfigEntry<float> HorseStamina = null!;
    internal static ConfigEntry<float> HorseStaminaRegen = null!;

    internal static ConfigEntry<float> HorseStaminaRegenHungry = null!;
    //internal static ConfigEntry<int> HorseFeedingTime = null!;
    //internal static ConfigEntry<int> HorseTamingTime = null!;

    internal static ConfigEntry<int> HorseOffspringHealth = null!;
    internal static ConfigEntry<int> HorseOffspringGrowupTime = null!;
    internal static ConfigEntry<float> HorseOffspringMeatDropChance = null!;
    internal static ConfigEntry<int> HorseOffspringMeatDropMinimum = null!;
    internal static ConfigEntry<int> HorseOffspringMeatDropMaximum = null!;
    internal static ConfigEntry<float> HorseOffspringHideDropChance = null!;
    internal static ConfigEntry<int> HorseOffspringHideDropMinimum = null!;
    internal static ConfigEntry<int> HorseOffspringHideDropMaximum = null!;
    private static List<Material> horseMaterials;
    private static AssetBundle horseAssetBundle;

    public void Awake()
    {
        Localizer.Load();
        LoadAssetBundle();

        serverConfigLocked = config("General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
        configSync.AddLockingConfigEntry(serverConfigLocked);

        HorseRunningSpeed = config("Horse", "Running Speed", 10, new ConfigDescription("Declare running speed for Horse"));
        HorseHealth = config("Horse", "Health", 200, new ConfigDescription("Declare health points for Horse"));
        HorseStamina = config("Horse", "Stamina", 400f, new ConfigDescription("Declare stamina for Horse"));
        HorseStaminaRegen = config("Horse", "Stamina Regen", 2f, new ConfigDescription("Declare stamina regen for Horse"));
        HorseStaminaRegenHungry = config("Horse", "Stamina Regen Hungry", 1f, new ConfigDescription("Declare stamina regen when hungry for Horse"));
        //HorseFeedingTime = config("Horse", "Fed duration", 600, new ConfigDescription("Declare fed duration for Horse"));
        //HorseTamingTime = config("Horse", "Taming time", 1800, new ConfigDescription("Declare taming time needed to tame the Horse"));

        HorseOffspringHealth = config("Horse Offspring", "Health", 60, new ConfigDescription("Declare health points for Horse Offspring"));
        HorseOffspringGrowupTime = config("Horse Offspring", "Grow-up time", 2000, new ConfigDescription("Declare growup time needed to convert offspring into Horse. Time in seconds."));
        HorseOffspringMeatDropChance = config("Horse Offspring", "Meat Drop Chance", 1.00f, new ConfigDescription("Declare drop chance for Horse Meat from offspring"));
        HorseOffspringMeatDropMinimum = config("Horse Offspring", "Meat Amount Min", 1, new ConfigDescription("Declare minimum amount of Horse Meat to drop from offspring"));
        HorseOffspringMeatDropMaximum = config("Horse Offspring", "Meat Amount Max", 2, new ConfigDescription("Declare maximum amount of Horse Meat to drop from offspring"));
        HorseOffspringHideDropChance = config("Horse Offspring", "Hide Drop Chance", 0.33f, new ConfigDescription("Declare drop chance for Horse Hide from offspring"));
        HorseOffspringHideDropMinimum = config("Horse Offspring", "Hide Amount Min", 1, new ConfigDescription("Declare minimum amount of Horse Hide to drop from offspring"));
        HorseOffspringHideDropMaximum = config("Horse Offspring", "Hide Amount Max", 1, new ConfigDescription("Declare maximum amount of Horse Hide to drop from offspring"));

        #region Items

        Item raeHorseMeat = new Item("horsesets", "rae_HorseMeat");
        Item raeOdinHorseHide = new Item("horsesets", "rae_HorseHide");
        Item raeOdinHorseTrophy = new Item("horsesets", "rae_OdinHorse_Trophy");

        Item raeHorseSaddle = new Item("horsesets", "rae_SaddleHorse");
        raeHorseSaddle.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseSaddle.MaximumRequiredStationLevel = 5; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSaddle.RequiredItems.Add("FineWood", 20);
        raeHorseSaddle.RequiredItems.Add("Bronze", 10);
        raeHorseSaddle.RequiredItems.Add("rae_HorseHide", 30);
        raeHorseSaddle.CraftAmount = 1;
/*
        Item rae_iron_HorseArmor = new Item("horsesets", "rae_iron_HorseArmor");
        rae_iron_HorseArmor.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        rae_iron_HorseArmor.MaximumRequiredStationLevel = 5; // Limits the crafting station level required to upgrade or repair the item to 5
        rae_iron_HorseArmor.RequiredItems.Add("Iron", 20);
        rae_iron_HorseArmor.RequiredItems.Add("Bronze", 10);
        rae_iron_HorseArmor.RequiredItems.Add("rae_HorseHide", 30);
        rae_iron_HorseArmor.CraftAmount = 1;
*/
        Item raeHorseSticks = new Item("horsesets", "rae_HorseSticks");
        raeHorseSticks.Crafting.Add(ItemManager.CraftingTable.Cauldron, 1);
        raeHorseSticks.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSticks.RequiredItems.Add("rae_HorseMeat", 3);
        raeHorseSticks.RequiredItems.Add("Coal", 1);
        raeHorseSticks.RequiredItems.Add("Dandelion", 2);
        raeHorseSticks.CraftAmount = 2;

        Item raeHorseSoup = new Item("horsesets", "rae_HorseSoup");
        raeHorseSoup.Crafting.Add(ItemManager.CraftingTable.Cauldron, 1);
        raeHorseSoup.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSoup.RequiredItems.Add("rae_HorseMeat", 2);
        raeHorseSoup.RequiredItems.Add("Carrot", 2);
        raeHorseSoup.RequiredItems.Add("Dandelion", 3);
        raeHorseSoup.CraftAmount = 1;

        Item raeHorseSkewer = new Item("horsesets", "rae_HorseMeatSkewer");
        raeHorseSkewer.Crafting.Add(ItemManager.CraftingTable.Cauldron, 2);
        raeHorseSkewer.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSkewer.RequiredItems.Add("rae_HorseMeat", 3);
        raeHorseSkewer.RequiredItems.Add("Mushroom", 2);
        raeHorseSkewer.RequiredItems.Add("NeckTail", 1);
        raeHorseSkewer.CraftAmount = 2;

        Item raeHorseaker = new Item("horsesets", "rae_Horseaker");
        raeHorseaker.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        raeHorseaker.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseaker.RequiredItems.Add("Iron", 30);
        raeHorseaker.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseaker.RequiredItems.Add("ElderBark", 35);
        raeHorseaker.RequiredUpgradeItems.Add("Iron", 5);
        raeHorseaker.RequiredUpgradeItems.Add("ElderBark", 10);
        raeHorseaker.CraftAmount = 1;

        Item raeHorseHelmet = new Item("horsesets", "rae_OdinHorse_Helmet");
        raeHorseHelmet.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseHelmet.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseHelmet.RequiredItems.Add("Tin", 10);
        raeHorseHelmet.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseHelmet.RequiredItems.Add("rae_HorseHide", 5);
        raeHorseHelmet.RequiredUpgradeItems.Add("rae_HorseHide", 4);
        raeHorseHelmet.RequiredUpgradeItems.Add("Tin", 2);
        raeHorseHelmet.CraftAmount = 1;

        Item raeHorseCape = new Item("horsesets", "rae_CapeHorseHide");
        raeHorseCape.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseCape.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseCape.RequiredItems.Add("rae_HorseHide", 10);
        raeHorseCape.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseCape.RequiredItems.Add("Tin", 2);
        raeHorseCape.RequiredUpgradeItems.Add("rae_HorseHide", 10);
        raeHorseCape.RequiredUpgradeItems.Add("Tin", 2);
        raeHorseCape.CraftAmount = 1;

        Item ArmorHorseClothHelmet_T1 = new Item("horsesets", "ArmorHorseClothHelmet_T1");
        ArmorHorseClothHelmet_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothHelmet_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothHelmet_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothHelmet_T1.RequiredItems.Add("LeatherScraps", 6);
        ArmorHorseClothHelmet_T1.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        ArmorHorseClothHelmet_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothHelmet_T1.RequiredUpgradeItems.Add("LeatherScraps", 3);
        ArmorHorseClothHelmet_T1.CraftAmount = 1;

        Item ArmorHorseClothHelmet_T2 = new Item("horsesets", "ArmorHorseClothHelmet_T2");
        ArmorHorseClothHelmet_T2.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        ArmorHorseClothHelmet_T2.MaximumRequiredStationLevel = 10;
        ArmorHorseClothHelmet_T2.RequiredItems.Add("rae_HorseHide", 6);
        ArmorHorseClothHelmet_T2.RequiredItems.Add("Iron", 15);
        ArmorHorseClothHelmet_T2.RequiredItems.Add("LeatherScraps", 4);
        ArmorHorseClothHelmet_T2.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        ArmorHorseClothHelmet_T2.RequiredUpgradeItems.Add("rae_HorseHide", 3);
        ArmorHorseClothHelmet_T2.RequiredUpgradeItems.Add("Iron", 5);
        ArmorHorseClothHelmet_T2.RequiredUpgradeItems.Add("LeatherScraps", 2);
        ArmorHorseClothHelmet_T2.CraftAmount = 1;

        Item ArmorHorseClothChest_T1 = new Item("horsesets", "ArmorHorseClothChest_T1");
        ArmorHorseClothChest_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothChest_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothChest_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothChest_T1.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothChest_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothChest_T1.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothChest_T1.CraftAmount = 1;

        Item ArmorHorseClothChest_T2 = new Item("horsesets", "ArmorHorseClothChest_T2");
        ArmorHorseClothChest_T2.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        ArmorHorseClothChest_T2.MaximumRequiredStationLevel = 10;
        ArmorHorseClothChest_T2.RequiredItems.Add("rae_HorseHide", 6);
        ArmorHorseClothChest_T2.RequiredItems.Add("Iron", 20);
        ArmorHorseClothChest_T2.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothChest_T2.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothChest_T2.RequiredUpgradeItems.Add("Iron", 5);
        ArmorHorseClothChest_T2.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothChest_T2.CraftAmount = 1;

        Item ArmorHorseClothLegs_T1 = new Item("horsesets", "ArmorHorseClothLegs_T1");
        ArmorHorseClothLegs_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothLegs_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothLegs_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothLegs_T1.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothLegs_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothLegs_T1.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothLegs_T1.CraftAmount = 1;

        Item ArmorHorseClothLegs_T2 = new Item("horsesets", "ArmorHorseClothLegs_T2");
        ArmorHorseClothLegs_T2.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        ArmorHorseClothLegs_T2.MaximumRequiredStationLevel = 10;
        ArmorHorseClothLegs_T2.RequiredItems.Add("rae_HorseHide", 6);
        ArmorHorseClothLegs_T2.RequiredItems.Add("Iron", 20);
        ArmorHorseClothLegs_T2.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothLegs_T2.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothLegs_T2.RequiredUpgradeItems.Add("Iron", 5);
        ArmorHorseClothLegs_T2.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothLegs_T2.CraftAmount = 1;

        #endregion

        #region Build Pieces

        BuildPiece raeHorseRug = new("horsesets", "rae_OdinHorse_Rug"); // Note: If you wish to use the default "assets" folder for your assets, you can omit it!
        raeHorseRug.RequiredItems.Add("rae_HorseHide", 20, true);
        raeHorseRug.Category.Set(BuildPieceCategory.Furniture);
        raeHorseRug.Crafting.Set(PieceManager.CraftingTable.Workbench);

        BuildPiece raeHorseChair = new("horsesets", "rae_HorseChair"); // Note: If you wish to use the default "assets" folder for your assets, you can omit it!
        raeHorseChair.RequiredItems.Add("rae_HorseHide", 2, true);
        raeHorseChair.RequiredItems.Add("FineWood", 3, true);
        raeHorseChair.Category.Set(BuildPieceCategory.Furniture);
        raeHorseChair.Crafting.Set(PieceManager.CraftingTable.Workbench);

        BuildPiece raeHorseCart = new("horsesets", "rae_HorseCart"); // Note: If you wish to use the default "assets" folder for your assets, you can omit it!
        raeHorseCart.RequiredItems.Add("BronzeNails", 60, true);
        raeHorseCart.RequiredItems.Add("FineWood", 40, true);
        raeHorseCart.Category.Set(BuildPieceCategory.Misc);
        raeHorseCart.Crafting.Set(PieceManager.CraftingTable.Workbench);

        raeHorseCart.Prefab.GetComponent<Vagon>().m_breakForce = float.MaxValue;

        #endregion

        #region GameObjects Register

        /* OBJECTS */
        GameObject raeHorse_bite_attack = ItemManager.PrefabManager.RegisterPrefab("horsesets", "horse_bite_attack");
        GameObject raeOffspringNormal = ItemManager.PrefabManager.RegisterPrefab("horsesets", "rae_Offspring_Normal");
        GameObject raeOffspringNormal_Ragdoll = ItemManager.PrefabManager.RegisterPrefab("horsesets", "rae_Offspring_Normal_ragdoll");
        GameObject rae_OdinHorse_Ragdoll = ItemManager.PrefabManager.RegisterPrefab("horsesets", "rae_OdinHorse_ragdoll");

        #endregion

        #region Creatures

        raeHorse = new("horsesets", "rae_OdinHorse")
        {
            ConfigurationEnabled = true,
            TamingTime = 1600,
            FedDuration = 300,
            Biome = Heightmap.Biome.Meadows,
            CanSpawn = true,
            CanBeTamed = true,
            CanHaveStars = true,
            FoodItems = "Blueberries, Carrot, Cloudberry, Barley",
            SpawnChance = 15,
            GroupSize = new Range(1, 2),
            CheckSpawnInterval = 2000,
            SpecificSpawnTime = SpawnTime.Day,
            SpecificSpawnArea = CreatureManager.SpawnArea.Center,
            RequiredWeather = Weather.ClearSkies,
            AttackImmediately = false,
            ForestSpawn = Forest.Both,
            CreatureFaction = Character.Faction.ForestMonsters,
            Maximum = 1
        };
        raeHorse.Drops["rae_HorseMeat"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_HorseMeat"].DropChance = 100f;
        raeHorse.Drops["rae_HorseHide"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_HorseHide"].DropChance = 100f;
        raeHorse.Drops["rae_OdinHorse_Trophy"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_OdinHorse_Trophy"].DropChance = 10f;

        raeHorse.Prefab.GetComponent<Humanoid>().m_runSpeed = HorseRunningSpeed.Value;
        raeHorse.Prefab.GetComponent<Humanoid>().m_health = HorseHealth.Value;
        //raeHorse.Prefab.GetComponent<Tameable>().m_fedDuration = HorseFeedingTime.Value;
        //raeHorse.Prefab.GetComponent<Tameable>().m_tamingTime = HorseTamingTime.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_maxStamina = HorseStamina.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_staminaRegen = HorseStaminaRegen.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_staminaRegenHungry = HorseStaminaRegenHungry.Value;


        raeOffspringNormal.GetComponent<CharacterDrop>().m_drops.Add(new CharacterDrop.Drop
        {
            m_prefab = raeOdinHorseHide.Prefab,
            m_amountMin = HorseOffspringHideDropMinimum.Value,
            m_amountMax = HorseOffspringHideDropMaximum.Value,
            m_chance = HorseOffspringHideDropChance.Value,
            m_levelMultiplier = true,
            m_onePerPlayer = false
        });

        raeOffspringNormal.GetComponent<CharacterDrop>().m_drops.Add(new CharacterDrop.Drop
        {
            m_prefab = raeHorseMeat.Prefab,
            m_amountMin = HorseOffspringMeatDropMinimum.Value,
            m_amountMax = HorseOffspringMeatDropMaximum.Value,
            m_chance = HorseOffspringMeatDropChance.Value,
            m_levelMultiplier = true,
            m_onePerPlayer = false
        });

        raeOffspringNormal.GetComponent<Humanoid>().m_health = HorseOffspringHealth.Value;
        raeOffspringNormal.GetComponent<Growup>().m_growTime = HorseOffspringGrowupTime.Value;

        #endregion

        CustomMapPins.RegisterCustomPin(raeHorse.Prefab, "$horse_odin", raeOdinHorseTrophy.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons[0]);
        CustomMapPins_Cart.RegisterCustomPinGeneric(raeHorseCart.Prefab, "$pin_cart", raeHorseCart.Prefab.GetComponent<Piece>().m_icon);

        // Patch
        Assembly assembly = Assembly.GetExecutingAssembly();
        Harmony harmony = new(ModGUID);
        harmony.PatchAll(assembly);
    }

    private void LoadAssetBundle()
    {
        string resourcePath = "OdinHorse.assets.horsesets"; // Adjust this to match your embedded resource path
        using Stream assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
        if (assetStream == null)
        {
            Logger.LogError($"Failed to load embedded resource: {resourcePath}");
            return;
        }

        horseAssetBundle = AssetBundle.LoadFromStream(assetStream);
        if (horseAssetBundle == null)
        {
            Logger.LogError("Failed to load AssetBundle from stream.");
            return;
        }

        // Load materials
        horseMaterials = new List<Material>
    {
        horseAssetBundle.LoadAsset<Material>("Horse_color_Black_Tobiano_pinto"),
        horseAssetBundle.LoadAsset<Material>("Horse_color_brown"),
        horseAssetBundle.LoadAsset<Material>("Horse_color_grays"),
        horseAssetBundle.LoadAsset<Material>("Horse_color_palomino"),
        horseAssetBundle.LoadAsset<Material>("Horse_color_white"),
        horseAssetBundle.LoadAsset<Material>("Horse_color_dark_grey"),

    };

        Logger.LogInfo("Successfully loaded horse materials.");
    }
    private void RegisterHorseWithCreatureManager()
    {
        Creature raeHorse = new("horsesets", "rae_OdinHorse")
        {
            ConfigurationEnabled = true,
            TamingTime = 1600,
            FedDuration = 300,
            Biome = Heightmap.Biome.Meadows,
            CanSpawn = true,
            CanBeTamed = true,
            CanHaveStars = true,
            FoodItems = "Blueberries, Carrot, Cloudberry, Barley",
            SpawnChance = 15,
            GroupSize = new Range(1, 2),
            CheckSpawnInterval = 2000,
            SpecificSpawnTime = SpawnTime.Day,
            SpecificSpawnArea = CreatureManager.SpawnArea.Center,
            RequiredWeather = Weather.ClearSkies,
            AttackImmediately = false,
            ForestSpawn = Forest.Both,
            CreatureFaction = Character.Faction.ForestMonsters,
            Maximum = 1
        };

        raeHorse.Drops["rae_HorseMeat"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_HorseMeat"].DropChance = 100f;
        raeHorse.Drops["rae_HorseHide"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_HorseHide"].DropChance = 100f;
        raeHorse.Drops["rae_OdinHorse_Trophy"].Amount = new Range(1, 2);
        raeHorse.Drops["rae_OdinHorse_Trophy"].DropChance = 10f;

        
    }

    private void OnPrefabPostLoad(GameObject prefab)
    {
        SkinnedMeshRenderer renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null) return;

        // Assign default material to prevent null references
        renderer.material = horseMaterials[0];
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Awake))]
    public static class CharacterAwakePatch
    {
        static void Postfix(Character __instance)
        {
            if (__instance.name != "rae_OdinHorse(Clone)") return;

            SkinnedMeshRenderer renderer = __instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null) return;

            ZNetView znv = __instance.GetComponent<ZNetView>();
            if (znv == null || !znv.IsValid()) return;

            // Check if material index is already set
            int materialIndex = znv.GetZDO().GetInt("HorseMaterial", -1);
            if (materialIndex == -1)
            {
                // Choose random material and save
                materialIndex = UnityEngine.Random.Range(0, horseMaterials.Count);
                znv.GetZDO().Set("HorseMaterial", materialIndex);
            }

            renderer.material = horseMaterials[materialIndex]; // Apply material
        }
    }



    #region Patch_Cart_Attach

    /// <summary>
    /// An enum describing what is getting attached to the cart
    /// </summary>
    public enum HorseBeasts
    {
        player,
        horse
    }

    /// <summary>
    /// Parses a character into a Beasts
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public static HorseBeasts ParseCharacterType(Character c)
    {
        if (c.IsPlayer())
        {
            return HorseBeasts.player;
        }

        if (c.m_nview.IsValid())
        {
            switch (ZNetScene.instance.GetPrefab(c.m_nview.GetZDO().GetPrefab()).name)
            {
                case "rae_OdinHorse":
                    return HorseBeasts.horse;
                default:
                    return HorseBeasts.horse;
            }
        }
        else
        {
            return HorseBeasts.horse;
        }
    }

    /// <summary>
    /// Different sized characters require different attachment offsets for the cart. 
    /// This will return the appropriate offset.
    /// </summary>
    /// <param name="c">to be attached to the cart</param>
    /// <returns>vector of where the cart should attach</returns>
    public static Vector3 GetCartOffsetVectorForCharacter(Character c)
    {
        if (c)
        {
            return new Vector3(0f, 0.8f, 0f - c.GetRadius());
        }

        return new Vector3(0f, 0.8f, 0f);
    }

    /// <summary>
    /// Allows the types of animals attached to to be configurable
    /// </summary>
    /// <param name="c"></param>
    /// <returns>if cart can be attached to character type</returns>
    public static bool IsAttachableCharacter(Character c)
    {
        switch (ParseCharacterType(c))
        {
            case HorseBeasts.player:
                return false;
            case HorseBeasts.horse:
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Different character types should be different distances to the cart for it to attach.
    /// </summary>
    /// <param name="c"></param>
    /// <returns>the appropriate attach/detach distance for the provided character</returns>
    public static float GetCartDetachDistance(Character c)
    {
        if (c)
        {
            if (c.IsPlayer())
            {
                return 2f;
            }
            else
            {
                return c.GetRadius() * 3f;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Searches nearby animals and finds the closest one to the cart that could be attached.
    /// </summary>
    /// <param name="cart"></param>
    /// <returns>Closest character to the cart that can attach to it, null if no character available</returns>
    static Character FindClosestAttachableAnimal(Vagon cart)
    {
        if (cart == null || cart.m_attachPoint == null) return null;

        Transform attachPoint = cart.m_attachPoint;
        Character closest_animal = null;
        float closest_distance = float.MaxValue;

        if (!cart.m_attachPoint)
        {
            return null;
        }

        foreach (Character currentCharacter in Character.GetAllCharacters())
        {
            if (currentCharacter == null || !currentCharacter.m_nview.IsValid() || currentCharacter.IsDead()) continue;
            if (currentCharacter)
            {
                if (!currentCharacter.IsPlayer() && currentCharacter.IsTamed() && IsAttachableCharacter(currentCharacter))
                {
                    Vector3 cartOffset = GetCartOffsetVectorForCharacter(currentCharacter);
                    Vector3 animalPosition = currentCharacter.transform.position;

                    float distance = Vector3.Distance(animalPosition + cartOffset, attachPoint.position);
                    float detachDistance = GetCartDetachDistance(currentCharacter);
                    if (distance < detachDistance && distance < closest_distance)
                    {
                        closest_animal = currentCharacter;
                        closest_distance = distance;
                    }
                }
            }
        }

        return closest_animal;
    }


    /// <summary>
    /// Helper method to access the character currently attached to a cart
    /// </summary>
    /// <param name="cart"></param>
    /// <returns>Character currently attached</returns>
    static Character AttachedCharacter(Vagon cart)
    {
        if (cart && cart.IsAttached())
        {
            return cart.m_attachJoin.connectedBody.gameObject.GetComponent<Character>();
        }

        return null;
    }

    /// <summary>
    /// Logs the contents of a given cart to the debug logger. 
    /// Used during debugging to easily differentiate between carts.
    /// </summary>
    /// <param name="cart"></param>
    static void LogCartContents(Vagon cart)
    {
        Container c = cart.m_container;
        foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
        {
        }
    }

    /// <summary>
    /// This method is similar to Vagon.AttachTo except we don't call DetachAll as the first operation.
    /// </summary>
    /// <param name="attachTarget"></param>
    /// <param name="cart"></param>
    static void AttachCartTo(Character attachTarget, Vagon cart)
    {
        cart.m_attachOffset = GetCartOffsetVectorForCharacter(attachTarget);

        cart.m_attachJoin = cart.gameObject.AddComponent<ConfigurableJoint>();
        ((Joint)cart.m_attachJoin).autoConfigureConnectedAnchor = false;
        ((Joint)cart.m_attachJoin).anchor = cart.m_attachPoint.localPosition;
        ((Joint)cart.m_attachJoin).connectedAnchor = cart.m_attachOffset;
        ((Joint)cart.m_attachJoin).breakForce = cart.m_breakForce;
        cart.m_attachJoin.xMotion = ((ConfigurableJointMotion)1);
        cart.m_attachJoin.yMotion = ((ConfigurableJointMotion)1);
        cart.m_attachJoin.zMotion = ((ConfigurableJointMotion)1);
        SoftJointLimit linearLimit = default(SoftJointLimit);
        linearLimit.limit = 0.001f;
        cart.m_attachJoin.linearLimit = linearLimit;
        SoftJointLimitSpring linearLimitSpring = default(SoftJointLimitSpring);
        linearLimitSpring.spring = cart.m_spring;
        linearLimitSpring.damper = cart.m_springDamping;
        cart.m_attachJoin.linearLimitSpring = linearLimitSpring;
        cart.m_attachJoin.zMotion = ((ConfigurableJointMotion)0);
        cart.m_attachJoin.connectedBody = (attachTarget.gameObject.GetComponent<Rigidbody>());
    }


    /// <summary>
    /// Patch for Vagon.LateUpdate that handles a situation where the attached animal is killed.
    /// </summary>
    [HarmonyPatch(typeof(Vagon), nameof(Vagon.LateUpdate))]
    class LateUpdate_Vagon_Patch
    {
        static void Prefix(ref Vagon __instance, ref ConfigurableJoint ___m_attachJoin, ref Rigidbody ___m_body)
        {
            try
            {
                if (___m_attachJoin != null && ___m_attachJoin.connectedBody == null)
                {
                    __instance.Detach();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    /// <summary>
    /// Patch overriding InUse that will correctly return false if an animal is the one attached to a cart
    /// </summary>
    [HarmonyPatch(typeof(Vagon), nameof(Vagon.InUse))]
    class InUse_Vagon_Patch
    {
        static bool Prefix(ref bool __result, ref Vagon __instance)
        {
            if ((bool)__instance.m_container && __instance.m_container.IsInUse())
            {
                __result = true;
            }
            else if (__instance.IsAttached())
            {
                __result = (bool)__instance.m_attachJoin.connectedBody.gameObject.GetComponent<Player>();
            }
            else
            {
                __result = false;
            }

            return false;
        }
    }

    /// <summary>
    /// Patch to FixedUpdate that will attempt to attach cart to animal if there is an appropriate one nearby.
    /// </summary>
    [HarmonyPatch(typeof(Vagon), nameof(Vagon.FixedUpdate))]
    class Vagon_FixedUpdate_Patch
    {
        static bool Prefix(Vagon __instance)
        {
            if (!__instance.m_nview.IsValid())
            {
                return false;
            }

            if (__instance.IsAttached() && __instance.m_attachJoin?.connectedBody == null)
            {
                __instance.Detach();
                return false;
            }

            // Attempt to attach the cart
            __instance.UpdateAudio(Time.fixedDeltaTime);
            if (__instance.m_nview.IsOwner())
            {
                if ((bool)__instance.m_useRequester)
                {
                    if (__instance.IsAttached())
                    {
                        // If attached detach
                        __instance.Detach();
                    }
                    else
                    {
                        /// Determine if there is a valid animal in range and if so attempt to attach to it. 
                        /// If not attempt to attach to player
                        Character closest_tamed = FindClosestAttachableAnimal(__instance);
                        if (closest_tamed != null)
                        {
                            AttachCartTo(closest_tamed, __instance);
                        }
                        else if (__instance.CanAttach(__instance.m_useRequester.gameObject))
                        {
                            AttachCartTo(__instance.m_useRequester, __instance);
                        }
                        else
                        {
                            __instance.m_useRequester.Message(MessageHud.MessageType.Center, "Not in the right position");
                        }
                    }

                    __instance.m_useRequester = null;
                }

                if (__instance.IsAttached())
                {
                    // Update detach distance before check if it should be detached
                    __instance.m_detachDistance = GetCartDetachDistance(AttachedCharacter(__instance));
                    if (!__instance.CanAttach(((Component)(object)((Joint)__instance.m_attachJoin).connectedBody).gameObject))
                    {
                        __instance.Detach();
                    }
                }
            }
            else if (__instance.IsAttached())
            {
                __instance.Detach();
            }

            return false;
        }
    }

    /// <summary>
    /// Patch for follow logic that allows for a greater follow distance.
    /// This is necessary because the lox tries to follow the player so closely that it constantly pushes the player
    /// Future use could include randomizing follow distance so multiple cart pulling animals are less likely to collide.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.Follow))]
    class Tamed_Follow_patch
    {
        static bool Prefix(GameObject go, float dt, ref BaseAI __instance)
        {
            /// Allow normal follow code to run if character isn't tamed
            if (!__instance.m_character.IsTamed())
            {
                return true;
            }

            // If the character isn't following a player allow the normal follow code to run
            if ((__instance as MonsterAI).GetFollowTarget().GetComponent<Player>() == null)
            {
                return true;
            }

            float distance = Vector3.Distance(go.transform.position, __instance.transform.position);
            float followDistance;
            switch (ParseCharacterType(__instance.m_character))
            {
                case HorseBeasts.horse:
                    followDistance = 3f;
                    break;
                default:
                    // Kick it back to the original method for unknown creature types
                    return true;
            }

            bool run = distance > followDistance * 3;
            if (distance < followDistance)
            {
                __instance.StopMoving();
            }
            else
            {
                __instance.MoveTo(dt, go.transform.position, 0f, run);
            }

            return false;
        }
    }

    #endregion

    #region HorseJump_is_off

    /*
    [HarmonyPatch(typeof(Player), "SetControls")]
    private static class SetControls
    {
        private static void Prefix(Player __instance, ref bool jump)
        {
            if (__instance.IsRiding() && jump)
            {

                Sadle saddle = (Sadle)__instance.m_doodadController;
                if (saddle != null && saddle.gameObject.name == "rae_SaddleHorse")
                {
                    Debug.LogError("I am trying to jump on mount");
                    saddle.m_monsterAI.m_character.Jump();

                    jump = false;
                }
                return;
            }

            if (!__instance.IsRiding())
            {
                return;
            }

            jump = false;
        }
    }
    */

    #endregion

    #region PinHandler is OFF

    public static class CustomMapPins
    {
        public class CustomPinhandlerHorse : MonoBehaviour
        {
            public Sprite icon;
            public string pinName;
            private Minimap.PinData pin;

            private void Awake()
            {
                if (!gameObject.GetComponent<Tameable>().HaveSaddle())
                {
                    DestroyImmediate(this);
                    return;
                }

                pin = new Minimap.PinData();
                pin.m_type = Minimap.PinType.Icon0;
                pin.m_name = Localization.instance.Localize(pinName);
                pin.m_pos = transform.position;
                pin.m_icon = icon;
                pin.m_save = false;
                pin.m_checked = false;
                pin.m_ownerID = 0;
                RectTransform root = (Minimap.instance.m_mode == Minimap.MapMode.Large) ? Minimap.instance.m_pinNameRootLarge : Minimap.instance.m_pinNameRootSmall;
                pin.m_NamePinData = new Minimap.PinNameData(pin);
                Minimap.instance.CreateMapNamePin(pin, root);
                pin.m_NamePinData.PinNameText.richText = true;
                pin.m_NamePinData.PinNameText.overrideColorTags = false;
                Minimap.instance?.m_pins?.Add(pin);
            }

            private void LateUpdate()
            {
                pin.m_checked = false;
                pin.m_pos = transform.position;
            }

            private void OnDestroy()
            {
                if (pin == null) return;
                if (pin.m_uiElement) Minimap.instance.DestroyPinMarker(pin);
                Minimap.instance?.m_pins?.Remove(pin);
            }
        }

        public static void RegisterCustomPin(GameObject go, string name, Sprite icon)
        {
            var comp = go.AddComponent<CustomPinhandlerHorse>();
            comp.pinName = name;
            comp.icon = icon;
        }
    }

    #endregion

    #region PinHandler_Cart

    public static class CustomMapPins_Cart
    {
        public class CustomPinhandler_Generic_Cart : MonoBehaviour
        {
            public Sprite icon;
            public string pinName;
            private Minimap.PinData pin;

            private void Awake()
            {
                pin = new Minimap.PinData();
                pin.m_type = Minimap.PinType.Icon0;
                pin.m_name = Localization.instance.Localize(pinName);
                pin.m_pos = transform.position;
                pin.m_icon = icon;
                pin.m_save = false;
                pin.m_checked = false;
                pin.m_ownerID = 0;
                RectTransform root = (Minimap.instance.m_mode == Minimap.MapMode.Large) ? Minimap.instance.m_pinNameRootLarge : Minimap.instance.m_pinNameRootSmall;
                pin.m_NamePinData = new Minimap.PinNameData(pin);
                Minimap.instance.CreateMapNamePin(pin, root);
                pin.m_NamePinData.PinNameText.richText = true;
                pin.m_NamePinData.PinNameText.overrideColorTags = false;
                Minimap.instance?.m_pins?.Add(pin);
            }

            private void LateUpdate()
            {
                pin.m_checked = false;
                pin.m_pos = transform.position;
            }

            private void OnDestroy()
            {
                if (pin == null) return;

                // Ensure pin UI elements are safely destroyed
                if (pin.m_uiElement != null)
                    Minimap.instance.DestroyPinMarker(pin);
                Minimap.instance?.m_pins?.Remove(pin);
            }
        }

        public static void RegisterCustomPinGeneric(GameObject go, string name, Sprite icon)
        {
            var comp = go.AddComponent<CustomPinhandler_Generic_Cart>();
            comp.pinName = name;
            comp.icon = icon;
        }
    }

    #endregion
    [HarmonyPatch(typeof(CookingStation))]
    public static class CookingStationPatch
    {
        // Patch the Awake method to ensure m_conversion is properly initialized
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(CookingStation __instance)
        {
            // Log if the CookingStation instance is valid
            if (__instance == null)
            {
                Debug.LogError("CookingStation instance is null in Awake_Postfix.");
                return;
            }

            // Log if m_conversion is null or has existing items
            if (__instance.m_conversion == null)
            {
                __instance.m_conversion = new List<CookingStation.ItemConversion>();
                Debug.Log("m_conversion was null and has been initialized.");
            }
            else
            {
                Debug.Log($"m_conversion is already initialized with {__instance.m_conversion.Count} items.");
            }

            // Add custom item conversion
            AddCustomConversion(__instance);
        }

        // Adds a custom item conversion to the CookingStation
        private static void AddCustomConversion(CookingStation station)
        {
            try
            {
                // Ensure the station and its m_conversion list are valid
                if (station == null || station.m_conversion == null)
                {
                    Debug.LogError("CookingStation or m_conversion is null during AddCustomConversion.");
                    return;
                }

                // Define the item conversion
                CookingStation.ItemConversion customConversion = new CookingStation.ItemConversion
                {
                    m_from = ObjectDB.instance.GetItemPrefab("rae_HorseMeat").GetComponent<ItemDrop>(),
                    m_to = ObjectDB.instance.GetItemPrefab("rae_CookedHorseMeat").GetComponent<ItemDrop>(),
                    m_cookTime = 25f
                };

                // Verify the items exist before adding
                if (customConversion.m_from == null)
                {
                    Debug.LogError("Could not find prefab for 'rae_HorseMeat'.");
                    return;
                }

                if (customConversion.m_to == null)
                {
                    Debug.LogError("Could not find prefab for 'rae_CookedHorseMeat'.");
                    return;
                }

                // Add the conversion to the CookingStation
                station.m_conversion.Add(customConversion);
                Debug.Log($"Added custom conversion: {customConversion.m_from.name} -> {customConversion.m_to.name} with cook time {customConversion.m_cookTime}s.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception in AddCustomConversion: {ex}");
            }
        }
    }


}