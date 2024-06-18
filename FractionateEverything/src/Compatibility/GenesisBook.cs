using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using ProjectGenesis.Patches.Logic.QTools;
using ProjectGenesis.Patches.UI.QTools;
using ProjectGenesis.Patches.UI.QTools.MyComboBox;
using ProjectGenesis.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Main.FractionatorLogic;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Compatibility.CheckPlugins;
using Utils_ERecipeType = ProjectGenesis.Utils.ERecipeType;
using static FractionateEverything.Main.FractionateRecipes;

namespace FractionateEverything.Compatibility {
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

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.GenesisBook");
            harmony.PatchAll(typeof(GenesisBook));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(GenesisBook), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("GenesisBook Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //修改重氢的前置科技为奇异物质
            LDB.items.Select(I重氢).preTech = LDB.techs.Select(T奇异物质);

            //调整黑雾物品的位置，调回胶囊位置
            List<int> idList = [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素];
            for (int i = 0; i < idList.Count; i++) {
                LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + (i + 2) * 100 + 16;
            }
            idList = [I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊];
            for (int i = 0; i < idList.Count; i++) {
                LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + 701 + i;
            }

            //修改创世部分物品、配方的显示位置
            LDB.recipes.Select(RGB物质回收).GridIndex = 1209;
            ModifyItemAndItsRecipeGridIndex(I动力引擎, 1, 210);
            idList = [
                I原型机, I精准无人机, I攻击无人机, I护卫舰, I驱逐舰,
                I高频激光塔_GB高频激光塔MKI, IGB高频激光塔MKII, I近程电浆塔, I磁化电浆炮,
                I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器,
            ];
            for (int i = 0; i < idList.Count; i++) {
                ModifyItemAndItsRecipeGridIndex(idList[i], tab防御, 101 + i);
            }
            idList = [
                I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱,
                I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元,
                I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组,
                I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组,
                I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊,
            ];
            foreach (var id in idList) {
                ModifyItemAndItsRecipeGridIndex(id, 2);
            }
            ModifyItemAndItsRecipeGridIndex(I高斯机枪塔, tab防御, 301);
            ModifyItemAndItsRecipeGridIndex(I聚爆加农炮_GB聚爆加农炮MKI, tab防御, 401);
            ModifyItemAndItsRecipeGridIndex(IGB聚爆加农炮MKII, tab防御, 501);
            ModifyItemAndItsRecipeGridIndex(I导弹防御塔, tab防御, 601);

            _finished = true;
            LogInfo("GenesisBook Compatibility LDBToolOnPostAddDataAction finish.");
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

        #region 量化工具适配，特别感谢创世之书代码编写者Awbugl的帮助

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIQToolsWindow), "CreateUI")]
        private static void UIQToolsWindow_CreateUI_Postfix(ref UIQToolsWindow __instance) {
            __instance._recipeMachines.Add(Utils_ERecipeType.Fractionate,
                MyComboBox.CreateComboBox<ItemComboBox>(30, 335, __instance._tabs[0]));
            var rect = (RectTransform)__instance._proliferatorComboBox.transform;
            var anchoredPosition3D = rect.anchoredPosition3D;
            anchoredPosition3D.y -= 45;
            rect.anchoredPosition3D = anchoredPosition3D;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(QTools), "GetFactoryDict")]
        private static void UIQToolsWindow_GetFactoryDict_Postfix(
            ref ConcurrentDictionary<Utils_ERecipeType, List<ItemProto>> __result) {
            __result.TryAddOrInsert(Utils_ERecipeType.Fractionate, LDB.items.Select(2314));
        }

        private const int Nonuse = 0;
        private const int ExtraProducts = 1;
        private const int ProductionSpeedup = 2;
        private const int ExtraProducts10 = 3;
        private const int ProductionSpeedup10 = 4;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProliferatorComboBox), "Strategy", MethodType.Getter)]
        private static bool ProliferatorComboBox_Strategy_Getter_Prefix(ref ProliferatorComboBox __instance,
            ref EProliferatorStrategy __result) {
            __result = __instance.comboBox.Items[__instance.selectIndex] switch {
                "不使用增产剂" => (EProliferatorStrategy)Nonuse,
                "增产4点" => (EProliferatorStrategy)ExtraProducts,
                "加速4点" => (EProliferatorStrategy)ProductionSpeedup,
                "增产10点" => (EProliferatorStrategy)ExtraProducts10,
                "加速10点" => (EProliferatorStrategy)ProductionSpeedup10,
                _ => EProliferatorStrategy.Nonuse,
            };
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProliferatorComboBox), "Init")]
        private static bool ProliferatorComboBox_Init_Prefix(ref ProliferatorComboBox __instance, int strategy) {
            __instance.Init([509, 1143, 1143, 1143, 1143], ["不使用增产剂", "增产4点", "加速4点", "增产10点", "加速10点"], strategy);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProliferatorComboBox), "InitNoProductive")]
        private static bool ProliferatorComboBox_InitNoProductive_Prefix(ref ProliferatorComboBox __instance,
            int strategy) {
            __instance.Init([509, 1143, 1143], ["不使用增产剂", "加速4点", "加速10点"], strategy);
            return false;
        }

        private static void InitIPF(this ProliferatorComboBox proliferatorComboBox, int strategy) {
            proliferatorComboBox.Init([1143, 1143], ["增产4点", "增产10点"], strategy);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProliferatorComboBox), "SetStrategySlience")]
        private static bool ProliferatorComboBox_SetStrategySlience_Prefix(ref ProliferatorComboBox __instance,
            EProliferatorStrategy strategy) {
            UIComboBox uiComboBox = __instance.comboBox;
            uiComboBox._itemIndex = __instance.Items.Count switch {
                5 => (int)strategy,
                3 => (int)strategy / 2,
                2 => (int)strategy == ExtraProducts10 ? 1 : 0,
                _ => throw new(),//不应出现的情况
            };
            uiComboBox.m_Input.text = uiComboBox._itemIndex >= 0 ? uiComboBox.Items[uiComboBox._itemIndex] : "";
            __instance.selectIndex = uiComboBox._itemIndex;
            __instance.OnItemIndexChange();
            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ItemComboBox), "OnItemIndexChange")]
        static IEnumerable<CodeInstruction> ItemComboBox_OnItemIndexChange_Transpiler(
            IEnumerable<CodeInstruction> instructions) {
            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false, new CodeMatch(OpCodes.Ldarg_0));
            //第1行ldarg.0不变
            matcher.Advance(1);
            //第2行改成call
            matcher.SetAndAdvance(OpCodes.Call,
                AccessTools.Method(typeof(GenesisBook), nameof(ItemComboBox_OnItemIndexChange_InsertMethod)));
            //3-5行改成nop
            while (matcher.Opcode != OpCodes.Stloc_0) {
                matcher.SetAndAdvance(OpCodes.Nop, null);
            }
            //第6行stloc.0不变
            return matcher.InstructionEnumeration();
        }

        public static ItemProto ItemComboBox_OnItemIndexChange_InsertMethod(ItemComboBox __instance) {
            return __instance.selectIndex == -1
                ? LDB.items.Select(IFE增产分馏塔)
                : __instance._items[__instance.selectIndex];
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NodeDataSet), "AddNodeChilds")]
        private static bool NodeDataSet_AddNodeChilds_Prefix(ref NodeDataSet __instance, NodeData node) {
            //如果本身是矿物且没有任何配方能制作它，则作为Raw处理
            if (node.Item.isRaw && node.Item.recipes.Count == 0) {
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
            //增产塔
            if (node.Options.Factory.ID == IFE增产分馏塔) {
                //计算需要的自喷涂过的增产剂（每个可以喷涂74次）数目
                float count = node.ItemCount;
                switch ((int)node.Options.Strategy) {
                    case ExtraProducts:
                        count /= 1.25f;
                        __instance._totalProliferatedItemCount += count;
                        break;
                    case ExtraProducts10:
                        count /= 1.4f;
                        __instance._totalProliferatedItemCount += count * 2.5f;
                        break;
                    default:
                        throw new();
                }
                //不需要计算原料，直接返回
                return false;
            }
            RecipeProto recipe = node.Options.Recipe;
            //常规分馏配方
            if ((Utils_ERecipeType)recipe.Type == Utils_ERecipeType.Fractionate) {
                //计算原料的node
                int inputItemID = recipe.Items[0];
                ItemProto inputItem = LDB.items.Select(inputItemID);
                //根据分馏配方实际情况，计算需要多少原料
                if (!fracRecipeNumRatioDic.TryGetValue(inputItemID, out Dictionary<int, float> dic)) {
                    dic = defaultDic;
                }
                //x表示原料数目
                float x = 1.0f;
                //y表示可以转换出来的产物数目
                float y = 0.0f;
                //获取损毁概率
                float destroyRate = 0.0f;
                if (FractionateEverything.enableDestroy && dic.TryGetValue(-1, out float value)) {
                    destroyRate = value;
                }
                while (x > 1e-6) {
                    //先判定损毁
                    x *= 1 - destroyRate;
                    //再判定转换情况
                    float tempSubX = 0;
                    float tempSubY = 0;
                    foreach (var p in dic) {
                        if (p.Key <= 0) {
                            continue;
                        }
                        //如果有增产剂，将会进一步提升分馏成功率
                        float subX = x * p.Value;
                        if ((int)node.Options.Strategy == ProductionSpeedup) {
                            subX *= 2f;
                        }
                        else if ((int)node.Options.Strategy == ProductionSpeedup10) {
                            subX *= 3.5f;
                        }
                        tempSubX += subX;
                        tempSubY += p.Key * subX;
                    }
                    x -= tempSubX;
                    y += tempSubY;
                }
                //现在得到结论：1个原料可以生成y个产物
                //node.ItemCount是产物数目
                NodeData nodeData = __instance.ItemNeed(inputItem, (float)(node.ItemCount / y));
                //判断是否有循环递归问题，递归只在常规分馏中出现所以写在这里
                if (__instance.Datas.ContainsKey(inputItem)) {
                    __instance.MergeRaws(nodeData);
                    return false;
                }
                //计算需要的自喷涂过的增产剂（每个可以喷涂74次）数目
                float count = nodeData.ItemCount;
                switch ((int)node.Options.Strategy) {
                    case Nonuse:
                        break;
                    case ProductionSpeedup:
                        __instance._totalProliferatedItemCount += count;
                        break;
                    case ProductionSpeedup10:
                        __instance._totalProliferatedItemCount += count * 2.5f;
                        break;
                    default:
                        throw new();
                }
                __instance.AddNodeChilds(nodeData);
                return false;
            }
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
                switch ((int)node.Options.Strategy) {
                    case Nonuse:
                        break;
                    case ExtraProducts:
                        count /= 1.25f;
                        __instance._totalProliferatedItemCount += count;
                        break;
                    case ProductionSpeedup:
                        count /= 2f;
                        __instance._totalProliferatedItemCount += count;
                        break;
                    case ExtraProducts10:
                        count /= 1.4f;
                        __instance._totalProliferatedItemCount += count * 2.5f;
                        break;
                    case ProductionSpeedup10:
                        count /= 3.5f;
                        __instance._totalProliferatedItemCount += count * 2.5f;
                        break;
                    default:
                        throw new();
                }
                if (node.Options.Factory.ModelIndex == ProtoID.M负熵熔炉) count /= 2f;
                NodeData nodeData = __instance.ItemNeed(proto, count);
                __instance.AddNodeChilds(nodeData);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NodeDataSet), "ItemNeed", [typeof(ItemProto), typeof(float)])]
        private static bool NodeDataSet_ItemNeed_Prefix(ref NodeDataSet __instance, ItemProto proto, float count,
            ref NodeData __result) {
            if (__instance.CustomOptions.TryGetValue(proto, out NodeOptions option)) {
                if (option.AsRaw
                    || (proto.isRaw && proto.recipes.Count == 0)) {
                    __result = __instance.ItemRaw(proto, count, option);
                    return false;
                }
                NodeData data = __instance.ItemNeed(proto, count, option);
                data.RefreshFactoryCount();
                __result = data;
                return false;
            }
            //先选出第一个配方，配方类型未知
            var list = new List<RecipeProto>(proto.recipes);
            RecipeProto recipe = list.FirstOrDefault();
            //如果去除分馏配方之后还有配方，默认使用首个非分馏配方
            list.RemoveAll(x => (Utils_ERecipeType)x.Type == Utils_ERecipeType.Fractionate);
            if (list.Count > 0) {
                recipe = list.FirstOrDefault();
            }
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
        [HarmonyPatch(typeof(NodeData), "RefreshFactoryCount")]
        private static bool NodeData_RefreshFactoryCount_Prefix(ref NodeData __instance) {
            if (__instance.Options.Factory.ID == IFE增产分馏塔) {
                //增产分馏
                //0.6%1个，0.4%2个，产出为每个塔100/分钟
                __instance.Options.FactoryCount = (int)__instance.Options.Strategy switch {
                    ExtraProducts => __instance.ItemCount / 100.0f * 0.4f / 0.25f,
                    ExtraProducts10 => __instance.ItemCount / 100.0f,
                    _ => throw new(),
                };
                return false;
            }
            if (__instance.Options.Factory.ID == I分馏塔_FE通用分馏塔) {
                //1%1个，产出为每个塔75.3/分钟
                __instance.Options.FactoryCount = (int)__instance.Options.Strategy switch {
                    Nonuse => __instance.ItemCount / 75.3f,
                    ProductionSpeedup => __instance.ItemCount / 75.3f / 2f,
                    ProductionSpeedup10 => __instance.ItemCount / 75.3f / 3.5f,
                    _ => throw new(),
                };
                return false;
            }
            PrefabDesc factoryPrefabDesc = __instance.Options.Factory.prefabDesc;
            float assemblerSpeed = factoryPrefabDesc.assemblerSpeed;
            if (factoryPrefabDesc.isLab) assemblerSpeed = factoryPrefabDesc.labAssembleSpeed;
            int idx = Array.IndexOf(__instance.Options.Recipe.Results, __instance.Item.ID);
            float count = __instance.ItemCount
                          * __instance.Options.Recipe.TimeSpend
                          / __instance.Options.Recipe.ResultCounts[idx]
                          / assemblerSpeed
                          / 0.36f;
            if (__instance.Options.Factory.ModelIndex == ProtoID.M负熵熔炉) count /= 2f;
            switch ((int)__instance.Options.Strategy) {
                case Nonuse:
                    break;
                case ExtraProducts:
                    count /= 1.25f;
                    break;
                case ProductionSpeedup:
                    count /= 2f;
                    break;
                case ExtraProducts10:
                    count /= 1.4f;
                    break;
                case ProductionSpeedup10:
                    count /= 3.5f;
                    break;
                default:
                    throw new();
            }
            __instance.Options.FactoryCount = count;
            return false;
        }

        //proliferator ComboBox Increase Production Fractionator，增产分馏塔专用
        private static readonly Dictionary<object, object> proliferatorComboBoxIPFDic = [];

        private static ProliferatorComboBox _proliferatorComboBoxIPF(this ProductDetail productDetail) {
            return (ProliferatorComboBox)proliferatorComboBoxIPFDic[productDetail];
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ProductDetail), "CreateProductDetail")]
        private static void ProductDetail_CreateProductDetail_Postfix(ref ProductDetail __result) {
            //添加右键工厂事件
            ProductDetail productDetail = __result;
            __result.factoryButton.onRightClick += _ => OnFactoryButtonRightClick(productDetail);
            //为这个ProductDetail对象添加唯一对应的_proliferatorComboBoxIPF
            //本来是要写在Init_Postfix里面的，后来发现运行顺序不对劲，就改到这里了，效果是一样的
            ProliferatorComboBox _proliferatorComboBoxIPF =
                MyComboBox.CreateComboBox<ProliferatorComboBox>(700, 10, __result._rect);
            _proliferatorComboBoxIPF.gameObject.SetActive(false);
            _proliferatorComboBoxIPF.comboBox.gameObject.SetActive(false);
            proliferatorComboBoxIPFDic[__result] = _proliferatorComboBoxIPF;
            _proliferatorComboBoxIPF.InitIPF(0);
            _proliferatorComboBoxIPF.OnIndexChange += __result.OnProliferatorChange;
        }

        private static void OnFactoryButtonRightClick(ProductDetail productDetail) {
            productDetail.factoryComboBox.comboBox.itemIndex =
                productDetail.factoryComboBox.comboBox.itemIndex == -1 ? 0 : -1;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ProductDetail), "SetData")]
        private static void ProductDetail_SetData_Postfix(ref ProductDetail __instance, NodeData data) {
            RecipeProto recipe;
            if (data.Options.Factory.ID == IFE增产分馏塔) {
                recipe = new() {
                    Type = ERecipeType.Fractionate,
                    Items = [data.Item.ID],
                    ItemCounts = [100],
                    Results = [data.Item.ID],
                    ResultCounts = [1],
                    TimeSpend = 60,
                };
            }
            else {
                recipe = data.Options.Recipe;
            }
            bool multiRecipes = data.Item.recipes.Count > 1;
            bool canMining = !string.IsNullOrWhiteSpace(data.Item.miningFrom);
            bool buttonShow = multiRecipes || canMining;
            if (buttonShow) {
                var sb = new StringBuilder();
                if (multiRecipes) sb.AppendLine("左键点击：更换配方".TranslateFromJson());
                if (canMining) sb.AppendLine("右键点击：将其设置为原材料".TranslateFromJson());
                __instance.recipeImgButton.tips.tipTitle =
                    (multiRecipes ? canMining ? "可采集多配方物品" : "可调整配方" : "可采集物品").TranslateFromJson();
                __instance.recipeImgButton.tips.tipText = sb.ToString();
            }
            __instance.recipeImgButton.gameObject.SetActive(buttonShow);
            __instance.recipeEntry.SetRecipe(recipe);
            if (data.Options.Factory.ID == IFE增产分馏塔) {
                __instance._proliferatorComboBoxNormal.gameObject.SetActive(false);
                __instance._proliferatorComboBoxNonProductive.gameObject.SetActive(false);
                __instance._proliferatorComboBoxIPF().gameObject.SetActive(true);
                __instance.currentProliferatorComboBox = __instance._proliferatorComboBoxIPF();
            }
            else if (recipe.productive && (Utils_ERecipeType)recipe.Type != Utils_ERecipeType.Fractionate) {
                __instance._proliferatorComboBoxNormal.gameObject.SetActive(true);
                __instance._proliferatorComboBoxNonProductive.gameObject.SetActive(false);
                __instance._proliferatorComboBoxIPF().gameObject.SetActive(false);
                __instance.currentProliferatorComboBox = __instance._proliferatorComboBoxNormal;
            }
            else {
                __instance._proliferatorComboBoxNormal.gameObject.SetActive(false);
                __instance._proliferatorComboBoxNonProductive.gameObject.SetActive(true);
                __instance._proliferatorComboBoxIPF().gameObject.SetActive(false);
                __instance.currentProliferatorComboBox = __instance._proliferatorComboBoxNonProductive;
            }
            __instance.currentProliferatorComboBox.SetStrategySlience(data.Options.Strategy);
            data.Options.Strategy = __instance.currentProliferatorComboBox.Strategy;
            __instance.proliferatorText.text = __instance.currentProliferatorComboBox.comboBox.m_Input.text;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProductDetail), "OnFactoryChange")]
        private static bool ProductDetail_OnFactoryChange_Prefix(ref ProductDetail __instance,
            (Utils_ERecipeType, ItemProto proto) obj) {
            __instance._data.Options.Factory = obj.proto;
            __instance.factoryButton.tips.tipTitle = obj.proto.name;
            //执行此方法以使用正确的增产策略（更换currentProliferatorComboBox到正确对象，并重设data.Options.Strategy）
            ProductDetail_SetData_Postfix(ref __instance, __instance._data);
            __instance.RefreshFactoryCount();
            __instance.RefreshNeeds();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProductDetail), "OnRecipePickerReturn")]
        private static bool ProductDetail_OnRecipePickerReturn_Prefix(ref ProductDetail __instance,
            RecipeProto recipeProto) {
            if (recipeProto == null) {
                return false;
            }
            __instance.recipeEntry.SetRecipe(recipeProto);
            __instance._data.Options.Recipe = recipeProto;
            __instance._data.CheckFactory();
            //执行此方法以使用正确的增产策略（更换currentProliferatorComboBox到正确对象，并重设data.Options.Strategy）
            ProductDetail_SetData_Postfix(ref __instance, __instance._data);
            __instance.RefreshFactoryCount();
            __instance.RefreshNeeds();
            return false;
        }

        #endregion
    }
}
