using static FE.Utils.Utils;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using UnityEngine;
using FE.Logic.Items;
using System;
using FE.Compatibility.Nebula;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;
using System.IO;
using FE.Logic.Civilization.Presentation;
using UnityEngine.UI;

namespace FE.Logic.Civilization {

    #region TechManager

    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static partial class TechManager {
        /// <summary>
        /// 注册隐藏科技、文明恢复提示和启动信号 HUD 使用的本地化文本。
        /// </summary>
        public static void AddTranslations() {
            Register("T分馏数据中心", "Fractionation Data Centre", "分馏数据中心");
            Register("分馏数据中心描述",
                $"An abnormal old-civilization signal has been recovered. The starter kit contains the data-centre communication channel, initial tower protos, and one Cultivation Tower. Press {"Shift + F".WithColor(Orange)} after recovery to connect to the Fractionation Data Centre.",
                $"已回收异常旧文明信号中的启动套件。套件包含数据中心通信方式、初始分馏塔原胚和一座培养塔。回收完成后按 {"Shift + F".WithColor(Orange)} 即可连接到分馏数据中心。");
            Register("分馏数据中心结果",
                "Data-centre communication has been restored. The starter kit cargo is available.",
                "数据中心通信已恢复，启动套件货物已就绪。");
            Register("允许连接到分馏数据中心", "Restore data-centre communication", "恢复数据中心通信");
            Register("给予一些分馏塔原胚", "Recover starter tower protos", "回收启动套件原胚");
            Register("旧文明启动套件回收提示", "Old-civilization starter kit recovered.", "已回收旧文明启动套件，数据中心通信已建立。");
            Register("异常旧文明信号出生星提示",
                "Abnormal old-civilization signal detected on the birth planet.",
                "出生星上探测到异常旧文明信号。");
            Register("异常旧文明信号距离提示",
                "Abnormal old-civilization signal: about {0} m to the east-northeast.",
                "异常旧文明信号：东北偏东方向约 {0} 米。");
            Register("启动信号HUD-标题", "Old-Civilization Signal", "异常旧文明信号");
            Register("启动信号HUD-返回出生星", "Return to the birth planet.", "请返回出生星定位信号。");
            Register("启动信号HUD-方向距离", "{0} · about {1} m", "{0} · 约{1}米");
            Register("启动信号方向-前", "ahead", "前");
            Register("启动信号方向-右前", "front-right", "右前");
            Register("启动信号方向-右", "right", "右");
            Register("启动信号方向-右后", "rear-right", "右后");
            Register("启动信号方向-后", "behind", "后");
            Register("启动信号方向-左后", "rear-left", "左后");
            Register("启动信号方向-左", "left", "左");
            Register("启动信号方向-左前", "front-left", "左前");
            Register("旧文明协议恢复提示", "Old-civilization protocol restored.", "旧文明协议已恢复。");
            Register("物流交互协议恢复提示", "Logistics interaction protocol restored.", "物流交互协议已恢复。");

            Register("T分馏塔原胚", "Fractionator Proto", "分馏塔原胚");
            Register("分馏塔原胚描述",
                "In the recovered fractionation system, towers are cultivated instead of crafted. A common proto produces a random tower lineage, while a lineage proto grows into its matching tower.",
                "在恢复后的分馏体系中，分馏塔通过培养而非制造获得。通用原胚会随机培养为四种塔型之一，专属原胚会稳定培养为对应塔型。");
            Register("分馏塔原胚结果",
                "Fractionator proto cultivation is now available for all four tower lineages.",
                "你已经了解了分馏塔原胚的相关信息，可以将分馏塔原胚培养为不同的分馏塔了。");
            Register("恢复全部培养配方", "Restore all cultivation recipes", "恢复全部培养配方");
            Register("给予一个培养塔", "Provide a Cultivation Tower");

            Register("T培养塔", "Cultivation Tower", "培养塔");
            Register("培养塔描述",
                $"The old-civilization protocol lets the Cultivation Tower convert physical items into data-centre records and recover selected records as items. The same tower also cultivates fractionator protos into tower types.\n\n{"Upload a Cultivation Tower to the Fractionation Data Centre to restore this protocol.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
                $"旧文明协议允许培养塔把实体物品转换为数据中心记录，也可以把选中的记录重新取回为实体物品。同时，培养塔也承担了培养分馏塔原胚的职责。\n\n{"将培养塔上传至分馏数据中心即可恢复此协议。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
            Register("培养塔结果",
                "The Cultivation Tower has been registered. It can now interact with production lines.",
                "培养塔已完成注册，现在可以与产线交互了。");
            Register("自动上传被扔掉的物品", "Automatically upload dropped items");
            Register("双击背包排序按钮，自动上传背包内物品",
                "Double-click the backpack sort button to automatically upload the items within the backpack");

            Register("T解析塔", "Analysis Tower", "解析塔");
            Register("解析塔描述",
                $"The Analysis Tower processes matrices and dark-fog samples into Frag used for protocol recovery. Memory is derived from Frag yield.\n\n{"Upload an Analysis Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
                $"解析塔处理矩阵与黑雾样本产出残片（Frag），用于协议恢复；记忆源点（Memory）按残片产量派生。\n\n{"将解析塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
            Register("解析塔结果",
                "The Analysis Tower has been registered. Matrices and dark-fog samples can now be processed into Frag.",
                "解析塔已完成注册；现在可以将矩阵与黑雾样本处理为残片。");

            Register("T复制塔", "Replication Tower", "复制塔");
            Register("复制塔描述",
                $"Registering a Replication Tower restores its control interface. Individual resource-replication recipes still require civilization protocol recovery.\n\n{"Upload a Replication Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
                $"注册复制塔会恢复其控制接口；具体资源复制配方仍需通过文明协议恢复。\n\n{"将复制塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
            Register("复制塔结果",
                "The Replication Tower has been registered. Recover individual recipe protocols through civilization analysis.",
                "复制塔已完成注册；请继续通过文明解析恢复具体配方协议。");
            Register("注册复制塔控制接口", "Register the Replication Tower control interface", "注册复制塔控制接口");

            Register("T转化塔", "Conversion Tower", "转化塔");
            Register("转化塔描述",
                $"Registering a Conversion Tower restores its control interface. Individual conversion recipes still require civilization protocol recovery.\n\n{"Upload a Conversion Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
                $"注册转化塔会恢复其控制接口；具体转化配方仍需通过文明协议恢复。\n\n{"将转化塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
            Register("转化塔结果",
                "The Conversion Tower has been registered. Recover individual recipe protocols through civilization analysis.",
                "转化塔已完成注册；请继续通过文明解析恢复具体配方协议。");


            Register("T行星内物流交互", "Planetary Logistics Interaction", "行星内物流交互");
            Register("行星内物流交互描述",
                "Planetary Logistics Interaction lets local logistics stations exchange items directly with the Fractionation Data Centre, reducing repeated manual hauling within the same planet.",
                "行星内物流交互协议允许本地物流站直接与分馏数据中心交换物品，减少同星球范围内的重复搬运与手动中转。");
            Register("行星内物流交互结果",
                "The planetary logistics interaction protocol has been restored. Local logistics stations can now interact with the Fractionation Data Centre.",
                "行星内物流交互协议已恢复，现在可以让本地物流站与分馏数据中心直接交互。");

            Register("T星际物流交互", "Interstellar Logistics Interaction", "星际物流交互");
            Register("星际物流交互描述",
                "Interstellar Logistics Interaction extends the same direct data-centre exchange to interstellar logistics stations, turning them into long-range item interaction hubs.",
                "星际物流交互协议把同样的直连交互能力扩展到星际物流站，使其成为跨星系的物资交互中枢。");
            Register("星际物流交互结果",
                "The interstellar logistics interaction protocol has been restored. Interstellar logistics stations can now interact with the Fractionation Data Centre.",
                "星际物流交互协议已恢复，现在可以让星际物流站与分馏数据中心直接交互。");
        }
    }

    #endregion // TechManager

    #region Techs

    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static partial class TechManager {
        public static void AddTechs() {
            var tech分馏数据中心 = ProtoRegistry.RegisterTech(
                TFE分馏数据中心, "T分馏数据中心", "分馏数据中心描述", "分馏数据中心结果", "Assets/fe/tech分馏数据中心",
                GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
                //注：哈希块是3600的x倍时，实际需要的物品数目为当前数目*x
                [I电磁矩阵], [10], 3600,
                [],
                GetTechPos(1, 0)
            );
            tech分馏数据中心.PreTechsImplicit = [T电磁矩阵];
            tech分馏数据中心.AddItems = [];
            tech分馏数据中心.AddItemCounts = [];
            tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
            tech分馏数据中心.PropertyItemCounts = [10];
            tech分馏数据中心.IconTag = "flsjzx";

            var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
                TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
                [TFE分馏数据中心],
                [], [], 3600,
                [],
                GetTechPos(1, 1)
            );
            tech分馏塔原胚.AddItems = [IFE培养塔, IFE通用原胚];
            tech分馏塔原胚.AddItemCounts = [1, 80];
            tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
            tech分馏塔原胚.PropertyItemCounts = [100];
            tech分馏塔原胚.IconTag = "tfltyp";

            var tech培养塔 = ProtoRegistry.RegisterTech(
                TFE培养塔, "T培养塔", "培养塔描述", "培养塔结果", "Assets/fe/tech物品交互",
                [],
                [], [], 3600000,
                [],
                GetTechPos(1, 2)
            );
            tech培养塔.PreTechsImplicit = [TFE分馏塔原胚];
            tech培养塔.PropertyOverrideItems = [I电磁矩阵];
            tech培养塔.PropertyItemCounts = [200];
            tech培养塔.IconTag = "twpjh";

            var tech解析塔 = ProtoRegistry.RegisterTech(
                TFE解析塔, "T解析塔", "解析塔描述", "解析塔结果", "Assets/fe/tech物品分解",
                [],
                [], [], 3600000,
                [],
                GetTechPos(1, 3)
            );
            tech解析塔.PreTechsImplicit = [TFE分馏塔原胚];
            tech解析塔.PropertyOverrideItems = [I电磁矩阵];
            tech解析塔.PropertyItemCounts = [200];
            tech解析塔.IconTag = "twpjl";

            var tech复制塔 = ProtoRegistry.RegisterTech(
                TFE复制塔, "T复制塔", "复制塔描述", "复制塔结果", "Assets/fe/tech资源复制",
                [],
                [], [], 3600000,
                [],
                GetTechPos(1, 4)
            );
            tech复制塔.PreTechsImplicit = [TFE分馏塔原胚];
            tech复制塔.PropertyOverrideItems = [I电磁矩阵];
            tech复制塔.PropertyItemCounts = [200];
            tech复制塔.IconTag = "tkwfz";

            var tech转化塔 = ProtoRegistry.RegisterTech(
                TFE转化塔, "T转化塔", "转化塔描述", "转化塔结果", "Assets/fe/tech物品转化",
                [],
                [], [], 3600000,
                [],
                GetTechPos(1, 5)
            );
            tech转化塔.PreTechsImplicit = [TFE分馏塔原胚];
            tech转化塔.PropertyOverrideItems = [I电磁矩阵];
            tech转化塔.PropertyItemCounts = [200];
            tech转化塔.IconTag = "twpzh";

            var tech行星物流系统 = LDB.techs.Select(T行星物流系统);
            var tech行星内物流交互 = ProtoRegistry.RegisterTech(
                TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果", tech行星物流系统.IconPath,
                [],
                [..tech行星物流系统.Items], [..tech行星物流系统.ItemPoints], tech行星物流系统.HashNeeded,
                [RFE行星内物流交互站],
                GetTechPos(1, 6)
            );
            tech行星内物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE培养塔, tech行星物流系统.ID];
            tech行星内物流交互.PropertyOverrideItems = [..tech行星物流系统.PropertyOverrideItems];
            tech行星内物流交互.PropertyItemCounts = [..tech行星物流系统.PropertyItemCounts];
            tech行星内物流交互.IconTag = "txxnjh";

            var tech星际物流系统 = LDB.techs.Select(T星际物流系统);
            var tech星际物流交互 = ProtoRegistry.RegisterTech(
                TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果", tech星际物流系统.IconPath,
                [],
                [..tech星际物流系统.Items], [..tech星际物流系统.ItemPoints], tech星际物流系统.HashNeeded,
                [RFE星际物流交互站],
                GetTechPos(1, 7)
            );
            tech星际物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE培养塔, tech星际物流系统.ID];
            tech星际物流交互.PropertyOverrideItems = [..tech星际物流系统.PropertyOverrideItems];
            tech星际物流交互.PropertyItemCounts = [..tech星际物流系统.PropertyItemCounts];
            tech星际物流交互.IconTag = "txjjh";
        }

        /// <summary>
        /// 根据输入的行列，生成科技所在位置。
        /// </summary>
        /// <param name="row">从0开始，数字越大越靠下</param>
        /// <param name="column">从0开始，数字越大越靠右</param>
        /// <returns></returns>
        private static Vector2 GetTechPos(int row, int column) {
            if (GenesisBook.Enable) {
                return new(9 + column * 4, -47 - row * 4);
            }
            if (OrbitalRing.Enable) {
                return new(8 + column * 4, -76 - row * 4);
            }
            return new(13 + column * 4, -67 - row * 4);
        }
    }

    #endregion // Techs

    #region MatrixProgress

    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static partial class TechManager {
        public static bool IsMatrixTierFullyResearched(int matrixId) {
            if (GameMain.history == null) {
                return false;
            }

            bool hasFiniteTech = false;
            foreach (TechProto tech in LDB.techs.dataArray) {
                if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                    continue;
                }
                if (tech.MaxLevel > 20) {
                    continue;
                }
                if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                    continue;
                }

                hasFiniteTech = true;
                if (!GameMain.history.TechUnlocked(tech.ID, true)) {
                    return false;
                }
            }

            return hasFiniteTech;
        }

        public static float GetMatrixTierResearchProgress(int matrixId) {
            if (GameMain.history == null) {
                return 0f;
            }

            int total = 0;
            int unlocked = 0;
            foreach (TechProto tech in LDB.techs.dataArray) {
                if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                    continue;
                }
                if (tech.MaxLevel > 20) {
                    continue;
                }
                if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                    continue;
                }

                total++;
                if (GameMain.history.TechUnlocked(tech.ID, true)) {
                    unlocked++;
                }
            }

            if (total <= 0) {
                return 0f;
            }
            return unlocked / (float)total;
        }

        /// <summary>
        /// 原版配方增强采用“落后一层”的矩阵层级开放规则：
        /// 只有下一层矩阵已解锁，且该层有限科技全部研究完成时，才开放低一层增强。
        /// </summary>
        public static bool IsVanillaEnhancementUnlockedForMatrix(int matrixId) {
            int stageIndex = ItemManager.GetMatrixStageIndex(matrixId);
            if (stageIndex < 0 || stageIndex >= ItemManager.MainProgressMatrixIds.Length) {
                return false;
            }

            int requiredIndex = Mathf.Min(stageIndex + 1, ItemManager.MainProgressMatrixIds.Length - 1);
            int requiredMatrixId = ItemManager.MainProgressMatrixIds[requiredIndex];
            if (GameMain.history == null || !GameMain.history.ItemUnlocked(requiredMatrixId)) {
                return false;
            }
            return IsMatrixTierFullyResearched(requiredMatrixId);
        }

        public static int GetHighestUnlockedVanillaEnhancementMatrix() {
            for (int i = ItemManager.MainProgressMatrixIds.Length - 1; i >= 0; i--) {
                int matrixId = ItemManager.MainProgressMatrixIds[i];
                if (IsVanillaEnhancementUnlockedForMatrix(matrixId)) {
                    return matrixId;
                }
            }

            return 0;
        }
    }

    #endregion // MatrixProgress

    #region RuntimePatches

    /// <summary>
    /// 添加科技后，需要Preload、Preload2。
    /// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
    /// </summary>
    public static partial class TechManager {
        private static readonly bool[] techUnlockFlags = new bool[4];

        public static void ResetTechUnlockFlags() {
            Array.Clear(techUnlockFlags, 0, techUnlockFlags.Length);
        }

        public static void CheckTechUnlockCondition(int itemId) {
            int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(itemId);
            if (index >= 0) {
                techUnlockFlags[index] = true;
            }
        }

        /// <summary>
        /// 对于所有恢复标记为 true 的分馏塔，恢复对应旧文明协议。
        /// 联机客户端不执行主机权威解锁，但仍驱动只读启动信号 HUD 与限频恢复请求。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.GameTick))]
        public static void Player_GameTick_Postfix() {
            if (NebulaMultiplayerModAPI.IsClient) {
                ResetTechUnlockFlags();
                StackingManager.SyncRuntimeState();
                CivilizationRecoveryManager.Tick();
                return;
            }

            for (int i = 0; i < techUnlockFlags.Length; i++) {
                if (techUnlockFlags[i]) {
                    int techId = GetActiveFractionatorUnlockTechId(i);
                    if (techId <= 0) {
                        techUnlockFlags[i] = false;
                        continue;
                    }
                    if (!GameMain.history.TechUnlocked(techId)) {
                        GameMain.history.UnlockTechUnlimited(techId, false);
                        CivilizationRecoveryManager.ShowProtocolRecoveredTip(techId);
                        techUnlockFlags[i] = false;
                    } else {
                        techUnlockFlags[i] = false;
                    }
                }
            }

            StackingManager.SyncRuntimeState();
            CivilizationRecoveryManager.Tick();
        }

        private static int GetActiveFractionatorUnlockTechId(int index) {
            return index switch {
                0 => TFE培养塔,
                1 => TFE解析塔,
                2 => TFE复制塔,
                3 => TFE转化塔,
                _ => 0,
            };
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
        public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
            if (__instance.ID == TFE分馏数据中心) {
                __result = $"{"允许连接到分馏数据中心".Translate()}\r\n"
                           + $"{"给予一些分馏塔原胚".Translate()}";
                return false;
            }
            if (__instance.ID == TFE分馏塔原胚) {
                __result = $"{"恢复全部培养配方".Translate()}\r\n"
                           + $"{"给予一个培养塔".Translate()}\r\n"
                           + $"{"给予一些分馏塔原胚".Translate()}";
                return false;
            }
            if (__instance.ID == TFE培养塔) {
                __result = $"{"自动上传被扔掉的物品".Translate()}\r\n"
                           + $"{"双击背包排序按钮，自动上传背包内物品".Translate()}";
                return false;
            }
            if (__instance.ID == TFE复制塔) {
                __result = $"{"注册复制塔控制接口".Translate()}";
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIGeneralTips), nameof(UIGeneralTips.OnTechUnlocked))]
        public static bool UIGeneralTips_OnTechUnlocked_Prefix(int techId) {
            return !CivilizationRecoveryManager.IsInternalRecoveryTech(techId);
        }
    }

    #endregion // RuntimePatches

    #region CivilizationRecoveryManager

    /// <summary>
    /// 将旧 FE 科技树入口迁移为文明恢复流程。
    /// 旧 TFE 科技 ID 继续作为存档、配方解锁和兼容状态使用，但不再作为玩家可研究的主脑科技树节点展示。
    /// </summary>
    public static class CivilizationRecoveryManager {
        /// <summary>
        /// 新存档开始后显示异常信号的最早游戏 tick；300 tick 约为 5 秒。
        /// </summary>
        private const long StarterSignalStartTick = 300L;

        /// <summary>
        /// 启动套件信号沿出生点切线方向偏移的地表弧长，单位为米。
        /// </summary>
        private const float StarterSignalDistance = 160f;

        /// <summary>
        /// 玩家触发启动套件回收的直线距离上限，单位为米。
        /// </summary>
        private const float StarterSignalRecoverRadius = 35f;

        /// <summary>
        /// 客户端向主机发送启动恢复请求的最小间隔（tick）。
        /// </summary>
        private const long StarterRecoveryRequestIntervalTicks = 60L;

        private static readonly int[] logisticsInteractionTechIds = [
            TFE行星内物流交互,
            TFE星际物流交互,
        ];

        private static readonly int[] vanillaLogisticsTechIds = [
            T行星物流系统,
            T星际物流系统,
        ];

        private static readonly int[] internalRecoveryTechIds = [
            TFE分馏数据中心,
            TFE分馏塔原胚,
            TFE培养塔,
            TFE解析塔,
            TFE复制塔,
            TFE转化塔,
            TFE行星内物流交互,
            TFE星际物流交互,
        ];

        private static readonly bool[] logisticsInteractionRecovered = new bool[logisticsInteractionTechIds.Length];
        private static bool starterKitRecovered;
        private static bool starterSignalNotified;

        /// <summary>
        /// 客户端上次发送启动恢复请求的 gameTick；切档时清零，避免跨档节流泄漏。
        /// </summary>
        private static long lastStarterRecoveryRequestTick = long.MinValue;

        public static bool HasDataCenterCommunication =>
            GameMain.history != null
            && (starterKitRecovered || GameMain.history.TechUnlocked(TFE分馏数据中心, true));

        public static void SuppressInternalTechTreeEntries() {
            foreach (int techId in internalRecoveryTechIds) {
                TechProto tech = LDB.techs.Select(techId);
                if (tech == null) {
                    continue;
                }

                tech.IsHiddenTech = true;
                tech.IsObsolete = true;
                tech.IsLabTech = false;
            }
        }

        public static bool IsInternalRecoveryTech(int techId) {
            foreach (int internalTechId in internalRecoveryTechIds) {
                if (internalTechId == techId) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 推进文明恢复生命周期。主机/单机执行权威解锁；联机客户端仅维护只读 HUD 与限频请求。
        /// </summary>
        public static void Tick() {
            if (GameMain.history == null || GameMain.mainPlayer == null) {
                return;
            }

            if (NebulaMultiplayerModAPI.IsClient) {
                TickClientStarterSignal();
                return;
            }

            RecoverStarterKit();
            RecoverLogisticsInteractionProtocols();
        }

        /// <summary>
        /// 导入文明恢复状态，并在读取前清理上一存档的常驻信号对象。
        /// </summary>
        /// <param name="r">当前存档的文明恢复块读取器。</param>
        public static void Import(BinaryReader r) {
            IntoOtherSave();
            r.ReadBlocks(
                ("StarterKitRecovered", br => starterKitRecovered = br.ReadBoolean()),
                ("StarterSignalNotified", br => starterSignalNotified = br.ReadBoolean()),
                // 旧开发态 MatrixStageSupplyRecovered 只读丢弃：不迁移、不发奖，避免 unknown-tag 日志。
                ("MatrixStageSupplyRecovered", br => {
                    int count = Math.Max(0, br.ReadInt32());
                    for (int i = 0; i < count; i++) {
                        _ = br.ReadBoolean();
                    }
                }),
                ("LogisticsInteractionRecovered", br => {
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++) {
                        bool recovered = br.ReadBoolean();
                        if (i < logisticsInteractionRecovered.Length) {
                            logisticsInteractionRecovered[i] = recovered;
                        }
                    }
                })
            );
            SyncRecoveredFlagsFromHistory();
        }

        public static void Export(BinaryWriter w) {
            w.WriteBlocks(
                ("StarterKitRecovered", bw => bw.Write(starterKitRecovered)),
                ("StarterSignalNotified", bw => bw.Write(starterSignalNotified)),
                ("LogisticsInteractionRecovered", bw => {
                    bw.Write(logisticsInteractionRecovered.Length);
                    foreach (bool recovered in logisticsInteractionRecovered) {
                        bw.Write(recovered);
                    }
                })
            );
        }

        /// <summary>
        /// 清空存档级文明恢复状态并销毁常驻信号 HUD，防止切档后泄漏旧目标。
        /// </summary>
        public static void IntoOtherSave() {
            starterKitRecovered = false;
            starterSignalNotified = false;
            lastStarterRecoveryRequestTick = long.MinValue;
            Array.Clear(logisticsInteractionRecovered, 0, logisticsInteractionRecovered.Length);
            StarterSignalHud.DestroyForSaveSwitch();
        }

        /// <summary>
        /// 联机结束时清零客户端恢复请求节流，避免跨会话泄漏。
        /// </summary>
        public static void ResetNetworkRequestThrottle() {
            lastStarterRecoveryRequestTick = long.MinValue;
        }

        /// <summary>
        /// 主机校验客户端上报的启动信号位置后，执行与本地近距离回收相同的单次权威恢复。
        /// 客户端不得调用；已恢复时直接返回。
        /// </summary>
        /// <param name="planetId">客户端声称所在星球 ID，必须等于出生星。</param>
        /// <param name="posX">客户端玩家世界坐标 X。</param>
        /// <param name="posY">客户端玩家世界坐标 Y。</param>
        /// <param name="posZ">客户端玩家世界坐标 Z。</param>
        /// <returns>本次是否新完成启动套件恢复。</returns>
        public static bool TryRecoverStarterKitFromClientRequest(int planetId, float posX, float posY, float posZ) {
            if (NebulaMultiplayerModAPI.IsClient) {
                return false;
            }
            if (starterKitRecovered || GameMain.history == null) {
                return false;
            }
            if (GameMain.gameTick < StarterSignalStartTick) {
                return false;
            }
            if (!IsFiniteCoordinate(posX) || !IsFiniteCoordinate(posY) || !IsFiniteCoordinate(posZ)) {
                return false;
            }

            GameData gameData = GameMain.data;
            if (gameData?.galaxy == null || planetId != gameData.galaxy.birthPlanetId) {
                return false;
            }

            if (!TryGetStarterSignalPositionByPlanetId(planetId, out Vector3 signalPosition)) {
                return false;
            }

            Vector3 clientPosition = new(posX, posY, posZ);
            if (Vector3.Distance(clientPosition, signalPosition) > StarterSignalRecoverRadius) {
                return false;
            }

            return CompleteStarterKitRecovery();
        }

        public static void ShowProtocolRecoveredTip(int techId) {
            ShowRecoveryTip(techId is TFE行星内物流交互 or TFE星际物流交互
                ? "物流交互协议恢复提示"
                : "旧文明协议恢复提示");
        }

        /// <summary>
        /// 推进启动套件信号、常驻 HUD 与近距离回收生命周期，不改变套件位置或回收半径。
        /// 仅主机/单机调用；成功恢复后广播文明权威快照。
        /// </summary>
        private static void RecoverStarterKit() {
            if (starterKitRecovered) {
                StarterSignalHud.Hide();
                return;
            }

            bool hasDataCenter = IsTechRecovered(TFE分馏数据中心);
            bool hasProtoKit = IsTechRecovered(TFE分馏塔原胚);
            if (hasDataCenter || hasProtoKit) {
                if (!hasDataCenter) {
                    UnlockInternalRecoveryTech(TFE分馏数据中心);
                }
                if (!hasProtoKit) {
                    UnlockInternalRecoveryTech(TFE分馏塔原胚);
                }
                starterKitRecovered = true;
                StarterSignalHud.Hide();
                NebulaMultiplayerModAPI.BroadcastCivilizationState();
                return;
            }

            if (GameMain.gameTick < StarterSignalStartTick) {
                StarterSignalHud.Hide();
                return;
            }

            if (!TryGetStarterSignalPosition(out Vector3 signalPosition)) {
                NotifyStarterSignalOnce(onBirthPlanet: false, distance: 0f);
                StarterSignalHud.ShowReturnToBirth();
                return;
            }

            float distance = Vector3.Distance(GameMain.mainPlayer.position, signalPosition);
            StarterSignalHud.ShowSignal(GameMain.mainPlayer.position, signalPosition);
            if (distance > StarterSignalRecoverRadius) {
                NotifyStarterSignalOnce(onBirthPlanet: true, distance: distance);
                return;
            }

            CompleteStarterKitRecovery();
        }

        /// <summary>
        /// 联机客户端：只读启动信号 HUD + 进入 35m 后限频向主机请求恢复；不写权威状态、不解锁科技。
        /// </summary>
        private static void TickClientStarterSignal() {
            // 权威 starterKitRecovered 仅由主机快照 Import 写入；客户端可只读隐藏 HUD，不本地置位。
            if (starterKitRecovered
                || (IsTechRecovered(TFE分馏数据中心) && IsTechRecovered(TFE分馏塔原胚))) {
                StarterSignalHud.Hide();
                return;
            }

            if (GameMain.gameTick < StarterSignalStartTick) {
                StarterSignalHud.Hide();
                return;
            }

            if (!TryGetStarterSignalPosition(out Vector3 signalPosition)) {
                NotifyStarterSignalOnce(onBirthPlanet: false, distance: 0f);
                StarterSignalHud.ShowReturnToBirth();
                return;
            }

            Player player = GameMain.mainPlayer;
            float distance = Vector3.Distance(player.position, signalPosition);
            StarterSignalHud.ShowSignal(player.position, signalPosition);
            if (distance > StarterSignalRecoverRadius) {
                NotifyStarterSignalOnce(onBirthPlanet: true, distance: distance);
                return;
            }

            TrySendStarterRecoveryRequest(player);
        }

        /// <summary>
        /// 客户端在信号半径内以固定间隔向主机发送位置校验请求；网络不可用时不写本地状态。
        /// </summary>
        private static void TrySendStarterRecoveryRequest(Player player) {
            if (player == null) {
                return;
            }

            long tick = GameMain.gameTick;
            if (lastStarterRecoveryRequestTick != long.MinValue
                && tick - lastStarterRecoveryRequestTick < StarterRecoveryRequestIntervalTicks) {
                return;
            }

            PlanetData localPlanet = GameMain.data?.localPlanet;
            if (localPlanet == null) {
                return;
            }

            Vector3 position = player.position;
            if (!IsFiniteCoordinate(position.x)
                || !IsFiniteCoordinate(position.y)
                || !IsFiniteCoordinate(position.z)) {
                return;
            }

            if (!NebulaMultiplayerModAPI.RequestStarterKitRecovery(
                    localPlanet.id, position.x, position.y, position.z)) {
                return;
            }

            lastStarterRecoveryRequestTick = tick;
        }

        /// <summary>
        /// 主机权威完成启动套件恢复：解锁内部科技、置位、提示并广播快照。已恢复时为 no-op。
        /// </summary>
        /// <returns>本次是否新完成恢复。</returns>
        private static bool CompleteStarterKitRecovery() {
            if (starterKitRecovered) {
                StarterSignalHud.Hide();
                return false;
            }

            bool recovered = UnlockInternalRecoveryTech(TFE分馏数据中心);
            recovered |= UnlockInternalRecoveryTech(TFE分馏塔原胚);
            starterKitRecovered = true;
            StarterSignalHud.Hide();
            if (recovered) {
                ShowRecoveryTip("旧文明启动套件回收提示");
            }
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
            return true;
        }

        private static bool TryGetStarterSignalPosition(out Vector3 signalPosition) {
            signalPosition = default;
            GameData gameData = GameMain.data;
            PlanetData planet = gameData?.localPlanet;
            if (planet == null || gameData.galaxy == null || planet.id != gameData.galaxy.birthPlanetId) {
                return false;
            }

            return TryComputeStarterSignalPosition(planet, out signalPosition);
        }

        /// <summary>
        /// 由主机 GalaxyData.PlanetById 解析出生星后计算信号点，供客户端请求校验使用。
        /// </summary>
        private static bool TryGetStarterSignalPositionByPlanetId(int planetId, out Vector3 signalPosition) {
            signalPosition = default;
            GameData gameData = GameMain.data;
            if (gameData?.galaxy == null || planetId != gameData.galaxy.birthPlanetId) {
                return false;
            }

            PlanetData planet = gameData.galaxy.PlanetById(planetId);
            if (planet == null) {
                return false;
            }

            return TryComputeStarterSignalPosition(planet, out signalPosition);
        }

        /// <summary>
        /// 使用既有弧长/切线公式在给定行星上计算启动信号世界坐标。
        /// </summary>
        private static bool TryComputeStarterSignalPosition(PlanetData planet, out Vector3 signalPosition) {
            signalPosition = default;
            if (planet == null) {
                return false;
            }

            Vector3 birthPoint = planet.birthPoint;
            if (birthPoint.sqrMagnitude < 1f) {
                return false;
            }

            Vector3 up = birthPoint.normalized;
            Vector3 east = Vector3.Cross(Vector3.up, up);
            if (east.sqrMagnitude < 0.01f) {
                east = Vector3.Cross(Vector3.right, up);
            }
            east.Normalize();
            Vector3 north = Vector3.Cross(up, east).normalized;
            Vector3 tangent = (east * 0.72f + north * 0.69f).normalized;
            float arc = StarterSignalDistance / Mathf.Max(planet.realRadius, 1f);
            Vector3 signalDirection = (up * Mathf.Cos(arc) + tangent * Mathf.Sin(arc)).normalized;
            signalPosition = signalDirection * planet.realRadius;
            return IsFiniteCoordinate(signalPosition.x)
                   && IsFiniteCoordinate(signalPosition.y)
                   && IsFiniteCoordinate(signalPosition.z);
        }

        private static bool IsFiniteCoordinate(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// 首次探测到启动信号时弹一次有声音的实时提示；之后由常驻 HUD 持续指示方向与距离。
        /// <see cref="starterSignalNotified"/> 表示是否已弹过提示，存档语义保持兼容，不会重复播放声音。
        /// </summary>
        /// <param name="onBirthPlanet">玩家当前是否位于出生星。</param>
        /// <param name="distance">出生星上玩家与信号点的直线距离，仅在 <paramref name="onBirthPlanet"/> 为 true 时有效。</param>
        private static void NotifyStarterSignalOnce(bool onBirthPlanet, float distance) {
            if (starterSignalNotified) {
                return;
            }

            // ShowRecoveryTip 在 UI 未激活时会静默返回；这里同样等待 UI 就绪再消耗首次提示机会，
            // 避免 tick 300 恰好落在菜单/暂停时把唯一提示吞掉。
            if (UIRoot.instance?.uiGame?.generalTips == null || !UIRoot.instance.uiGame.active) {
                return;
            }

            starterSignalNotified = true;
            string text = onBirthPlanet
                ? string.Format("异常旧文明信号距离提示".Translate(), Mathf.CeilToInt(distance))
                : "异常旧文明信号出生星提示".Translate();
            ShowRecoveryTipText(text);
        }


        private static void RecoverLogisticsInteractionProtocols() {
            if (!starterKitRecovered || !IsTechRecovered(TFE培养塔)) {
                return;
            }

            for (int i = 0; i < logisticsInteractionTechIds.Length; i++) {
                if (logisticsInteractionRecovered[i]) {
                    continue;
                }

                int techId = logisticsInteractionTechIds[i];
                if (IsTechRecovered(techId)) {
                    logisticsInteractionRecovered[i] = true;
                    continue;
                }

                if (!IsTechRecovered(vanillaLogisticsTechIds[i])) {
                    continue;
                }

                if (UnlockInternalRecoveryTech(techId)) {
                    ShowProtocolRecoveredTip(techId);
                }
                logisticsInteractionRecovered[i] = true;
            }
        }

        private static bool UnlockInternalRecoveryTech(int techId) {
            if (GameMain.history == null || IsTechRecovered(techId)) {
                return false;
            }

            TechProto tech = LDB.techs.Select(techId);
            if (tech == null) {
                return false;
            }

            GameMain.history.UnlockTechUnlimited(techId, true);
            return IsTechRecovered(techId);
        }

        private static bool IsTechRecovered(int techId) {
            return GameMain.history != null && GameMain.history.TechUnlocked(techId, true);
        }

        private static void SyncRecoveredFlagsFromHistory() {
            if (GameMain.history == null) {
                return;
            }

            starterKitRecovered |= IsTechRecovered(TFE分馏数据中心) && IsTechRecovered(TFE分馏塔原胚);
            for (int i = 0; i < logisticsInteractionTechIds.Length; i++) {
                logisticsInteractionRecovered[i] |= IsTechRecovered(logisticsInteractionTechIds[i]);
            }
        }

        /// <summary>
        /// 翻译指定本地化键并显示恢复实时提示。
        /// </summary>
        /// <param name="translationKey">尚未翻译的本地化键。</param>
        private static void ShowRecoveryTip(string translationKey) {
            ShowRecoveryTipText(translationKey.Translate());
        }

        /// <summary>
        /// 显示已完成翻译和格式化的恢复实时提示。
        /// </summary>
        /// <param name="text">可直接显示的最终文本。</param>
        private static void ShowRecoveryTipText(string text) {
            if (UIRoot.instance?.uiGame?.generalTips == null || !UIRoot.instance.uiGame.active) {
                return;
            }
            UIRealtimeTip.Popup(text, true, 2);
        }
    }

    #endregion // CivilizationRecoveryManager

}

namespace FE.Logic.Civilization.Presentation {

    #region StarterSignalHud

    /// <summary>
    /// 启动信号常驻 HUD：在原版目标面板展开位置下方持续显示当前方向/距离。
    /// 独立于原版 GoalSystem，不注册 GoalProto、不写入存档、不新增 Harmony patch；
    /// 由 <see cref="FE.Logic.Civilization.CivilizationRecoveryManager"/> 在 Player.GameTick 调用链中驱动。
    /// 翻译键由启动期（TechManager.AddTranslations）统一注册，本类不再做运行期注册或刷新。
    /// </summary>
    public static class StarterSignalHud {
        /// <summary>
        /// HUD 在父级 UI 坐标系中的固定宽度。
        /// </summary>
        private const float HudWidth = 340f;

        /// <summary>
        /// HUD 在父级 UI 坐标系中的固定高度，可容纳英文状态的两行文本。
        /// </summary>
        private const float HudHeight = 60f;

        /// <summary>
        /// HUD 顶边与原版目标面板底边之间的 UI 坐标间距。
        /// </summary>
        private const float PanelGap = 8f;

        /// <summary>
        /// HUD 背景色：与原版目标区协调的克制深色半透明，使用普通纯色 Image（无圆角精灵依赖）。
        /// </summary>
        private static readonly Color HudBackgroundColor = new(26f / 255f, 30f / 255f, 36f / 255f, 0.82f);

        /// <summary>
        /// 罗盘八向翻译键，按玩家朝向顺时针排列：前、右前、右、右后、后、左后、左、左前。
        /// </summary>
        private static readonly string[] compassKeys = [
            "启动信号方向-前",
            "启动信号方向-右前",
            "启动信号方向-右",
            "启动信号方向-右后",
            "启动信号方向-后",
            "启动信号方向-左后",
            "启动信号方向-左",
            "启动信号方向-左前",
        ];

        /// <summary>
        /// 复用的世界坐标角点缓冲，避免每帧为目标面板定位分配数组。
        /// </summary>
        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        /// <summary>
        /// 当前存档场景中的 HUD 根节点；切档时销毁并清空。
        /// </summary>
        private static RectTransform hudRoot;

        /// <summary>
        /// HUD 内承载富文本状态的原版字体文本节点。
        /// </summary>
        private static Text hudText;

        /// <summary>
        /// 创建 HUD 时的目标面板父节点，用于检测场景或 UI 根节点切换。
        /// </summary>
        private static RectTransform cachedGoalParent;

        /// <summary>
        /// 隐藏 HUD，但保留已创建对象以便复用。
        /// </summary>
        public static void Hide() {
            if (hudRoot != null && hudRoot.gameObject.activeSelf) {
                hudRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 显示“返回出生星”提示。UI 尚未就绪时安全返回并隐藏残留 HUD。
        /// </summary>
        public static void ShowReturnToBirth() {
            if (!EnsureCreated()) {
                Hide();
                return;
            }

            hudText.text = ComposeLine("启动信号HUD-标题", "启动信号HUD-返回出生星".Translate());
            hudRoot.gameObject.SetActive(true);
            UpdatePosition();
        }

        /// <summary>
        /// 显示信号相对玩家朝向的罗盘方向与距离。UI 尚未就绪时安全返回并隐藏残留 HUD。
        /// </summary>
        /// <param name="playerPosition">玩家当前位置，用于计算距离与方向。</param>
        /// <param name="signalPosition">启动信号位置，与 <paramref name="playerPosition"/> 同坐标系。</param>
        public static void ShowSignal(Vector3 playerPosition, Vector3 signalPosition) {
            if (!EnsureCreated()) {
                Hide();
                return;
            }

            Player player = GameMain.mainPlayer;
            int compass = player != null
                ? CompassIndex(player.transform.forward, player.transform.right, signalPosition - playerPosition)
                : 0;
            int meters = Mathf.CeilToInt(Vector3.Distance(playerPosition, signalPosition));
            string body = string.Format("启动信号HUD-方向距离".Translate(), compassKeys[compass].Translate(), meters);
            hudText.text = ComposeLine("启动信号HUD-标题", body);
            hudRoot.gameObject.SetActive(true);
            UpdatePosition();
        }

        /// <summary>
        /// 切档或导入时彻底销毁 HUD，避免残留旧存档的目标块；先隐藏再销毁，规避 Unity 延迟销毁的一帧残影。
        /// </summary>
        public static void DestroyForSaveSwitch() {
            if (hudRoot != null) {
                hudRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(hudRoot.gameObject);
            }

            hudRoot = null;
            hudText = null;
            cachedGoalParent = null;
        }

        /// <summary>
        /// 拼装 HUD 单行富文本：橙色标题 + 白色正文。
        /// </summary>
        /// <param name="titleKey">标题翻译键。</param>
        /// <param name="body">已翻译的正文文本。</param>
        /// <returns>富文本字符串。</returns>
        private static string ComposeLine(string titleKey, string body) {
            return $"{titleKey.Translate().WithColor(Orange)}  {body.WithColor(White)}";
        }

        /// <summary>
        /// 根据玩家朝向把指向信号的水平向量映射为 0..7 罗盘索引。
        /// </summary>
        /// <param name="forward">玩家朝向。</param>
        /// <param name="right">玩家右侧方向。</param>
        /// <param name="toSignal">从玩家指向信号的向量。</param>
        /// <returns>0=前，1=右前，2=右，3=右后，4=后，5=左后，6=左，7=左前。</returns>
        private static int CompassIndex(Vector3 forward, Vector3 right, Vector3 toSignal) {
            if (toSignal.sqrMagnitude < 1e-6f) {
                return 0;
            }

            Vector3 fwd = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 rgt = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            float angle = Mathf.Atan2(Vector3.Dot(toSignal, rgt), Vector3.Dot(toSignal, fwd)) * Mathf.Rad2Deg;
            return ((int)Math.Round(angle / 45f, MidpointRounding.AwayFromZero) + 8) % 8;
        }

        /// <summary>
        /// 懒创建 HUD 并确保挂载在当前目标面板父节点下；UI 尚未就绪、玩家死亡或原版目标面板因全屏界面关闭时返回 false。
        /// </summary>
        /// <returns>HUD 已就绪且当前游戏界面允许显示时返回 true。</returns>
        private static bool EnsureCreated() {
            UIGame uiGame = UIRoot.instance?.uiGame;
            Player player = GameMain.mainPlayer;
            if (uiGame == null || !uiGame.active || uiGame.dashboard.active || uiGame.dysonEditor.active
                || player == null || !player.isAlive) {
                return false;
            }

            UIGoalPanel goalPanel = uiGame.goalPanel;
            if (goalPanel == null || goalPanel.rect == null) {
                return false;
            }

            RectTransform goalParent = goalPanel.rect.parent as RectTransform;
            if (goalParent == null) {
                return false;
            }

            // 外部（切档、场景卸载）销毁过 HUD 时，清理残留引用以便重建。
            if (hudRoot == null) {
                hudText = null;
                cachedGoalParent = null;
            } else if (hudRoot.parent != goalParent || cachedGoalParent != goalParent) {
                DestroyForSaveSwitch();
            }

            if (hudRoot == null) {
                CreateHud(goalParent);
                cachedGoalParent = goalParent;
            }

            return hudRoot != null;
        }

        /// <summary>
        /// 创建 HUD 节点：纯色深色背景 + 克隆自原版文本的 Text，挂到目标面板父节点下。
        /// </summary>
        /// <param name="goalParent">目标面板所在父节点，HUD 作为其兄弟节点。</param>
        private static void CreateHud(RectTransform goalParent) {
            Text sourceText = UIRoot.instance.uiGame.assemblerWindow?.stateText;
            if (sourceText == null) {
                return;
            }

            var rootObj = new GameObject("fe-starter-signal-hud", typeof(RectTransform), typeof(Image));
            hudRoot = rootObj.GetComponent<RectTransform>();
            hudRoot.SetParent(goalParent, false);
            hudRoot.pivot = new Vector2(0.5f, 1f);
            hudRoot.sizeDelta = new Vector2(HudWidth, HudHeight);
            // 锚点设为父节点顶部中点；实际位置由 UpdatePosition 用世界坐标覆盖。
            hudRoot.anchorMin = new Vector2(0.5f, 1f);
            hudRoot.anchorMax = new Vector2(0.5f, 1f);

            // 普通纯色 Image（无精灵），base.OnPopulateMesh 绘制实色矩形；不依赖 FE.Theme。
            Image background = rootObj.GetComponent<Image>();
            background.color = HudBackgroundColor;
            background.raycastTarget = false;

            hudText = UnityEngine.Object.Instantiate(sourceText, hudRoot);
            hudText.gameObject.name = "fe-starter-signal-hud-text";
            hudText.gameObject.SetActive(true);
            hudText.text = string.Empty;
            hudText.color = White;
            hudText.alignment = TextAnchor.MiddleCenter;
            hudText.fontSize = 15;
            hudText.supportRichText = true;
            hudText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // 英文返回提示可能换两行：52px 文本区可保留行间余量，Truncate 防止第三行越界。
            hudText.verticalOverflow = VerticalWrapMode.Truncate;
            hudText.raycastTarget = false;
            RectTransform textRect = hudText.rectTransform;
            textRect.localScale = Vector3.one;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            hudRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 把 HUD 顶部对齐到原版目标面板当前底边下方 <see cref="PanelGap"/> 像素处。
        /// X 方向用 <c>expandPosX</c> 校正，使目标面板 collapse/off 时 HUD 仍固定在展开位置，仅 Y 跟随其底边。
        /// </summary>
        private static void UpdatePosition() {
            UIGoalPanel goalPanel = UIRoot.instance == null ? null : UIRoot.instance.uiGame?.goalPanel;
            if (goalPanel == null || goalPanel.rect == null || hudRoot == null) {
                return;
            }

            RectTransform goalParent = goalPanel.rect.parent as RectTransform;
            if (goalParent == null) {
                return;
            }

            goalPanel.rect.GetWorldCorners(cornerBuffer);
            Vector3 bottomCenter = (cornerBuffer[0] + cornerBuffer[3]) * 0.5f;
            // 目标面板 collapse/off 时 anchoredPosition.x 会移到 collapsePosX；按 expandPosX 偏移把 HUD 固定在展开列。
            // X 校正和垂直间距都从父级 UI 坐标转换到世界坐标，保持 Canvas 缩放下的视觉距离一致。
            float xCorrection = goalPanel.expandPosX - goalPanel.rect.anchoredPosition.x;
            Vector3 expandedPositionOffset = goalParent.TransformVector(new Vector3(xCorrection, -PanelGap, 0f));
            hudRoot.position = bottomCenter + expandedPositionOffset;
        }
    }

    #endregion // StarterSignalHud

}
