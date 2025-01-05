using CommonAPI.Systems;
using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;

namespace FractionateEverything.Main {
    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static class FracTechManager {
        static readonly List<TechProto> techs = [];

        public static void AddTechs() {
            //第一页
            AddTechFractionators();
            //第二页
            AddTechFractionatorIntegrate();
            //这里先Preload一下，防止PreAdd结束后LDBTool报错
            //此时字符串翻译还未添加，所以PostAdd仍需要执行一次Preload
            PreloadAll();
        }

        private static void AddTechFractionators() {
            var tech1621 = ProtoRegistry.RegisterTech(TFE自然资源分馏,
                "T自然资源分馏", "自然资源分馏描述", "自然资源分馏结果",
                "Assets/fracicons/tech自然资源分馏",
                GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
                [I电磁矩阵], [20], 3600,
                [RFE自然资源分馏塔],
                GenesisBook.Enable ? new(9, -43) : new(13, 17)
            );
            tech1621.PreTechsImplicit = GenesisBook.Enable ? [T电磁矩阵, TGB基础机械组件] : [T电磁矩阵, T自动化冶金];
            tech1621.AddItems = [IFE自然资源分馏塔];
            tech1621.AddItemCounts = [3];
            techs.Add(tech1621);

            var tech1622 = ProtoRegistry.RegisterTech(TFE升降级分馏,
                "T升降级分馏", "升降级分馏描述", "升降级分馏结果",
                "Assets/fracicons/tech升降级分馏",
                [tech1621.ID],
                [I电磁矩阵], [20], 18000,
                [RFE升级分馏塔, RFE降级分馏塔],
                GenesisBook.Enable ? new(13, -43) : new(17, 17)
            );
            tech1622.PreTechsImplicit = [tech1621.ID, T钢材冶炼];
            tech1622.AddItems = [IFE升级分馏塔, IFE降级分馏塔];
            tech1622.AddItemCounts = [1, 1];
            techs.Add(tech1622);

            var tech1623 = ProtoRegistry.RegisterTech(TFE垃圾回收,
                "T垃圾回收", "垃圾回收描述", "垃圾回收结果",
                "Assets/fracicons/tech垃圾回收",
                [tech1622.ID],
                [I电磁矩阵, I能量矩阵], [20, 30], 36000,
                [RFE垃圾回收分馏塔],
                GenesisBook.Enable ? new(17, -43) : new(21, 17)
            );
            tech1623.PreTechsImplicit = GenesisBook.Enable
                ? [tech1622.ID, T能量矩阵, TGB先进机械组件, T高强度钛合金, T高强度玻璃, T处理器]
                : [tech1622.ID, T能量矩阵, T处理器];
            tech1623.AddItems = [IFE垃圾回收分馏塔];
            tech1623.AddItemCounts = [1];
            techs.Add(tech1623);

            var tech1159 = ProtoRegistry.RegisterTech(TFE增产点数聚集,
                "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果",
                "Assets/fracicons/tech增产点数聚集",
                GenesisBook.Enable ? [T增产剂MkI_GB物品增产] : [T增产剂MkIII_GB人造恒星],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 12, 4], 720000,
                [RFE点数聚集分馏塔],
                GenesisBook.Enable ? new(29, 29) : new(45, -11)
            );
            tech1159.PreTechsImplicit = GenesisBook.Enable
                ? [tech1623.ID, T结构矩阵, TGB尖端机械组件, TGB钨强化金属, TGB钨强化玻璃, T量子芯片]
                : [tech1623.ID, T结构矩阵, T高强度钛合金, T应用型超导体, T高强度玻璃];
            tech1159.PreTechsImplicit = [];
            techs.Add(tech1159);

            var tech1908 = ProtoRegistry.RegisterTech(TFE增产分馏,
                "T增产分馏", "增产分馏描述", "增产分馏结果",
                "Assets/fracicons/tech增产分馏",
                GenesisBook.Enable ? [] : [tech1159.ID],
                [I黑雾矩阵], [200], 36000,
                [RFE增产分馏塔],
                GenesisBook.Enable ? new(45, -23) : new(49, -11)
            );
            tech1908.IsHiddenTech = true;
            //前置物品仅需物质重组器，只要掉落该物品，该科技就为可见状态
            tech1908.PreItem = [I物质重组器];
            tech1908.PreTechsImplicit = GenesisBook.Enable
                ? [tech1159.ID, T信息矩阵, T引力矩阵, T重氢分馏_GB强相互作用力材料, TGB家园世界虚拟技术革新]
                : [tech1159.ID, T信息矩阵, T引力矩阵, T粒子可控, T波函数干扰, T量子芯片];
            techs.Add(tech1908);
        }

        private static void AddTechFractionatorIntegrate() {
            var tech3807 = ProtoRegistry.RegisterTech(TFE分馏流动输出集装,
                "T分馏流动输出集装", "分馏流动输出集装描述", "分馏流动输出集装结果",
                LDB.techs.Select(T运输站集装物流 + 2).IconPath,
                [],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
                [], new(37, -27));
            tech3807.Name = "T分馏流动输出集装";
            tech3807.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
            techs.Add(tech3807);

            var tech3804 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装,
                "T分馏产物输出集装1", "分馏产物输出集装描述1", "分馏产物输出集装结果1",
                LDB.techs.Select(T运输站集装物流).IconPath,
                [],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
                [], new(37, -31));
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
                [], new(41, -31));
            tech3805.Name = "T分馏产物输出集装";
            tech3805.Level = 2;
            tech3805.MaxLevel = 2;
            techs.Add(tech3805);

            var tech3806 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装 + 2,
                "T分馏产物输出集装3", "分馏产物输出集装描述2", "分馏产物输出集装结果2",
                LDB.techs.Select(T运输站集装物流 + 2).IconPath,
                [tech3805.ID],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
                [], new(45, -31));
            tech3806.Name = "T分馏产物输出集装";
            tech3806.Level = 3;
            tech3806.MaxLevel = 3;
            techs.Add(tech3806);

            var tech3808 = ProtoRegistry.RegisterTech(TFE分馏永动,
                "T分馏永动", "分馏永动描述", "分馏永动结果",
                Tech1134IconPath,
                [],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
                [], new(45, -27));
            tech3808.Name = "T分馏永动";
            tech3808.PreTechsImplicit = [tech3806.ID];
            techs.Add(tech3808);
        }

        public static void PreloadAll() {
            foreach (var tech in techs) {
                tech.Preload();
                tech.PreTechsImplicit = tech.PreTechsImplicit.Except(tech.PreTechs).ToArray();
                tech.Preload2();
            }
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
    }
}
