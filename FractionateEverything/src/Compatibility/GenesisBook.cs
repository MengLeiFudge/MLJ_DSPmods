using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using ProjectGenesis.Patches;
using ProjectGenesis.Utils;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Compatibility;

public static class GenesisBook {
    internal const string GUID = "org.LoShin.GenesisBook";

    internal static bool Enable;
    internal static int tab精炼;
    internal static int tab化工;
    internal static int tab防御;
    private static bool _finished;

    #region 创世ERecipeType拓展

    internal const ERecipeType 基础制造 = ERecipeType.Assemble;
    internal const ERecipeType 标准制造 = (ERecipeType)9;
    internal const ERecipeType 高精度加工 = (ERecipeType)10;

    #endregion

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        tab精炼 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab1");
        tab化工 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab2");
        tab防御 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab3");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.GenesisBook");
        harmony.PatchAll(typeof(GenesisBook));
        harmony.Patch(
            AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
            null,
            new(typeof(GenesisBook), nameof(AfterLDBToolPostAddData)) {
                after = [LDBToolPlugin.MODGUID]
            }
        );
        CheckPlugins.LogInfo("GenesisBook Compat finish.");
    }

    public static void AfterLDBToolPostAddData() {
        if (_finished) return;

        // //调整黑雾物品的位置，调回胶囊位置
        // List<int> idList = [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素];
        // for (int i = 0; i < idList.Count; i++) {
        //     LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + (i + 2) * 100 + 16;
        // }
        // idList = [I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊];
        // for (int i = 0; i < idList.Count; i++) {
        //     LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + 701 + i;
        // }
        //
        // //修改创世部分物品、配方的显示位置
        // idList = [IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器];
        // for (int i = 0; i < idList.Count; i++) {
        //     ModifyItemAndItsRecipeGridIndex(idList[i], 2, 501 + i);
        // }
        // idList = [
        //     I原型机, I精准无人机, I攻击无人机, I护卫舰, I驱逐舰,
        //     I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮,
        //     I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器,
        // ];
        // for (int i = 0; i < idList.Count; i++) {
        //     ModifyItemAndItsRecipeGridIndex(idList[i], tab防御, 101 + i);
        // }
        // idList = [
        //     I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱,
        //     I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元,
        //     I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组,
        //     I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组,
        //     I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊,
        // ];
        // foreach (var id in idList) {
        //     ModifyItemAndItsRecipeGridIndex(id, 2);
        // }
        // ModifyItemAndItsRecipeGridIndex(I高斯机枪塔, tab防御, 301);
        // ModifyItemAndItsRecipeGridIndex(I聚爆加农炮, tab防御, 401);
        // ModifyItemAndItsRecipeGridIndex(IGB电磁加农炮, tab防御, 501);
        // ModifyItemAndItsRecipeGridIndex(I导弹防御塔, tab防御, 601);

        _finished = true;
        CheckPlugins.LogInfo("GenesisBook Compatibility LDBToolOnPostAddDataAction finish.");
    }

    private static void ModifyItemAndItsRecipeGridIndex(int itemId, int tab, int rowColumn) {
        var item = LDB.items.Select(itemId);
        item.GridIndex = tab * 1000 + rowColumn;
        item.maincraft.GridIndex = item.GridIndex;
    }

    private static void ModifyItemAndItsRecipeGridIndex(int itemId, int offset) {
        var item = LDB.items.Select(itemId);
        item.GridIndex += offset;
        item.maincraft.GridIndex = item.GridIndex;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FastStartOptionPatches), nameof(FastStartOptionPatches.SetForNewGame))]
    private static IEnumerable<CodeInstruction> FastStartOptionPatches_SetForNewGame_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        //if (!GameMain.data.history.TechUnlocked(proto.ID) && NeedFastUnlock(proto.Items))
        //变为
        //if (IsFracTech(proto.ID) && !GameMain.data.history.TechUnlocked(proto.ID) && NeedFastUnlock(proto.Items))
        var matcher = new CodeMatcher(instructions);
        //寻找: GameMain.data.history.TechUnlocked(proto.ID)
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldsfld),// GameMain.data
            new CodeMatch(OpCodes.Ldfld),// .history
            new CodeMatch(OpCodes.Ldloc_3),// proto
            new CodeMatch(OpCodes.Ldfld),// .ID
            new CodeMatch(OpCodes.Callvirt)// TechUnlocked
        );
        if (matcher.IsInvalid) {
            CheckPlugins.LogError("Failed to find TechUnlocked call pattern");
            return instructions;
        }
        //找到要跳转的标签
        var matcher2 = matcher.Clone();
        matcher2.MatchForward(false, new CodeMatch(OpCodes.Brtrue));
        if (matcher2.IsInvalid) {
            CheckPlugins.LogError("Failed to find Brtrue opcode");
            return instructions;
        }
        // 在 GameMain.data.history.TechUnlocked 调用之前插入我们的检查
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_3),// proto
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TechProto), "ID")),// proto.ID
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(GenesisBook), nameof(IsFracTech))),// IsFracTech(proto.ID)
            new CodeInstruction(matcher2.Opcode, matcher2.Operand)// 如果是分馏科技，直接跳过
        );
        return matcher.InstructionEnumeration();
    }

    public static bool IsFracTech(int id) {
        return id >= TFE分馏数据中心 && id <= TFE超值礼包9;
    }

    /// <summary>
    /// 修复开启“科技探索”时，分馏塔的科技不能显示的问题
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InitialTechPatches), nameof(InitialTechPatches.RefreshNode))]
    private static bool InitialTechPatches_RefreshNode_Prefix(ref UITechTree __instance) {
        GameHistoryData history = GameMain.history;
        foreach ((int techId, UITechNode node) in __instance.nodes) {
            TechProto tech = node.techProto;
            if (techId > 1999 || node == null || tech.IsHiddenTech) {
                continue;
            }
            bool techUnlocked = history.TechUnlocked(techId);
            bool anyPreTechUnlocked = tech.PreTechs.Length > 0
                ? tech.PreTechs.Any(history.TechUnlocked)
                : tech.PreTechsImplicit.Any(history.TechUnlocked);
            node.gameObject.SetActive(techUnlocked || anyPreTechUnlocked);
            if (tech.postTechArray.Length > 0) {
                node.connGroup.gameObject.SetActive(techUnlocked);
            }
        }
        return false;
    }

    /*#region 量化工具适配，禁止选取所有分馏配方，添加10点数适配

    //此处必须使用int而非EProliferatorStrategy，否则会因需要加载类型EProliferatorStrategy而报错找不到创世之书dll
    private const int Nonuse = 0;
    private const int ExtraProducts = 1;
    private const int ProductionSpeedup = 2;
    private const int ExtraProducts10 = 3;
    private const int ProductionSpeedup10 = 4;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProliferatorComboBox), nameof(ProliferatorComboBox.Strategy), MethodType.Getter)]
    private static bool ProliferatorComboBox_Strategy_Getter_Prefix(ref ProliferatorComboBox __instance,
        ref EProliferatorStrategy __result) {
        __result = __instance.comboBox.Items[__instance.selectIndex] switch {
            "增产4点" => (EProliferatorStrategy)ExtraProducts,
            "加速4点" => (EProliferatorStrategy)ProductionSpeedup,
            "增产10点" => (EProliferatorStrategy)ExtraProducts10,
            "加速10点" => (EProliferatorStrategy)ProductionSpeedup10,
            _ => (EProliferatorStrategy)Nonuse,
        };
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProliferatorComboBox), nameof(ProliferatorComboBox.Init))]
    private static bool ProliferatorComboBox_Init_Prefix(ref ProliferatorComboBox __instance, int strategy) {
        __instance.Init([509, 1143, 1143, 1143, 1143], ["不使用增产剂", "增产4点", "加速4点", "增产10点", "加速10点"], strategy);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProliferatorComboBox), nameof(ProliferatorComboBox.InitNoProductive))]
    private static bool ProliferatorComboBox_InitNoProductive_Prefix(ref ProliferatorComboBox __instance,
        int strategy) {
        __instance.Init([509, 1143, 1143], ["不使用增产剂", "加速4点", "加速10点"], strategy);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProliferatorComboBox), nameof(ProliferatorComboBox.SetStrategySlience))]
    private static bool ProliferatorComboBox_SetStrategySlience_Prefix(ref ProliferatorComboBox __instance,
        EProliferatorStrategy strategy) {
        UIComboBox uiComboBox = __instance.comboBox;
        uiComboBox._itemIndex = __instance.Items.Count switch {
            5 => (int)strategy,
            3 => (int)strategy / 2,
            _ => throw new(),//不应出现的情况
        };
        uiComboBox.m_Input.text = uiComboBox._itemIndex >= 0 ? uiComboBox.Items[uiComboBox._itemIndex] : "";
        __instance.selectIndex = uiComboBox._itemIndex;
        __instance.OnItemIndexChange();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NodeDataSet), nameof(NodeDataSet.AddNodeChilds))]
    private static bool NodeDataSet_AddNodeChilds_Prefix(ref NodeDataSet __instance, NodeData node) {
        //如果本身是自然资源或没有任何配方能制作它，则作为Raw处理
        if (node.Item.isRaw || node.Item.recipes.Count == 0) {
            __instance.MergeRaws(node);
            return false;
        }
        //如果用户规定这个东西为原料，则作为AsRaw处理
        if (node.Options.AsRaw) {
            __instance.MergeAsRaws(node);
            return false;
        }
        //将这个node作为Data处理
        __instance.MergeData(node);
        RecipeProto recipe = node.Options.Recipe;
        //使用多产物配方时，将所有额外产物添加到右侧“副产物”内，避免递归计算
        int idx = Array.IndexOf(recipe.Results, node.Item.ID);
        int resultsLength = recipe.Results.Length;
        if (resultsLength > 1) {
            for (var index = 0; index < resultsLength; index++) {
                if (idx != index) {
                    ItemProto proto = LDB.items.Select(recipe.Results[index]);
                    float count = node.ItemCount * recipe.ResultCounts[index] / recipe.ResultCounts[idx];
                    __instance.MergeByproducts(__instance.ItemRaw(proto, count));
                }
            }
        }
        //计算每一个原料的node，同时计算需要的自喷涂过的增产剂（每个可以喷涂74次）数目
        int itemsLength = recipe.Items.Length;
        for (var index = 0; index < itemsLength; index++) {
            ItemProto proto = LDB.items.Select(recipe.Items[index]);
            float count = node.ItemCount * recipe.ItemCounts[index] / recipe.ResultCounts[idx];
            if (node.Options.Factory.ModelIndex == Utils.M负熵熔炉) count /= 2f;
            switch ((int)node.Options.Strategy) {
                case Nonuse:
                    break;
                case ExtraProducts:
                    count /= 1 + (float)Cargo.incTableMilli[4];
                    __instance._totalProliferatedItemCount += count;
                    break;
                case ProductionSpeedup:
                    __instance._totalProliferatedItemCount += count;
                    break;
                case ExtraProducts10:
                    count /= 1 + (float)Cargo.incTableMilli[10];
                    __instance._totalProliferatedItemCount += count * 2.5f;
                    break;
                case ProductionSpeedup10:
                    __instance._totalProliferatedItemCount += count * 2.5f;
                    break;
                default:
                    throw new();
            }
            NodeData nodeData = __instance.ItemNeed(proto, count);
            __instance.AddNodeChilds(nodeData);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NodeDataSet), nameof(NodeDataSet.ItemNeed), [typeof(ItemProto), typeof(float)])]
    private static bool NodeDataSet_ItemNeed_Prefix(ref NodeDataSet __instance, ItemProto proto, float count,
        ref NodeData __result) {
        if (__instance.CustomOptions.TryGetValue(proto, out NodeOptions option)) {
            if (option.AsRaw || proto.isRaw || proto.recipes.Count == 0) {
                __result = __instance.ItemRaw(proto, count, option);
                return false;
            }
            NodeData data = __instance.ItemNeed(proto, count, option);
            data.RefreshFactoryCount();
            __result = data;
            return false;
        }
        //从可制造配方中移除所有分馏配方。如果移除后无可用配方，按照raw处理。
        var list = new List<RecipeProto>(proto.recipes);
        list.RemoveAll(x => (Utils_ERecipeType)x.Type == Utils_ERecipeType.Fractionate);
        RecipeProto recipe = list.FirstOrDefault();
        Utils_ERecipeType type = (Utils_ERecipeType?)recipe?.Type ?? Utils_ERecipeType.None;
        if (type == Utils_ERecipeType.None) {
            __result = __instance.ItemRaw(proto, count);
            return false;
        }
        if (!__instance.DefaultMachine.TryGetValue(type, out ItemProto factory)) {
            factory = QTools.RecipeTypeFactoryMap[type][0];
            __instance.SetDefaultMachine(type, factory);
        }
        __result = __instance.ItemNeed(proto, count, factory, recipe, !string.IsNullOrWhiteSpace(proto.miningFrom));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NodeData), nameof(NodeData.RefreshFactoryCount))]
    private static bool NodeData_RefreshFactoryCount_Prefix(ref NodeData __instance) {
        PrefabDesc factoryPrefabDesc = __instance.Options.Factory.prefabDesc;
        float assemblerSpeed = factoryPrefabDesc.assemblerSpeed;
        if (factoryPrefabDesc.isLab) assemblerSpeed = factoryPrefabDesc.labAssembleSpeed;
        int idx = Array.IndexOf(__instance.Options.Recipe.Results, __instance.Item.ID);
        float count = __instance.ItemCount
                      * __instance.Options.Recipe.TimeSpend
                      / __instance.Options.Recipe.ResultCounts[idx]
                      / assemblerSpeed
                      / 0.36f;
        if (__instance.Options.Factory.ModelIndex == Utils.M负熵熔炉) count /= 2f;
        switch ((int)__instance.Options.Strategy) {
            case Nonuse:
                break;
            case ExtraProducts:
                count /= 1 + (float)Cargo.incTableMilli[4];
                break;
            case ProductionSpeedup:
                count /= 1 + (float)Cargo.accTableMilli[4];
                break;
            case ExtraProducts10:
                count /= 1 + (float)Cargo.incTableMilli[10];
                break;
            case ProductionSpeedup10:
                count /= 1 + (float)Cargo.accTableMilli[10];
                break;
            default:
                throw new();
        }
        __instance.Options.FactoryCount = count;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProductDetail), nameof(ProductDetail.SetData))]
    private static void ProductDetail_SetData_Postfix(ref ProductDetail __instance, NodeData data) {
        List<RecipeProto> recipes = [..data.Item.recipes];
        recipes.RemoveAll(x => (Utils_ERecipeType)x.Type == Utils_ERecipeType.Fractionate);
        bool flag1 = recipes.Count > 1;
        bool flag2 = !string.IsNullOrWhiteSpace(data.Item.miningFrom);
        bool flag3 = flag1 | flag2;
        if (flag3) {
            StringBuilder stringBuilder = new StringBuilder();
            if (flag1) stringBuilder.AppendLine("左键点击：更换配方".TranslateFromJson());
            if (flag2) stringBuilder.AppendLine("右键点击：将其设置为原材料".TranslateFromJson());
            __instance.recipeImgButton.tips.tipTitle =
                (flag1 ? (flag2 ? "可采集多配方物品" : "可调整配方") : "可采集物品").TranslateFromJson();
            __instance.recipeImgButton.tips.tipText = stringBuilder.ToString();
        }
        __instance.recipeImgButton.gameObject.SetActive(flag3);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProductDetail), nameof(ProductDetail.Filter))]
    private static bool ProductDetail_Filter_Prefix(ref ProductDetail __instance, RecipeProto recipeProto,
        ref bool __result) {
        if ((Utils_ERecipeType)recipeProto.Type == Utils_ERecipeType.Fractionate) {
            __result = false;
            return false;
        }
        return true;
    }

    #endregion*/
}
