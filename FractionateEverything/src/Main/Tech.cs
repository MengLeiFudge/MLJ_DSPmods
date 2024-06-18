using CommonAPI.Systems;
using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything.Main {
    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static class Tech {
        static readonly List<TechProto> techs = [];

        public static void AddTechs() {
            //第一页
            AddTechFractionators();
            //第二页
            MoveOriTechs();
            //todo: 添加无限科技，提升增产塔效果
            AddTechFractionatorIntegrate();
            //这里先Preload一下，防止PreAdd结束后LDBTool报错
            //此时字符串翻译还未添加，所以PostAdd仍需要执行一次Preload
            PreloadAll();
        }

        private static void AddTechFractionators() {
            var tech1 = ProtoRegistry.RegisterTech(TFE精准分馏,
                "T精准分馏", "精准分馏描述", "精准分馏结果",
                "",//"Assets/fractionatebaseicon/tech精准分馏",//todo:制作图标
                GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
                [I电磁矩阵], [20], 18000,
                [RFE精准分馏塔],
                GenesisBook.Enable ? new(9, -43) : new(13, 17)
            );
            tech1.PreTechsImplicit = [T自动化冶金, T电磁矩阵];
            tech1.AddItems = [IFE精准分馏塔];
            tech1.AddItemCounts = [5];
            techs.Add(tech1);

            var tech2 = ProtoRegistry.RegisterTech(TFE建筑极速分馏,
                "T建筑极速分馏", "建筑极速分馏描述", "建筑极速分馏结果",
                "",//"Assets/fractionatebaseicon/tech建筑极速分馏",//todo:制作图标
                [tech1.ID],
                [I电磁矩阵], [20], 36000,
                [RFE建筑极速分馏塔],
                GenesisBook.Enable ? new(13, -43) : new(17, 17)
            );
            tech2.PreTechsImplicit = [T改良物流系统];
            tech2.AddItems = [IFE建筑极速分馏塔];
            tech2.AddItemCounts = [3];
            techs.Add(tech2);

            var tech3 = ProtoRegistry.RegisterTech(TFE垃圾回收,
                "T垃圾回收", "垃圾回收描述", "垃圾回收结果",
                "",//"Assets/fractionatebaseicon/tech垃圾回收",//todo:制作图标
                [tech2.ID],
                [I电磁矩阵, I能量矩阵], [20, 30], 36000,
                [RFE垃圾回收分馏塔],
                GenesisBook.Enable ? new(17, -43) : new(21, 17)
            );
            tech3.AddItems = [IFE垃圾回收分馏塔];
            tech3.AddItemCounts = [1];
            techs.Add(tech3);

            TechProto tech4;
            if (GenesisBook.Enable) {
                tech4 = LDB.techs.Select(T微型粒子对撞机_GB粒子对撞机);
                tech4.Name = "T粒子对撞与通用分馏";
                tech4.Desc = "粒子对撞与通用分馏描述";
                tech4.Conclusion = "粒子对撞与通用分馏结果";
                tech4.UnlockRecipes = [..tech4.UnlockRecipes, RFE通用分馏塔];
            }
            else {
                tech4 = LDB.techs.Select(T重氢分馏_GB强相互作用力材料_FE通用分馏);
                tech4.Name = "T通用分馏";
                tech4.Desc = "通用分馏描述";
                tech4.Conclusion = "通用分馏结果";
            }
            tech4.PreTechsImplicit = [..tech4.PreTechsImplicit, tech3.ID];
            tech4.AddItems = [..tech4.AddItems, I分馏塔_FE通用分馏塔];
            tech4.AddItemCounts = [..tech4.AddItemCounts, 1];
            techs.Add(tech4);

            var tech5 = ProtoRegistry.RegisterTech(TFE增产点数聚集,
                "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果",
                "Assets/fractionatebaseicon/tech增产点数聚集",
                GenesisBook.Enable ? [T增产剂MkI_GB物品增产] : [T增产剂MkIII_GB人造恒星MKI],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 6, 4, 4], 360000,
                [RFE点数聚集分馏塔],
                GenesisBook.Enable ? new(29, 29) : new(45, -11)
            );
            tech5.PreTechsImplicit = [tech4.ID];
            tech5.AddItems = [..tech4.AddItems, I分馏塔_FE通用分馏塔];
            tech5.AddItemCounts = [..tech4.AddItemCounts, 1];
            techs.Add(tech5);

            var tech6 = ProtoRegistry.RegisterTech(TFE增产分馏,
                "T增产分馏", "增产分馏描述", "增产分馏结果",
                "Assets/fractionatebaseicon/tech增产分馏",
                GenesisBook.Enable ? [] : [tech5.ID],
                [I黑雾矩阵], [200], 36000,
                [RFE增产分馏塔],
                GenesisBook.Enable ? new(45, -23) : new(49, -11)
            );
            tech6.IsHiddenTech = true;
            tech6.PreItem = [I黑雾矩阵, I物质重组器, I量子芯片];
            tech6.PreTechsImplicit = GenesisBook.Enable ? [tech5.ID, T量子芯片] : [T量子芯片];
            techs.Add(tech6);
        }

        /// <summary>
        /// 将第二页部分科技下移一行，为增产科技腾出来位置
        /// </summary>
        private static void MoveOriTechs() {

        }

        private static void AddTechFractionatorIntegrate() {
            var tech3807 = ProtoRegistry.RegisterTech(TFE分馏塔流动输出集装,
                "T分馏塔流动输出集装", "分馏塔流动输出集装描述", "分馏塔流动输出集装结果",
                LDB.techs.Select(T运输站集装物流 + 2).IconPath,
                [],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
                [], new(45, -27));
            tech3807.Name = "T分馏塔流动输出集装";
            tech3807.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
            techs.Add(tech3807);

            var tech3804 = ProtoRegistry.RegisterTech(TFE分馏塔产物输出集装,
                "T分馏塔产物输出集装1", "分馏塔产物输出集装描述1", "分馏塔产物输出集装结果1",
                LDB.techs.Select(T运输站集装物流).IconPath,
                [],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
                [], new(37, -31));
            tech3804.Name = "T分馏塔产物输出集装";
            tech3804.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
            tech3804.Level = 1;
            tech3804.MaxLevel = 1;
            techs.Add(tech3804);

            var tech3805 = ProtoRegistry.RegisterTech(TFE分馏塔产物输出集装 + 1,
                "T分馏塔产物输出集装2", "分馏塔产物输出集装描述2", "分馏塔产物输出集装结果2",
                LDB.techs.Select(T运输站集装物流 + 1).IconPath,
                [tech3804.ID],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 8, 8, 8], 360000,
                [], new(41, -31));
            tech3805.Name = "T分馏塔产物输出集装";
            tech3805.Level = 2;
            tech3805.MaxLevel = 2;
            techs.Add(tech3805);

            var tech3806 = ProtoRegistry.RegisterTech(TFE分馏塔产物输出集装 + 2,
                "T分馏塔产物输出集装3", "分馏塔产物输出集装描述2", "分馏塔产物输出集装结果2",
                LDB.techs.Select(T运输站集装物流 + 2).IconPath,
                [tech3805.ID],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
                [], new(45, -31));
            tech3806.Name = "T分馏塔产物输出集装";
            tech3806.Level = 3;
            tech3806.MaxLevel = 3;
            techs.Add(tech3806);
        }

        public static void PreloadAll() {
            foreach (var tech in techs) {
                tech.Preload();
                tech.PreTechsImplicit = tech.PreTechsImplicit.Except(tech.PreTechs).ToArray();
                tech.Preload2();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TechProto), "UnlockFunctionText")]
        public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
            switch (__instance.ID) {
                case TFE分馏塔流动输出集装:
                    __result = "+3" + "分馏塔流动输出集装等级".Translate() + "\r\n";
                    return false;
                case >= TFE分馏塔产物输出集装 and <= TFE分馏塔产物输出集装 + 2:
                    __result = "+1" + "分馏塔产物输出集装等级".Translate() + "\r\n";
                    return false;
            }
            return true;
        }
    }
}
