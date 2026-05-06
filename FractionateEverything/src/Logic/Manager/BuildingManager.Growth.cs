using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Fractionation.Recipes;
using HarmonyLib;
using NebulaAPI;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class BuildingManager {
    public static int Level(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.Level,
            IFE矿物复制塔 => MineralReplicationTower.Level,
            IFE点数聚集塔 => PointAggregateTower.Level,
            IFE转化塔 => ConversionTower.Level,
            IFE精馏塔 => RectificationTower.Level,
            IFE行星内物流交互站 => PlanetaryInteractionStation.Level,
            IFE星际物流交互站 => InterstellarInteractionStation.Level,
            _ => 0
        };
    }

    public static int GetDefaultMaxStackByLevel(int level) => level switch {
        < DefaultMaxStackTier1UpperExclusive => 1,
        < DefaultMaxStackTier2UpperExclusive => 4,
        < DefaultMaxStackTier3UpperExclusive => 8,
        _ => 12,
    };

    public static float GetDefaultEnergyRatioByLevel(int level) => level switch {
        < 1 => 1.0f,
        < 4 => 0.95f,
        < 7 => 0.85f,
        < 10 => 0.7f,
        _ => 0.5f,
    };

    public static float GetDefaultPlrRatioByLevel(int level) => level switch {
        < 2 => 1.0f,
        < 5 => 1.1f,
        < 8 => 1.3f,
        < 11 => 1.6f,
        _ => 1.8f,
    };

    public static float GetStationInteractEnergyRatioByLevel(int level) => level switch {
        < 1 => 1.00f,
        < 2 => 0.95f,
        < 4 => 0.85f,
        < 5 => 0.70f,
        < 7 => 0.55f,
        < 8 => 0.40f,
        < 10 => 0.30f,
        < 11 => 0.25f,
        _ => 0.20f,
    };

    private static int GetGrowthIndex(int buildingId) {
        return buildingId switch {
            IFE交互塔 => 0,
            IFE矿物复制塔 => 1,
            IFE点数聚集塔 => 2,
            IFE转化塔 => 3,
            IFE精馏塔 => 4,
            IFE行星内物流交互站 => 5,
            IFE星际物流交互站 => 5,
            _ => -1,
        };
    }

    public static long GetBuildingExp(int buildingId) {
        int index = GetGrowthIndex(buildingId);
        return index >= 0 ? buildingExp[index] : 0L;
    }

    public static bool NeedsBreakthrough(int buildingId) {
        return GetRequiredExpForNextLevelInternal(GetCurrentLevel(buildingId)) <= 0
               && GetCurrentLevel(buildingId) < MaxLevel;
    }

    public static (int matrixId, int matrixCount, int fragmentCount) GetBreakthroughCost(int buildingLevel) {
        int matrixId = GetCurrentProgressMatrixId();
        for (int i = 0; i < BreakthroughLevels.Length; i++) {
            if (BreakthroughLevels[i] == buildingLevel) {
                return (matrixId, BreakthroughMatrixCosts[i], BreakthroughFragmentCosts[i]);
            }
        }
        return (matrixId, 0, 0);
    }

    public static long GetRequiredExpForNextLevel(int buildingId) {
        return GetRequiredExpForNextLevelInternal(GetCurrentLevel(buildingId));
    }

    public static void AddBuildingExp(int buildingId, long amount) {
        int index = GetGrowthIndex(buildingId);
        if (index < 0 || amount <= 0) {
            return;
        }

        buildingExp[index] += amount;
        TryAutoLevelUp(buildingId);
    }

    private static int GetCurrentLevel(int buildingId) {
        return LDB.items.Select(buildingId)?.Level() ?? 0;
    }

    private static void TryAutoLevelUp(int buildingId) {
        int index = GetGrowthIndex(buildingId);
        if (index < 0) {
            return;
        }

        ItemProto building = LDB.items.Select(buildingId);
        if (building == null) {
            return;
        }

        while (building.Level() < MaxLevel) {
            long requiredExp = GetRequiredExpForNextLevel(buildingId);
            if (requiredExp <= 0 || buildingExp[index] < requiredExp) {
                return;
            }

            buildingExp[index] -= requiredExp;
            building.Level(building.Level() + 1);
        }
    }

    private static long GetRequiredExpForNextLevelInternal(int currentLevel) {
        return currentLevel switch {
            < 0 => 0,
            0 => 200,
            1 => 500,
            2 => 0,
            3 => 1000,
            4 => 2200,
            5 => 0,
            6 => 5000,
            7 => 9000,
            8 => 0,
            9 => 16000,
            10 => 28000,
            11 => 0,
            _ => 0,
        };
    }

    public static void Level(this ItemProto building, int level, bool manual = false) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.Level = level;
                InteractionTower.UpdateHpAndEnergy();
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.Level = level;
                MineralReplicationTower.UpdateHpAndEnergy();
                break;
            case IFE点数聚集塔:
                PointAggregateTower.Level = level;
                PointAggregateTower.UpdateHpAndEnergy();
                break;
            case IFE转化塔:
                ConversionTower.Level = level;
                ConversionTower.UpdateHpAndEnergy();
                break;
            case IFE精馏塔:
                RectificationTower.Level = level;
                RectificationTower.UpdateHpAndEnergy();
                break;
            case IFE行星内物流交互站:
            case IFE星际物流交互站:
                PlanetaryInteractionStation.Level = level;
                PlanetaryInteractionStation.UpdateHpAndEnergy();
                InterstellarInteractionStation.UpdateHpAndEnergy();
                break;
            default:
                return;
        }
        RefreshFractionatorRuntimeConfig();
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BuildingChangePacket(building.ID, 1, level));
        }
    }

    public static bool EnableFluidEnhancement(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFluidEnhancement,
            IFE矿物复制塔 => MineralReplicationTower.EnableFluidEnhancement,
            IFE点数聚集塔 => PointAggregateTower.EnableFluidEnhancement,
            IFE转化塔 => ConversionTower.EnableFluidEnhancement,
            IFE精馏塔 => RectificationTower.EnableFluidEnhancement,
            _ => false
        };
    }

    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.MaxStack,
            IFE矿物复制塔 => MineralReplicationTower.MaxStack,
            IFE点数聚集塔 => PointAggregateTower.MaxStack,
            IFE转化塔 => ConversionTower.MaxStack,
            IFE精馏塔 => RectificationTower.MaxStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxStack,
            _ => 1
        };
    }

    public static long workEnergyPerTick(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            IFE精馏塔 => RectificationTower.workEnergyPerTick,
            IFE行星内物流交互站 => PlanetaryInteractionStation.workEnergyPerTick,
            IFE星际物流交互站 => InterstellarInteractionStation.workEnergyPerTick,
            _ => LDB.models.Select(M分馏塔).prefabDesc.workEnergyPerTick
        };
    }

    public static long idleEnergyPerTick(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.idleEnergyPerTick;
            case IFE矿物复制塔:
                return MineralReplicationTower.idleEnergyPerTick;
            case IFE点数聚集塔:
                return PointAggregateTower.idleEnergyPerTick;
            case IFE转化塔:
                return ConversionTower.idleEnergyPerTick;
            case IFE精馏塔:
                return RectificationTower.idleEnergyPerTick;
            case IFE行星内物流交互站:
                return PlanetaryInteractionStation.idleEnergyPerTick;
            case IFE星际物流交互站:
                return InterstellarInteractionStation.idleEnergyPerTick;
            default:
                return LDB.models.Select(M分馏塔).prefabDesc.idleEnergyPerTick;
        }
    }

    public static float EnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnergyRatio,
            IFE矿物复制塔 => MineralReplicationTower.EnergyRatio,
            IFE点数聚集塔 => PointAggregateTower.EnergyRatio,
            IFE转化塔 => ConversionTower.EnergyRatio,
            IFE精馏塔 => RectificationTower.EnergyRatio,
            _ => 1.0f
        };
    }

    public static float InteractEnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.InteractEnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.InteractEnergyRatio,
            _ => 1.0f
        };
    }

    public static float PlrRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.PlrRatio,
            IFE矿物复制塔 => MineralReplicationTower.PlrRatio,
            IFE点数聚集塔 => PointAggregateTower.PlrRatio,
            IFE转化塔 => ConversionTower.PlrRatio,
            IFE精馏塔 => RectificationTower.PlrRatio,
            _ => 1.0f
        };
    }
}
