using System;
using System.IO;
using FE.Compatibility.Nebula;
using NebulaAPI;
using FE.Logic.Station.Definitions;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// FE 建筑等级、经验和特性查询逻辑。
/// </summary>
public static class BuildingGrowthService {
    /// <summary>
    /// 定义解锁流动输入增产加成的建筑等级阈值。
    /// </summary>
    public const int LevelThresholdFluidEnhancement = 3;
    /// <summary>
    /// 定义解锁第一项建筑特质的等级阈值。
    /// </summary>
    public const int LevelThresholdTrait1 = 6;
    /// <summary>
    /// 定义解锁第二项建筑特质的等级阈值。
    /// </summary>
    public const int LevelThresholdTrait2 = 12;
    /// <summary>
    /// 定义建筑成长需要突破的等级节点。
    /// </summary>
    public static readonly int[] BreakthroughLevels = [2, 5, 8, 11];
    /// <summary>
    /// 定义各突破阶段需要消耗的矩阵数量。
    /// </summary>
    public static readonly int[] BreakthroughMatrixCosts = [1, 2, 4, 8];
    /// <summary>
    /// 定义各突破阶段需要消耗的残片数量。
    /// </summary>
    public static readonly int[] BreakthroughFragmentCosts = [36, 120, 360, 960];
    /// <summary>
    /// 定义旧版默认堆叠一档的等级上界。
    /// </summary>
    public const int DefaultMaxStackTier1UpperExclusive = 6;
    /// <summary>
    /// 定义旧版默认堆叠二档的等级上界。
    /// </summary>
    public const int DefaultMaxStackTier2UpperExclusive = 9;
    /// <summary>
    /// 定义旧版默认堆叠三档的等级上界。
    /// </summary>
    public const int DefaultMaxStackTier3UpperExclusive = 12;
    private static readonly long[] buildingExp = new long[5];

    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public static int Level(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.Level,
            IFE矿物复制塔 => MineralReplicationTower.Level,
            IFE转化塔 => ConversionTower.Level,
            IFE精馏塔 => RectificationTower.Level,
            IFE行星内物流交互站 => PlanetaryInteractionStation.Level,
            IFE星际物流交互站 => InterstellarInteractionStation.Level,
            _ => 0
        };
    }

    /// <summary>
    /// 按建筑等级返回旧版默认处理堆叠上限。
    /// </summary>
    public static int GetDefaultMaxStackByLevel(int level) => level switch {
        < DefaultMaxStackTier1UpperExclusive => 1,
        < DefaultMaxStackTier2UpperExclusive => 4,
        < DefaultMaxStackTier3UpperExclusive => 8,
        _ => 12,
    };

    /// <summary>
    /// 按建筑等级返回分馏塔能耗倍率。
    /// </summary>
    public static float GetDefaultEnergyRatioByLevel(int level) => level switch {
        < 1 => 1.0f,
        < 4 => 0.95f,
        < 7 => 0.85f,
        < 10 => 0.7f,
        _ => 0.5f,
    };

    /// <summary>
    /// 按建筑等级返回增产点倍率。
    /// </summary>
    public static float GetDefaultPlrRatioByLevel(int level) => level switch {
        < 2 => 1.0f,
        < 5 => 1.1f,
        < 8 => 1.3f,
        < 11 => 1.6f,
        _ => 1.8f,
    };

    /// <summary>
    /// 按建筑等级返回交互站交互能耗倍率。
    /// </summary>
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
            IFE转化塔 => 2,
            IFE精馏塔 => 3,
            IFE行星内物流交互站 => 4,
            IFE星际物流交互站 => 4,
            _ => -1,
        };
    }

    /// <summary>
    /// 读取指定建筑类型累计的成长经验。
    /// </summary>
    public static long GetBuildingExp(int buildingId) {
        int index = GetGrowthIndex(buildingId);
        return index >= 0 ? buildingExp[index] : 0L;
    }

    /// <summary>
    /// 判断指定建筑类型当前等级是否需要突破材料。
    /// </summary>
    public static bool NeedsBreakthrough(int buildingId) {
        return GetRequiredExpForNextLevelInternal(GetCurrentLevel(buildingId)) <= 0
               && GetCurrentLevel(buildingId) < MaxLevel;
    }

    /// <summary>
    /// 计算指定建筑等级突破到下一阶段所需材料。
    /// </summary>
    public static (int matrixId, int matrixCount, int fragmentCount) GetBreakthroughCost(int buildingLevel) {
        int matrixId = GetCurrentProgressMatrixId();
        for (int i = 0; i < BreakthroughLevels.Length; i++) {
            if (BreakthroughLevels[i] == buildingLevel) {
                return (matrixId, BreakthroughMatrixCosts[i], BreakthroughFragmentCosts[i]);
            }
        }
        return (matrixId, 0, 0);
    }

    /// <summary>
    /// 读取指定建筑类型升到下一级所需经验。
    /// </summary>
    public static long GetRequiredExpForNextLevel(int buildingId) {
        return GetRequiredExpForNextLevelInternal(GetCurrentLevel(buildingId));
    }

    /// <summary>
    /// 给指定建筑类型增加经验并尝试自动升级。
    /// </summary>
    public static void AddBuildingExp(int buildingId, long amount) {
        int index = GetGrowthIndex(buildingId);
        if (index < 0 || amount <= 0) {
            return;
        }

        buildingExp[index] += amount;
        TryAutoLevelUp(buildingId);
    }

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        int count = r.ReadInt32();
        if (count == 6) {
            long[] legacyExp = new long[count];
            for (int i = 0; i < legacyExp.Length; i++) {
                legacyExp[i] = r.ReadInt64();
            }
            buildingExp[0] = legacyExp[0];
            buildingExp[1] = legacyExp[1];
            buildingExp[2] = legacyExp[3];
            buildingExp[3] = legacyExp[4];
            buildingExp[4] = legacyExp[5];
            return;
        }
        for (int i = 0; i < Math.Min(count, buildingExp.Length); i++) {
            buildingExp[i] = r.ReadInt64();
        }
        for (int i = buildingExp.Length; i < count; i++) {
            r.ReadInt64();
        }
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.Write(buildingExp.Length);
        for (int i = 0; i < buildingExp.Length; i++) {
            w.Write(buildingExp[i]);
        }
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        Array.Clear(buildingExp, 0, buildingExp.Length);
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

    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
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

    /// <summary>
    /// 判断该建筑是否已启用流动输入增产加成。
    /// </summary>
    public static bool EnableFluidEnhancement(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFluidEnhancement,
            IFE矿物复制塔 => MineralReplicationTower.EnableFluidEnhancement,
            IFE转化塔 => ConversionTower.EnableFluidEnhancement,
            IFE精馏塔 => RectificationTower.EnableFluidEnhancement,
            _ => false
        };
    }

    /// <summary>
    /// 读取该建筑当前允许的分馏处理堆叠上限。
    /// </summary>
    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.MaxStack,
            IFE矿物复制塔 => MineralReplicationTower.MaxStack,
            IFE转化塔 => ConversionTower.MaxStack,
            IFE精馏塔 => RectificationTower.MaxStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxStack,
            _ => 1
        };
    }

    /// <summary>
    /// 读取该建筑当前每 tick 工作能耗。
    /// </summary>
    public static long workEnergyPerTick(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            IFE精馏塔 => RectificationTower.workEnergyPerTick,
            IFE行星内物流交互站 => PlanetaryInteractionStation.workEnergyPerTick,
            IFE星际物流交互站 => InterstellarInteractionStation.workEnergyPerTick,
            _ => LDB.models.Select(M分馏塔).prefabDesc.workEnergyPerTick
        };
    }

    /// <summary>
    /// 读取该建筑当前每 tick 待机能耗。
    /// </summary>
    public static long idleEnergyPerTick(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.idleEnergyPerTick;
            case IFE矿物复制塔:
                return MineralReplicationTower.idleEnergyPerTick;
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

    /// <summary>
    /// 读取该建筑当前能耗倍率。
    /// </summary>
    public static float EnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnergyRatio,
            IFE矿物复制塔 => MineralReplicationTower.EnergyRatio,
            IFE转化塔 => ConversionTower.EnergyRatio,
            IFE精馏塔 => RectificationTower.EnergyRatio,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 读取该建筑当前交互能耗倍率。
    /// </summary>
    public static float InteractEnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.InteractEnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.InteractEnergyRatio,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 读取该建筑当前增产点倍率。
    /// </summary>
    public static float PlrRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.PlrRatio,
            IFE矿物复制塔 => MineralReplicationTower.PlrRatio,
            IFE转化塔 => ConversionTower.PlrRatio,
            IFE精馏塔 => RectificationTower.PlrRatio,
            _ => 1.0f
        };
    }
}
