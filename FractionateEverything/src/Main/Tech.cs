using CommonAPI.Systems;
using FractionateEverything.Compatibility;
using HarmonyLib;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything.Main {
    public static class Tech {
        public static TechProto tech增产点数聚集;
        public static TechProto tech增产分馏;

        public static void AddTechs() {
            //第一页
            AddTech增产点数聚集();
            AddTech增产分馏();
            //第二页
            MoveOriTechs();
            //todo: 添加无限科技，提升增产塔效果
            AddTech分馏塔产物集装物流();
        }

        private static void AddTech增产点数聚集() {
            tech增产点数聚集 = ProtoRegistry.RegisterTech(
                TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果",
                "Assets/fracicons/tech增产点数聚集",
                GenesisBook.Enable ? [T增产剂MkI_GB物品增产] : [T增产剂MkIII_GB人造恒星MKI, T信息矩阵],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 6, 4, 4], 360000,
                [RFE点数聚集分馏塔],
                GenesisBook.Enable ? new(29, 29) : new(45, -11)
            );
            tech增产点数聚集.ID = TFE增产点数聚集;
            tech增产点数聚集.PreTechsImplicit = [T信息矩阵];
            //Preload2之后，unlockRecipeArray才不是null，这样LDBTool添加的时候不会报错
            tech增产点数聚集.Preload();
            tech增产点数聚集.Preload2();
        }

        private static void AddTech增产分馏() {
            tech增产分馏 = ProtoRegistry.RegisterTech(
                TFE增产分馏, "T增产分馏", "增产分馏描述", "增产分馏结果",
                "Assets/fracicons/tech增产分馏",
                GenesisBook.Enable ? [] : [TFE增产点数聚集],
                [I黑雾矩阵], [200], 36000,
                [RFE增产分馏塔],
                GenesisBook.Enable ? new(45, -23) : new(49, -11)
            );
            tech增产分馏.ID = TFE增产分馏;
            tech增产分馏.IsHiddenTech = true;
            tech增产分馏.PreItem = [I黑雾矩阵, I量子芯片];
            tech增产分馏.PreTechsImplicit = [TFE增产点数聚集, T量子芯片];
            //Preload2之后，unlockRecipeArray才不是null，这样LDBTool添加的时候不会报错
            tech增产分馏.Preload();
            tech增产分馏.Preload2();
        }

        /// <summary>
        /// 将第二页部分科技下移一行，为增产科技腾出来位置
        /// </summary>
        private static void MoveOriTechs() {

        }

        private static void AddTech分馏塔产物集装物流() {
            var tech3804 = ProtoRegistry.RegisterTech(TFE分馏塔产物集装物流,
                "T分馏塔产物集装物流1", "分馏塔产物集装物流描述1", "分馏塔产物集装物流结果1",
                LDB.techs.Select(T运输站集装物流).IconPath, [],
                [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
                [], new(37, -31));
            tech3804.ID = TFE分馏塔产物集装物流;
            tech3804.Name = "T分馏塔产物集装物流";
            tech3804.Level = 1;
            tech3804.PreTechsImplicit = [GenesisBook.Enable ? TGB集装物流系统 : T集装物流系统_GB物品仓储];
            var tech3805 = ProtoRegistry.RegisterTech(TFE分馏塔产物集装物流 + 1,
                "T分馏塔产物集装物流2", "分馏塔产物集装物流描述2", "分馏塔产物集装物流结果2",
                LDB.techs.Select(T运输站集装物流 + 1).IconPath, [TFE分馏塔产物集装物流],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 8, 8, 8], 360000,
                [], new(41, -31));
            tech3805.ID = TFE分馏塔产物集装物流 + 1;
            tech3805.Name = "T分馏塔产物集装物流";
            tech3805.Level = 2;
            var tech3806 = ProtoRegistry.RegisterTech(TFE分馏塔产物集装物流 + 2,
                "T分馏塔产物集装物流3", "分馏塔产物集装物流描述2", "分馏塔产物集装物流结果2",
                LDB.techs.Select(T运输站集装物流 + 2).IconPath, [TFE分馏塔产物集装物流 + 1],
                [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
                [], new(45, -31));
            tech3806.ID = TFE分馏塔产物集装物流 + 2;
            tech3806.Name = "T分馏塔产物集装物流";
            tech3806.Level = 3;
            tech3806.PreTechsMax = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TechProto), "UnlockFunctionText")]
        public static bool UnlockFunctionTextPrePatch(ref TechProto __instance, ref string __result) {
            if (__instance.ID is < TFE分馏塔产物集装物流 or > TFE分馏塔产物集装物流 + 2) {
                return true;
            }
            __result = "+1" + "分馏塔产物集装等级".Translate();
            return false;
        }
    }
}
