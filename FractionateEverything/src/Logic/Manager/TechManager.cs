using CommonAPI.Systems;
using FE.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using static FE.Utils.ProtoID;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static class TechManager {
    static readonly List<TechProto> techs = [];

    public static void AddTechs() {
        //第一页
        AddTechFractionators();
        //第二页
        AddTechFractionatorIntegrate();
    }

    /// <summary>
    /// 添加所有科技。对于科技的位置，x向右y向下，间距固定为4
    /// </summary>
    private static void AddTechFractionators() {
        //原胚、交互塔
        var tech1750 = ProtoRegistry.RegisterTech(TFE分馏原胚,
            "T分馏原胚", "分馏原胚描述", "分馏原胚结果",
            "Assets/fracicons/tech分馏原胚",
            GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
            [I电磁矩阵], [10], 3600,
            [IFE分馏原胚普通, IFE分馏原胚精良, IFE分馏原胚稀有, IFE分馏原胚史诗, IFE分馏原胚传说, IFE分馏原胚定向],
            GenesisBook.Enable ? new(9, -47) : new(9, -47)
        );
        tech1750.AddItems = [IFE分馏原胚定向];
        tech1750.AddItemCounts = [10];
        techs.Add(tech1750);

        var tech1751 = ProtoRegistry.RegisterTech(TFE交互塔,
            "T交互塔", "交互塔描述", "交互塔结果",
            "Assets/fracicons/tech交互塔",
            [tech1750.ID],
            [IFE交互塔], [1], 3600,
            [RFE交互塔],
            GenesisBook.Enable ? new(9, -47) : new(13, -47)
        );
        tech1751.AddItems = [IFE交互塔];
        tech1751.AddItemCounts = [11];
        techs.Add(tech1751);

        //矿物复制塔、点数聚集塔、量子复制塔
        var tech1752 = ProtoRegistry.RegisterTech(TFE矿物复制塔,
            "T矿物复制塔", "矿物复制塔描述", "矿物复制塔结果",
            "Assets/fracicons/tech矿物复制塔",
            [tech1751.ID],
            [IFE矿物复制塔], [1], 3600,
            [RFE矿物复制塔],
            GenesisBook.Enable ? new(9, -47) : new(17, -47)
        );
        tech1752.AddItems = [IFE矿物复制塔];
        tech1752.AddItemCounts = [11];
        techs.Add(tech1752);

        var tech1754 = ProtoRegistry.RegisterTech(TFE点数聚集塔,
            "T点数聚集塔", "点数聚集塔描述", "点数聚集塔结果",
            "Assets/fracicons/tech点数聚集塔",
            [tech1751.ID],
            [IFE点数聚集塔], [1], 3600,
            [RFE点数聚集塔],
            GenesisBook.Enable ? new(9, -47) : new(21, -47)
        );
        tech1754.AddItems = [IFE点数聚集塔];
        tech1754.AddItemCounts = [11];
        techs.Add(tech1754);

        var tech1755 = ProtoRegistry.RegisterTech(TFE量子复制塔,
            "T量子复制塔", "量子复制塔描述", "量子复制塔结果",
            "Assets/fracicons/tech量子复制塔",
            [tech1751.ID],
            [IFE量子复制塔, I黑雾矩阵], [1, 200], 36000,
            [RFE量子复制塔],
            GenesisBook.Enable ? new(9, -47) : new(25, -47)
        );
        tech1755.IsHiddenTech = true;
        //前置物品仅需物质重组器，只要掉落该物品，该科技就为可见状态
        tech1755.PreItem = [I物质重组器];
        tech1755.PreTechsImplicit = GenesisBook.Enable
            ? [T信息矩阵, T引力矩阵, T重氢分馏_GB强相互作用力材料, TGB家园世界虚拟技术革新]
            : [T信息矩阵, T引力矩阵, T粒子可控, T波函数干扰, T量子芯片];
        tech1755.AddItems = [IFE量子复制塔];
        tech1755.AddItemCounts = [11];
        techs.Add(tech1755);


        //点金塔、分解塔、转化塔
        var tech1756 = ProtoRegistry.RegisterTech(TFE点金塔,
            "T点金塔", "点金塔描述", "点金塔结果",
            "Assets/fracicons/tech点金塔",
            [tech1751.ID],
            [IFE点金塔], [1], 3600,
            [RFE点金塔],
            GenesisBook.Enable ? new(13, -47) : new(17, -51)
        );
        tech1756.AddItems = [IFE点金塔];
        tech1756.AddItemCounts = [11];
        techs.Add(tech1756);

        var tech1757 = ProtoRegistry.RegisterTech(TFE分解塔,
            "T分解塔", "分解塔描述", "分解塔结果",
            "Assets/fracicons/tech分解塔",
            [tech1756.ID],
            [IFE分解塔], [1], 3600,
            [RFE分解塔],
            GenesisBook.Enable ? new(13, -47) : new(21, -51)
        );
        tech1757.AddItems = [IFE分解塔];
        tech1757.AddItemCounts = [11];
        techs.Add(tech1757);

        var tech1758 = ProtoRegistry.RegisterTech(TFE转化塔,
            "T转化塔", "转化塔描述", "转化塔结果",
            "Assets/fracicons/tech转化塔",
            [tech1757.ID],
            [IFE转化塔], [1], 3600,
            [RFE转化塔],
            GenesisBook.Enable ? new(13, -47) : new(25, -51)
        );
        tech1758.AddItems = [IFE转化塔];
        tech1758.AddItemCounts = [11];
        techs.Add(tech1758);
    }

    private static void AddTechFractionatorIntegrate() {
        var tech3807 = ProtoRegistry.RegisterTech(TFE分馏流动输出集装,
            "T分馏流动输出集装", "分馏流动输出集装描述", "分馏流动输出集装结果",
            LDB.techs.Select(T运输站集装物流 + 2).IconPath,
            [],
            [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
            [], new(37, -31));
        tech3807.Name = "T分馏流动输出集装";
        tech3807.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
        techs.Add(tech3807);

        var tech3804 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装,
            "T分馏产物输出集装1", "分馏产物输出集装描述1", "分馏产物输出集装结果1",
            LDB.techs.Select(T运输站集装物流).IconPath,
            [],
            [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
            [], new(37, -35));
        tech3804.Name = "T分馏产物输出集装";
        tech3804.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
        tech3804.Level = 1;
        tech3804.MaxLevel = 1;
        techs.Add(tech3804);

        var tech3805 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装 + 1,
            "T分馏产物输出集装2", "分馏产物输出集装描述2", "分馏产物输出集装结果2",
            LDB.techs.Select(T运输站集装物流 + 1).IconPath,
            [tech3804.ID],
            [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 8, 8, 8], 360000,
            [], new(41, -35));
        tech3805.Name = "T分馏产物输出集装";
        tech3805.Level = 2;
        tech3805.MaxLevel = 2;
        techs.Add(tech3805);

        var tech3806 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装 + 2,
            "T分馏产物输出集装3", "分馏产物输出集装描述2", "分馏产物输出集装结果2",
            LDB.techs.Select(T运输站集装物流 + 2).IconPath,
            [tech3805.ID],
            [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
            [], new(45, -35));
        tech3806.Name = "T分馏产物输出集装";
        tech3806.Level = 3;
        tech3806.MaxLevel = 3;
        techs.Add(tech3806);

        var tech3808 = ProtoRegistry.RegisterTech(TFE分馏永动,
            "T分馏永动", "分馏永动描述", "分馏永动结果",
            "Assets/fracicons/tech分馏永动",
            [],
            [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
            [], new(45, -31));
        tech3808.Name = "T分馏永动";
        tech3808.PreTechsImplicit = [tech3806.ID];
        techs.Add(tech3808);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
        switch (__instance.ID) {
            case TFE分馏流动输出集装:
                __result = "+3" + "分馏流动输出集装等级".Translate() + "\r\n";
                return false;
            case >= TFE分馏产物输出集装 and <= TFE分馏产物输出集装 + 2:
                __result = "+1" + "分馏产物输出集装等级".Translate() + "\r\n";
                return false;
            case TFE分馏永动:
                __result = "分馏持续运行".Translate() + "\r\n";
                return false;
        }
        return true;
    }

    #region 一键解锁

    /// <summary>
    /// 处于沙盒模式下时，在点击“解锁全部”按钮后额外执行的操作
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    public static void UITechTree_Do1KeyUnlock_Postfix() {
        //解锁所有分馏配方
        RecipeManager.UnlockAll();

        // int unlockCount = 0;
        // foreach (BaseRecipe r in naturalResourceRecipeList) {
        //     if (!r.IsUnlocked) {
        //         unlockCount++;
        //         r.Unlock();
        //     }
        // }
        // if (unlockCount > 0) {
        //     LogInfo($"Unlocked {naturalResourceRecipeList.Count} natural resource recipes.");
        // }
        // unlockCount = 0;
        // foreach (BaseRecipe r in upgradeRecipeList) {
        //     if (!r.IsUnlocked) {
        //         unlockCount++;
        //         r.Unlock();
        //     }
        // }
        // if (unlockCount > 0) {
        //     LogInfo($"Unlocked {upgradeRecipeList.Count} upgrade recipes.");
        // }
        // unlockCount = 0;
        // foreach (BaseRecipe r in downgradeRecipeList) {
        //     if (!r.IsUnlocked) {
        //         unlockCount++;
        //         r.Unlock();
        //     }
        // }
        // if (unlockCount > 0) {
        //     LogInfo($"Unlocked {downgradeRecipeList.Count} downgrade recipes.");
        // }
        // unlockCount = 0;
        // foreach (BaseRecipe r in pointsAggregateRecipeList) {
        //     if (!r.IsUnlocked) {
        //         unlockCount++;
        //         r.Unlock();
        //     }
        // }
        // if (unlockCount > 0) {
        //     LogInfo($"Unlocked {pointsAggregateRecipeList.Count} points aggregate recipes.");
        // }
        // unlockCount = 0;
        // foreach (BaseRecipe r in increaseRecipeList) {
        //     if (!r.IsUnlocked) {
        //         unlockCount++;
        //         r.Unlock();
        //     }
        // }
        // if (unlockCount > 0) {
        //     LogInfo($"Unlocked {increaseRecipeList.Count} increase recipes.");
        // }
    }

    #endregion
}
