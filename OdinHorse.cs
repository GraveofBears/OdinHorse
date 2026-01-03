using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CreatureManager;
using HarmonyLib;
using ItemManager;
using LocalizationManager;
using PieceManager;
using ServerSync;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;
using UnityEngine.InputSystem;
namespace OdinHorse;
[BepInPlugin(ModGUID, ModName, ModVersion)]

//[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]

public class OdinHorse : BaseUnityPlugin
{
    private const string ModName = "OdinHorse";
    private const string ModVersion = "1.5.6";
    private const string ModGUID = "Raelaziel.OdinHorse";

    #region ConfigEntry

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

    internal static Creature raeHorse;
    internal static ConfigEntry<bool> ServerConfigLocked = null!;
    internal static ConfigEntry<int> HorseRunningSpeed = null!;
    internal static ConfigEntry<int> HorseWalkingSpeed = null!;
    internal static ConfigEntry<int> HorseSpeed = null!;
    internal static ConfigEntry<int> HorseHealth = null!;
    internal static ConfigEntry<float> HorseStamina = null!;
    internal static ConfigEntry<float> HorseStaminaRegen = null!;
    internal static ConfigEntry<float> HorseStaminaRegenHungry = null!;
    internal static ConfigEntry<float> HorseProcreationUpdateInterval;
    internal static ConfigEntry<float> HorseProcreationTotalCheckRange;
    internal static ConfigEntry<int> HorseProcreationMaxCreatures;
    internal static ConfigEntry<float> HorseProcreationPartnerCheckRange;
    internal static ConfigEntry<float> HorseProcreationPregnancyChance;
    internal static ConfigEntry<float> HorseProcreationPregnancyDuration;
    internal static ConfigEntry<int> HorseProcreationRequiredLovePoints;
    internal static ConfigEntry<int> HorseOffspringHealth = null!;
    internal static ConfigEntry<int> HorseOffspringGrowupTime = null!;
    internal static ConfigEntry<float> HorseOffspringMeatDropChance = null!;
    internal static ConfigEntry<int> HorseOffspringMeatDropMinimum = null!;
    internal static ConfigEntry<int> HorseOffspringMeatDropMaximum = null!;
    internal static ConfigEntry<float> HorseOffspringHideDropChance = null!;
    internal static ConfigEntry<int> HorseOffspringHideDropMinimum = null!;
    internal static ConfigEntry<int> HorseOffspringHideDropMaximum = null!;
    internal static ConfigEntry<KeyboardShortcut> RemoveArmorHotKey = null!;
    internal static ConfigEntry<KeyboardShortcut> WaitHotKey = null!;
    internal static ConfigEntry<bool> HideHorsePin;
    internal static ConfigEntry<bool> HideCartPin;
    private static List<Material> horseMaterials;
    private static AssetBundle horseAssetBundle;

    #endregion

    #region Awake

    public void Awake()
    {
        Localizer.Load();
        LoadAssetBundle();
        serverConfigLocked = config("General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
        configSync.AddLockingConfigEntry(serverConfigLocked);
        HorseRunningSpeed = config("Horse", "Running Speed", 8, new ConfigDescription("Declare running speed for Horse"));
        HorseWalkingSpeed = config("Horse", "Walking Speed", 5, new ConfigDescription("Declare walking speed for Horse"));
        HorseSpeed = config("Horse", "Speed Modifier", 7, new ConfigDescription("Declare speed modified for Horse"));
        HorseHealth = config("Horse", "Health", 200, new ConfigDescription("Declare health points for Horse"));
        HorseStamina = config("Horse", "Stamina", 400f, new ConfigDescription("Declare stamina for Horse"));
        HorseStaminaRegen = config("Horse", "Stamina Regen", 2f, new ConfigDescription("Declare stamina regen for Horse"));
        HorseStaminaRegenHungry = config("Horse", "Stamina Regen Hungry", 1f, new ConfigDescription("Declare stamina regen when hungry for Horse"));
        HorseProcreationUpdateInterval = config("Horse Procreation", "Update Interval", 10f, "Time interval in seconds to check for procreation.");
        HorseProcreationTotalCheckRange = config("Horse Procreation", "Total Check Range", 10f, "Range in meters to check for total creatures.");
        HorseProcreationMaxCreatures = config("Horse Procreation", "Max Creatures", 4, "Maximum number of creatures allowed in range.");
        HorseProcreationPartnerCheckRange = config("Horse Procreation", "Partner Check Range", 3f, "Range in meters to find a procreation partner.");
        HorseProcreationPregnancyChance = config("Horse Procreation", "Pregnancy Chance", 0.5f, "Chance of becoming pregnant per check.");
        HorseProcreationPregnancyDuration = config("Horse Procreation", "Pregnancy Duration", 10f, "Duration of pregnancy in seconds.");
        HorseProcreationRequiredLovePoints = config("Horse Procreation", "Required Love Points", 4, "Number of love points required to trigger pregnancy.");
        HorseOffspringHealth = config("Horse Offspring", "Health", 60, new ConfigDescription("Declare health points for Horse Offspring"));
        HorseOffspringGrowupTime = config("Horse Offspring", "Grow-up time", 2000, new ConfigDescription("Declare growup time needed to convert offspring into Horse. Time in seconds."));
        HorseOffspringMeatDropChance = config("Horse Offspring", "Meat Drop Chance", 1.00f, new ConfigDescription("Declare drop chance for Horse Meat from offspring"));
        HorseOffspringMeatDropMinimum = config("Horse Offspring", "Meat Amount Min", 1, new ConfigDescription("Declare minimum amount of Horse Meat to drop from offspring"));
        HorseOffspringMeatDropMaximum = config("Horse Offspring", "Meat Amount Max", 2, new ConfigDescription("Declare maximum amount of Horse Meat to drop from offspring"));
        HorseOffspringHideDropChance = config("Horse Offspring", "Hide Drop Chance", 0.33f, new ConfigDescription("Declare drop chance for Horse Hide from offspring"));
        HorseOffspringHideDropMinimum = config("Horse Offspring", "Hide Amount Min", 1, new ConfigDescription("Declare minimum amount of Horse Hide to drop from offspring"));
        HorseOffspringHideDropMaximum = config("Horse Offspring", "Hide Amount Max", 1, new ConfigDescription("Declare maximum amount of Horse Hide to drop from offspring"));
        RemoveArmorHotKey = config("Hotkeys", "Remove Armor Key", new KeyboardShortcut(KeyCode.R), new ConfigDescription("The key needed to be held while interacting with the horse to remove the armor."));
        WaitHotKey = config("Hotkeys", "Wait Here Key", new KeyboardShortcut(KeyCode.T), new ConfigDescription("The key needed to be held while interacting with the horse to make it wait in place."));
        HideHorsePin = config("Map Icons", "HideHorsePin", false, "If true, disables the map icon for the Odin Horse.");
        HideCartPin = config("Map Icons", "HideCartPin", false, "If true, disables the map icon for the Odin Cart.");

        #endregion

        #region Items
        Item raeHorseMeat = new Item("horsesets", "rae_HorseMeat");
        Item raeOdinHorseHide = new Item("horsesets", "rae_HorseHide");
        Item raeOdinHorseTrophy = new Item("horsesets", "rae_OdinHorse_Trophy");
        Item rae_CookedHorseMeat = new Item("horsesets", "rae_CookedHorseMeat");
        Item raeHorseSaddle = new Item("horsesets", "rae_SaddleHorse");

        raeHorseSaddle.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseSaddle.MaximumRequiredStationLevel = 5; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSaddle.RequiredItems.Add("Bronze", 1);
        raeHorseSaddle.RequiredItems.Add("rae_HorseHide", 8);
        raeHorseSaddle.CraftAmount = 1;

        Item rae_iron_HorseArmor = new Item("horsesets", "rae_iron_HorseArmor");
        rae_iron_HorseArmor.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        rae_iron_HorseArmor.MaximumRequiredStationLevel = 5; // Limits the crafting station level required to upgrade or repair the item to 5
        rae_iron_HorseArmor.RequiredItems.Add("Iron", 10);
        rae_iron_HorseArmor.RequiredItems.Add("rae_HorseHide", 20);
        rae_iron_HorseArmor.CraftAmount = 1;

        TameableExtensions.m_armorItem = rae_iron_HorseArmor.Prefab.GetComponent<ItemDrop>();
        Item raeHorseSticks = new Item("horsesets", "rae_HorseSticks");
        raeHorseSticks.Configurable = Configurability.Full;
        raeHorseSticks.Crafting.Add(ItemManager.CraftingTable.Cauldron, 1);
        raeHorseSticks.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSticks.RequiredItems.Add("rae_HorseMeat", 3);
        raeHorseSticks.RequiredItems.Add("Coal", 1);
        raeHorseSticks.RequiredItems.Add("Dandelion", 2);
        raeHorseSticks.CraftAmount = 2;

        Item raeHorseSoup = new Item("horsesets", "rae_HorseSoup");
        raeHorseSoup.Configurable = Configurability.Full;
        raeHorseSoup.Crafting.Add(ItemManager.CraftingTable.Cauldron, 1);
        raeHorseSoup.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSoup.RequiredItems.Add("rae_HorseMeat", 2);
        raeHorseSoup.RequiredItems.Add("Carrot", 2);
        raeHorseSoup.RequiredItems.Add("Dandelion", 3);
        raeHorseSoup.CraftAmount = 1;

        Item raeHorseSkewer = new Item("horsesets", "rae_HorseMeatSkewer");
        raeHorseSkewer.Configurable = Configurability.Full;
        raeHorseSkewer.Crafting.Add(ItemManager.CraftingTable.Cauldron, 2);
        raeHorseSkewer.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseSkewer.RequiredItems.Add("rae_HorseMeat", 3);
        raeHorseSkewer.RequiredItems.Add("Mushroom", 2);
        raeHorseSkewer.RequiredItems.Add("NeckTail", 1);
        raeHorseSkewer.CraftAmount = 2;

        Item raeHorseaker = new Item("horsesets", "rae_Horseaker");
        raeHorseaker.Configurable = Configurability.Full;
        raeHorseaker.Crafting.Add(ItemManager.CraftingTable.Forge, 1);
        raeHorseaker.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseaker.RequiredItems.Add("Iron", 20);
        raeHorseaker.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseaker.RequiredItems.Add("ElderBark", 35);
        raeHorseaker.RequiredUpgradeItems.Add("Iron", 5);
        raeHorseaker.RequiredUpgradeItems.Add("ElderBark", 10);
        raeHorseaker.CraftAmount = 1;

        Item raeHorseHelmet = new Item("horsesets", "rae_OdinHorse_Helmet");
        raeHorseHelmet.Configurable = Configurability.Full;
        raeHorseHelmet.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseHelmet.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseHelmet.RequiredItems.Add("Tin", 10);
        raeHorseHelmet.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseHelmet.RequiredItems.Add("rae_HorseHide", 5);
        raeHorseHelmet.RequiredUpgradeItems.Add("rae_HorseHide", 4);
        raeHorseHelmet.RequiredUpgradeItems.Add("Tin", 2);
        raeHorseHelmet.CraftAmount = 1;

        Item raeHorseCape = new Item("horsesets", "rae_CapeHorseHide");
        raeHorseCape.Configurable = Configurability.Full;
        raeHorseCape.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        raeHorseCape.MaximumRequiredStationLevel = 10; // Limits the crafting station level required to upgrade or repair the item to 5
        raeHorseCape.RequiredItems.Add("rae_HorseHide", 10);
        raeHorseCape.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        raeHorseCape.RequiredItems.Add("Tin", 2);
        raeHorseCape.RequiredUpgradeItems.Add("rae_HorseHide", 10);
        raeHorseCape.RequiredUpgradeItems.Add("Tin", 2);
        raeHorseCape.CraftAmount = 1;

        Item ArmorHorseClothHelmet_T1 = new Item("horsesets", "ArmorHorseClothHelmet_T1");
        ArmorHorseClothHelmet_T1.Configurable = Configurability.Full;
        ArmorHorseClothHelmet_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothHelmet_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothHelmet_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothHelmet_T1.RequiredItems.Add("LeatherScraps", 6);
        ArmorHorseClothHelmet_T1.RequiredItems.Add("rae_OdinHorse_Trophy", 1);
        ArmorHorseClothHelmet_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothHelmet_T1.RequiredUpgradeItems.Add("LeatherScraps", 3);
        ArmorHorseClothHelmet_T1.CraftAmount = 1;

        Item ArmorHorseClothHelmet_T2 = new Item("horsesets", "ArmorHorseClothHelmet_T2");
        ArmorHorseClothHelmet_T2.Configurable = Configurability.Full;
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
        ArmorHorseClothChest_T1.Configurable = Configurability.Full;
        ArmorHorseClothChest_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothChest_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothChest_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothChest_T1.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothChest_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothChest_T1.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothChest_T1.CraftAmount = 1;

        Item ArmorHorseClothChest_T2 = new Item("horsesets", "ArmorHorseClothChest_T2");
        ArmorHorseClothChest_T2.Configurable = Configurability.Full;
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
        ArmorHorseClothLegs_T1.Configurable = Configurability.Full;
        ArmorHorseClothLegs_T1.Crafting.Add(ItemManager.CraftingTable.Workbench, 1);
        ArmorHorseClothLegs_T1.MaximumRequiredStationLevel = 10;
        ArmorHorseClothLegs_T1.RequiredItems.Add("rae_HorseHide", 4);
        ArmorHorseClothLegs_T1.RequiredItems.Add("LeatherScraps", 8);
        ArmorHorseClothLegs_T1.RequiredUpgradeItems.Add("rae_HorseHide", 2);
        ArmorHorseClothLegs_T1.RequiredUpgradeItems.Add("LeatherScraps", 4);
        ArmorHorseClothLegs_T1.CraftAmount = 1;

        Item ArmorHorseClothLegs_T2 = new Item("horsesets", "ArmorHorseClothLegs_T2");
        ArmorHorseClothLegs_T2.Configurable = Configurability.Full;
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

        BuildPiece rae_OdinHorse_Rug_Headless = new("horsesets", "rae_OdinHorse_Rug_Headless"); // Note: If you wish to use the default "assets" folder for your assets, you can omit it!
        rae_OdinHorse_Rug_Headless.RequiredItems.Add("rae_HorseHide", 20, true);
        rae_OdinHorse_Rug_Headless.Category.Set(BuildPieceCategory.Furniture);
        rae_OdinHorse_Rug_Headless.Crafting.Set(PieceManager.CraftingTable.Workbench);

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
        GameObject sfx_horse_idle = ItemManager.PrefabManager.RegisterPrefab("horsesets", "sfx_horse_idle");
        GameObject sfx_horse_birth = ItemManager.PrefabManager.RegisterPrefab("horsesets", "sfx_horse_birth");
        GameObject vfx_horse_birth = ItemManager.PrefabManager.RegisterPrefab("horsesets", "vfx_horse_birth");
        GameObject sfx_horse_hit = ItemManager.PrefabManager.RegisterPrefab("horsesets", "sfx_horse_hit");
        GameObject vfx_horse_death = ItemManager.PrefabManager.RegisterPrefab("horsesets", "vfx_horse_death");
        GameObject Sfx_horse_love = ItemManager.PrefabManager.RegisterPrefab("horsesets", "Sfx_horse_love");
        GameObject vfx_horse_love = ItemManager.PrefabManager.RegisterPrefab("horsesets", "vfx_horse_love");

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
        raeHorse.Prefab.GetComponent<Humanoid>().m_walkSpeed = HorseWalkingSpeed.Value;
        raeHorse.Prefab.GetComponent<Humanoid>().m_speed = HorseSpeed.Value;
        raeHorse.Prefab.GetComponent<Humanoid>().m_health = HorseHealth.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_maxStamina = HorseStamina.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_staminaRegen = HorseStaminaRegen.Value;
        raeHorse.Prefab.GetComponentInChildren<Sadle>(true).m_staminaRegenHungry = HorseStaminaRegenHungry.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_updateInterval = HorseProcreationUpdateInterval.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_totalCheckRange = HorseProcreationTotalCheckRange.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_maxCreatures = HorseProcreationMaxCreatures.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_partnerCheckRange = HorseProcreationPartnerCheckRange.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_pregnancyChance = HorseProcreationPregnancyChance.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_pregnancyDuration = HorseProcreationPregnancyDuration.Value;
        raeHorse.Prefab.GetComponentInChildren<Procreation>(true).m_requiredLovePoints = HorseProcreationRequiredLovePoints.Value;

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

        #region HidePinsValues

        if (!HideHorsePin.Value)
        {
            CustomMapPins.RegisterCustomPin(
                raeHorse.Prefab,
                "$horse_odin",
                raeOdinHorseTrophy.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons[0]
            );
        }
        if (!HideCartPin.Value)
        {
            CustomMapPins_Cart.RegisterCustomPinGeneric(
                raeHorseCart.Prefab,
                "$pin_cart",
                raeHorseCart.Prefab.GetComponent<Piece>().m_icon
            );
        }
        // Patch
        Assembly assembly = Assembly.GetExecutingAssembly();
        Harmony harmony = new(ModGUID);
        harmony.PatchAll(assembly);
    }

    #endregion

    #region LoadAssetBundle

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
        // Logger.LogInfo("Successfully loaded horse materials.");
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

            ZNetView znv = __instance.GetComponent<ZNetView>();
            if (znv == null || !znv.IsValid()) return;

            // Delay material application to ensure ZDO is fully synchronized
            __instance.StartCoroutine(ApplyHorseMaterial(__instance, znv));
        }

        private static IEnumerator ApplyHorseMaterial(Character character, ZNetView znv)
        {
            // Wait a frame to ensure ZDO is fully synchronized
            yield return null;

            SkinnedMeshRenderer renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null) yield break;

            // Check if material index is already set
            int materialIndex = znv.GetZDO().GetInt("HorseMaterial", -1);

            // Only assign random material if it has NEVER been set (still -1)
            if (materialIndex == -1)
            {
                // Wait a bit longer for potential network sync
                yield return new WaitForSeconds(0.5f);

                // Check again after waiting
                materialIndex = znv.GetZDO().GetInt("HorseMaterial", -1);

                // If still -1, this is a brand new horse - assign color
                if (materialIndex == -1 && znv.IsOwner())
                {
                    // Only owner assigns to avoid conflicts
                    materialIndex = UnityEngine.Random.Range(0, horseMaterials.Count);
                    znv.GetZDO().Set("HorseMaterial", materialIndex);
                }
                else if (materialIndex == -1)
                {
                    // Non-owner: wait for owner to set it
                    yield return new WaitForSeconds(1f);
                    materialIndex = znv.GetZDO().GetInt("HorseMaterial", 0);
                }
            }

            // Validate index is within bounds
            if (materialIndex < 0 || materialIndex >= horseMaterials.Count)
            {
                materialIndex = 0; // Fallback to first material
            }

            renderer.material = horseMaterials[materialIndex];
        }
    }

    // Patch to handle material persistence during updates
    [HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
    public static class Character_SetLevel_Patch
    {
        static void Postfix(Character __instance)
        {
            if (!__instance.gameObject.name.Contains("rae_OdinHorse")) return;

            ZNetView znv = __instance.GetComponent<ZNetView>();
            if (znv == null || !znv.IsValid()) return;

            // Reapply the saved material
            SkinnedMeshRenderer renderer = __instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer == null) return;

            int materialIndex = znv.GetZDO().GetInt("HorseMaterial", -1);
            if (materialIndex >= 0 && materialIndex < horseMaterials.Count)
            {
                renderer.material = horseMaterials[materialIndex];
            }
        }
    }

    #endregion

    #region ArmorAddition Patches
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
    static class AddRPCForHorseArmorTameableAwakePatch
    {
        static void Postfix(Tameable __instance)
        {
            // Only apply to OdinHorse prefabs
            if (!__instance.transform.root.gameObject.name.Contains("rae_OdinHorse")) return;
            if (__instance.m_character != null)
            {
                // Prevent duplicate death callback
                __instance.m_character.m_onDeath -= __instance.OnHorseDeath;
                __instance.m_character.m_onDeath += __instance.OnHorseDeath;
            }
            if (__instance.m_nview == null || !__instance.m_nview.IsValid()) return;
            // Always register RPCs — safe to do per instance, per client
            __instance.m_nview.Register("AddArmor", (long sender) => __instance.RPC_AddArmor(sender));
            __instance.m_nview.Register<bool>("SetArmor", (long sender, bool enabled) => __instance.RPC_SetArmor(sender, enabled));
            __instance.m_nview.Register<Vector3>("RemoveArmor", (long sender, Vector3 position) => __instance.RPC_RemoveArmor(sender, position));
            __instance.m_nview.Register("ToggleWait", (long sender) => __instance.RPC_ToggleWait(sender));
            __instance.m_nview.Register<bool>("SetWait", (long sender, bool enabled) => __instance.RPC_SetWait(sender, enabled));
            // Sync armor state after RPC registration
            __instance.SetArmor(__instance.HaveArmor());
            // Sync wait state
            __instance.SetWaitAnimation(__instance.IsWaiting());
        }
    }
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    static class AddTextForRemovalOfArmorTameableGetHoverTextPatch
    {
        static void Postfix(Tameable __instance, ref string __result)
        {
            if (!__instance.transform.root.gameObject.name.Contains("rae_OdinHorse")) return;
            if (!__instance.m_nview.IsValid())
                __result += "";
            if (__instance.HaveArmor() && __instance.m_character.IsTamed())
            {
                __result += !ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive()
                    ? Localization.instance.Localize($"\n[<color=yellow><b>{RemoveArmorHotKey.Value.ToString()} + $KEY_Use</b></color>] Remove Armor")
                    : Localization.instance.Localize($"\n[<color=yellow><b>{RemoveArmorHotKey.Value.ToString()} + $KEY_Use</b></color>] Remove Armor");
            }
            if (__instance.m_character.IsTamed())
            {
                bool isWaiting = __instance.IsWaiting();
                __result += !ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive()
                    ? Localization.instance.Localize($"\n[<color=yellow><b>{WaitHotKey.Value.ToString()} + $KEY_Use</b></color>] {(isWaiting ? "Resume" : "Wait Here")}")
                    : Localization.instance.Localize($"\n[<color=yellow><b>{WaitHotKey.Value.ToString()} + $KEY_Use</b></color>] {(isWaiting ? "Resume" : "Wait Here")}");
            }
        }
    }
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    static class RemoveArmorTameableInteractPatch
    {
        static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt)
        {
            if (!__instance.m_nview.IsValid() || hold)
                return false;
            if (!__instance.transform.root.gameObject.name.Contains("rae_OdinHorse"))
                return true;
            // Check for armor removal first
            if (RemoveArmorHotKey.Value.IsKeyHeld() && __instance.HaveArmor())
            {
                __instance.m_nview.InvokeRPC("RemoveArmor", user.transform.position);
                return false;
            }
            // Check for wait toggle
            if (WaitHotKey.Value.IsKeyHeld())
            {
                __instance.m_nview.InvokeRPC("ToggleWait");
                return false;
            }
            // If not removing armor or toggling wait, allow normal interact
            return true;
        }
    }
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    static class CycleHorseStateTameableInteractPatch
    {
        static void Postfix(Tameable __instance, Humanoid user, bool hold, bool alt, bool __result)
        {
            if (!__instance.m_nview.IsValid() || hold || alt)
                return;
            if (!__instance.transform.root.gameObject.name.Contains("rae_OdinHorse"))
                return;
            // Only cycle state if it's a normal interact (not armor removal or wait toggle)
            if (RemoveArmorHotKey.Value.IsKeyHeld() || WaitHotKey.Value.IsKeyHeld())
                return;
            if (!__instance.IsTamed())
                return;
            // Resume from waiting if interacting normally
            if (__instance.IsWaiting())
            {
                __instance.m_nview.InvokeRPC("ToggleWait");
            }
        }
    }
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
    static class UseArmorOnHorseTameableUseItemPatch
    {
        static void Postfix(Tameable __instance, Humanoid user, ItemDrop.ItemData item)
        {
            if (!__instance.transform.root.gameObject.name.Contains("rae_OdinHorse")) return;
            if (!__instance.m_nview.IsValid() || __instance.GetArmorItem() == null || !__instance.m_character.IsTamed() || item.m_shared.m_name != __instance.GetArmorItem().m_itemData.m_shared.m_name)
            {
                return;
            }
            if (__instance.HaveArmor())
            {
                user.Message(MessageHud.MessageType.Center, __instance.m_character.GetHoverName() + " Armor Already equipped");
            }
            else
            {
                //__instance.m_nview.InvokeRPC("AddArmor");
                __instance.m_nview.InvokeRPC("AddArmor", ZNet.GetUID());
                user.GetInventory().RemoveOneItem(item);
                user.Message(MessageHud.MessageType.Center, __instance.m_character.GetHoverName() + " Armor added");
            }
        }
    }
    #endregion

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
        linearLimit.limit = 0.05f;
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

    #region Wait State Patch
    /// <summary>
    /// Patch to prevent AI updates when horse is waiting
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    class MonsterAI_UpdateAI_WaitPatch
    {
        static bool Prefix(MonsterAI __instance, ref bool __result)
        {
            // Add null check for __instance
            if (__instance == null)
                return true;

            // Add null check for gameObject
            if (__instance.gameObject == null)
                return true;

            // Only apply to OdinHorse
            if (!__instance.gameObject.name.Contains("rae_OdinHorse"))
                return true;

            Tameable tameable = __instance.GetComponent<Tameable>();
            if (tameable != null && tameable.IsWaiting())
            {
                // Stop all movement
                __instance.StopMoving();
                __result = true;
                return false; // Skip the normal AI update
            }

            return true;
        }
    }
    /// <summary>
    /// Patch to resume normal behavior when horse is mounted
    /// </summary>
    [HarmonyPatch(typeof(Sadle), nameof(Sadle.UseItem))]
    class Sadle_UseItem_Patch
    {
        static void Postfix(Sadle __instance, bool __result)
        {
            if (!__result) return;
            Tameable tameable = __instance.GetComponentInParent<Tameable>();
            if (tameable != null && tameable.IsWaiting())
            {
                tameable.m_nview.InvokeRPC("ToggleWait");
            }
        }
    }
    /// <summary>
    /// Patch to resume normal behavior when horse is attacked
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    class Character_Damage_Patch
    {
        static void Postfix(Character __instance, HitData hit)
        {
            if (!__instance.gameObject.name.Contains("rae_OdinHorse")) return;
            Tameable tameable = __instance.GetComponent<Tameable>();
            if (tameable != null && tameable.IsWaiting())
            {
                tameable.m_nview.InvokeRPC("ToggleWait");
            }
        }
    }
    #endregion

    #region PinHandler is On
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
}

#endregion

#region KeyboardExtensions

public static class KeyboardExtensions
{
    // thank you to 'Margmas' for giving me this snippet from VNEI https://github.com/MSchmoecker/VNEI/blob/master/VNEI/Logic/BepInExExtensions.cs#L21
    // since KeyboardShortcut.IsPressed and KeyboardShortcut.IsDown behave un-intuitively
    public static bool IsKeyDown(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }
    public static bool IsKeyHeld(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }
}
#endregion

#region TamableExtensions
public static class TameableExtensions
{
    public static ItemDrop m_armorItem = null!;
    public static readonly int s_haveArmorHash = "HaveArmor".GetStableHashCode();
    public static readonly int s_isWaitingHash = "IsWaiting".GetStableHashCode();
    public static ItemDrop GetArmorItem(this Tameable tameable)
    {
        return m_armorItem;
    }
    public static bool IsWaiting(this Tameable tameable)
    {
        return tameable.m_nview.IsValid() && tameable.m_nview.GetZDO().GetBool(s_isWaitingHash);
    }
    public static void RPC_ToggleWait(this Tameable tameable, long sender)
    {
        if (!tameable.m_nview.IsOwner())
            return;
        bool newWaitState = !tameable.IsWaiting();
        tameable.m_nview.GetZDO().Set(s_isWaitingHash, newWaitState);
        tameable.m_nview.InvokeRPC(ZNetView.Everybody, "SetWait", newWaitState);
    }
    public static void RPC_SetWait(this Tameable tameable, long sender, bool enabled)
    {
        tameable.SetWaitAnimation(enabled);
    }
    public static void SetWaitAnimation(this Tameable tameable, bool enabled)
    {
        MonsterAI monsterAI = tameable.GetComponent<MonsterAI>();
        if (monsterAI != null && monsterAI.m_animator != null)
        {
            monsterAI.m_animator.SetBool("isWaiting", enabled);
        }
    }
    public static void RPC_AddArmor(this Tameable tameable, long sender)
    {
        if (!tameable.m_nview.IsOwner() || tameable.HaveArmor())
            return;
        float currentHealthPercentage = tameable.m_character.GetHealth() / tameable.m_character.GetMaxHealth();
        tameable.m_character.SetMaxHealth(tameable.m_character.GetMaxHealth() + 200f);
        tameable.m_character.SetHealth(tameable.m_character.GetMaxHealth() * currentHealthPercentage);
        tameable.m_nview.GetZDO().Set(s_haveArmorHash, true);
        tameable.m_nview.InvokeRPC(ZNetView.Everybody, "SetArmor", true);
    }
    public static void RPC_SetArmor(this Tameable tameable, long sender, bool enabled) => SetArmor(tameable, enabled);
    public static void RPC_RemoveArmor(this Tameable tameable, long sender, Vector3 userPoint)
    {
        if (!tameable.m_nview.IsOwner())
            return;
        Character character = tameable.m_character;
        float currentHealthPercentage = character.GetHealth() / character.GetMaxHealth();
        character.SetMaxHealth(character.GetMaxHealth() - 200f);
        character.SetHealth(character.GetMaxHealth() * currentHealthPercentage);
        tameable.DropArmor(userPoint);
    }
    public static void SetArmor(this Tameable tameable, bool enabled)
    {
        ZLog.Log(("Setting armor:" + enabled.ToString()));
        if (tameable.GetArmorItem() == null)
            return;
        SetArmorObjectsOn(tameable, enabled);
    }
    public static void SetArmorObjectsOn(Tameable tameable, bool enabled)
    {
        Utils.FindChild(tameable.transform, "Horse_mask").gameObject.SetActive(enabled);
        Utils.FindChild(tameable.transform, "horse_armor").gameObject.SetActive(enabled);
    }
    public static bool HaveArmor(this Tameable tameable)
    {
        return tameable.m_nview.IsValid() && tameable.m_nview.GetZDO().GetBool(s_haveArmorHash);
    }
    public static bool DropArmor(this Tameable tameable, Vector3 userPoint)
    {
        if (!tameable.HaveArmor())
            return false;
        tameable.m_nview.GetZDO().Set(s_haveArmorHash, false);
        tameable.m_nview.InvokeRPC(ZNetView.Everybody, "SetArmor", false);
        tameable.SpawnArmor(userPoint - tameable.transform.position);
        return true;
    }
    public static void SpawnArmor(this Tameable tameable, Vector3 flyDirection)
    {
        Rigidbody component = UnityEngine.Object.Instantiate<GameObject>(m_armorItem.gameObject, tameable.transform.TransformPoint(tameable.m_dropSaddleOffset), Quaternion.identity).GetComponent<Rigidbody>();
        if (!component)
            return;
        Vector3 up = Vector3.up;
        if (flyDirection.magnitude > 0.10000000149011612)
        {
            flyDirection.y = 0.0f;
            flyDirection.Normalize();
            up += flyDirection;
        }
        component.AddForce(up * tameable.m_dropItemVel, ForceMode.VelocityChange);
    }
    public static void OnHorseDeath(this Tameable tameable)
    {
        ZLog.Log(("Valid " + tameable.m_nview.IsValid().ToString()));
        ZLog.Log(("On death " + tameable.HaveArmor().ToString()));
        if (!tameable.HaveArmor() || !tameable.m_dropSaddleOnDeath)
            return;
        ZLog.Log("Spawning armor ");
        tameable.SpawnArmor(Vector3.zero);
    }
}

#endregion

#region CookingStation

[HarmonyPatch(typeof(CookingStation))]
public static class CookingStationPatch
{
    // Track if the custom conversion has already been added
    private static bool ConversionAdded = false;
    // Patch the Awake method to ensure m_conversion is properly initialized
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void Awake_Postfix(CookingStation __instance)
    {
        if (__instance == null)
        {
            Debug.LogError("CookingStation instance is null in Awake_Postfix.");
            return;
        }
        if (__instance.m_conversion == null)
        {
            __instance.m_conversion = new List<CookingStation.ItemConversion>();
        }
        AddCustomConversion(__instance);
    }
    // Adds a custom item conversion to the CookingStation
    private static void AddCustomConversion(CookingStation station)
    {
        try
        {
            if (station == null || station.m_conversion == null)
            {
                Debug.LogError("CookingStation or m_conversion is null during AddCustomConversion.");
                return;
            }
            if (station.m_conversion.Exists(c => c.m_from.name == "rae_HorseMeat" && c.m_to.name == "rae_CookedHorseMeat"))
            {
                return; // Skip if conversion already exists
            }
            CookingStation.ItemConversion customConversion = new CookingStation.ItemConversion
            {
                m_from = ObjectDB.instance.GetItemPrefab("rae_HorseMeat").GetComponent<ItemDrop>(),
                m_to = ObjectDB.instance.GetItemPrefab("rae_CookedHorseMeat").GetComponent<ItemDrop>(),
                m_cookTime = 25f
            };
            if (customConversion.m_from == null || customConversion.m_to == null)
            {
                Debug.LogError("Could not find prefabs for 'rae_HorseMeat' or 'rae_CookedHorseMeat'.");
                return;
            }
            station.m_conversion.Add(customConversion);
            // Only log the addition once per game session
            if (!ConversionAdded)
            {
                Debug.Log($"Added custom conversion: {customConversion.m_from.name} -> {customConversion.m_to.name} with cook time {customConversion.m_cookTime}s.");
                ConversionAdded = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception in AddCustomConversion: {ex}");
        }
    }
}
#endregion