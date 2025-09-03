using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonAPI.Systems;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class ItemManager {
    public static void AddTranslations() {
        Register("商店刷新提示", "The shop has been refreshed!", "商店已刷新！");
        Register("I商店刷新提示",
            $"The shop has been refreshed, don't forget to claim your relief supplies! ~\n(This is just a store refresh prompt and has no practical use. However, {"you should NOT be able to see this text, right?".WithColor(Red)})",
            $"商店已刷新，别忘了领取救济粮哦~\n（只是一个商店刷新的提示，没有实际用途。但是，{"你应该看不到这段话才对呀？".WithColor(Red)}）");

        Register("电磁奖券", "Electromagnetic Ticket");
        Register("I电磁奖券",
            "A high-tech ticket with a lot of electromagnetic matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量电磁矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("能量奖券", "Energy Ticket");
        Register("I能量奖券",
            "A high-tech ticket with a lot of energy matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量能量矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("结构奖券", "Structure Ticket");
        Register("I结构奖券",
            "A high-tech ticket with a lot of structure matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量结构矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("信息奖券", "Information Ticket");
        Register("I信息奖券",
            "A high-tech ticket with a lot of information matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量信息矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("引力奖券", "Gravity Ticket");
        Register("I引力奖券",
            "A high-tech ticket with a lot of gravity matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量引力矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("宇宙奖券", "Universe Ticket");
        Register("I宇宙奖券",
            "A high-tech ticket with a lot of universe matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量宇宙矩阵。也是分馏数据中心某些奖池的抽奖凭证。");
        Register("黑雾奖券", "Dark Fog Ticket");
        Register("I黑雾奖券",
            "A high-tech ticket with a lot of dark fog matrices encapsulated inside. It is also a lottery voucher for certain card pools in the data centre.",
            "一张高科技奖券，内部封装了大量黑雾矩阵。也是分馏数据中心某些奖池的抽奖凭证。");

        Register("分馏塔原胚普通", "Frac Building Proto(Normal)", "分馏塔原胚（普通）");
        Register("I分馏塔原胚普通",
            "The precursor of the fractionator can be seen everywhere. Various fractionators can be cultured from interactive towers, or transformed into other embryos from conversion towers.",
            "随处可见的分馏塔原胚。可由交互塔培养出各种分馏塔，或由转化塔转化为其他原胚。");
        Register("分馏塔原胚精良", "Frac Building Proto(Uncommon)", "分馏塔原胚（精良）");
        Register("I分馏塔原胚精良",
            "Carefully crafted distillation tower precursor. Various fractionators can be cultured from interactive towers, or transformed into other embryos from conversion towers.",
            "精心打造的分馏塔原胚。可由交互塔培养出各种分馏塔，或由转化塔转化为其他原胚。");
        Register("分馏塔原胚稀有", "Frac Building Proto(Rare)", "分馏塔原胚（稀有）");
        Register("I分馏塔原胚稀有",
            "Limited edition distillation tower embryo for sale. Various fractionators can be cultured from interactive towers, or transformed into other embryos from conversion towers.",
            "限量发售的分馏塔原胚。可由交互塔培养出各种分馏塔，或由转化塔转化为其他原胚。");
        Register("分馏塔原胚史诗", "Frac Building Proto(Epic)", "分馏塔原胚（史诗）");
        Register("I分馏塔原胚史诗",
            "A once-in-a-century rare distillation tower embryo. Various fractionators can be cultured from interactive towers, or transformed into other embryos from conversion towers.",
            "百年难遇的分馏塔原胚。可由交互塔培养出各种分馏塔，或由转化塔转化为其他原胚。");
        Register("分馏塔原胚传说", "Frac Building Proto(Legendary)", "分馏塔原胚（传说）");
        Register("I分馏塔原胚传说",
            "The primitive distillation tower with a long history. Various fractionators can be cultured from interactive towers, or transformed into other embryos from conversion towers.",
            "历史悠久的分馏塔原胚。可由交互塔培养出各种分馏塔，或由转化塔转化为其他原胚。");
        Register("分馏塔原胚定向", "Frac Building Proto(Directional)", "分馏塔原胚（定向）");
        Register("I分馏塔原胚定向",
            "Artificially synthesized distillation tower embryo. It can be directly cultivated as a designated fractionator.",
            "人工合成的分馏塔原胚。可以直接培养为指定的分馏塔。");
        Register("分馏配方通用核心", "Fractionate Recipe Core");
        Register("I分馏配方通用核心",
            "A cube containing peculiar energy. You can submit it to the COSMO and exchange it for any fractionation formula.",
            "含有奇特能量的立方体。可以提交给主脑，兑换任意分馏配方。");
        Register("分馏塔增幅芯片", "Fractionator Increase Chip");
        Register("I分馏塔增幅芯片",
            "Highly integrated electronic chips. It can be submitted to the COSMO to enhance the effectiveness of various distillation towers.",
            "高度集成的电子芯片。可以提交给主脑，增强各种分馏塔的效果。");

        Register("复制精华", "Copy Essence");
        Register("I复制精华",
            "The essence produced by the mineral replication tower is one of the consumables required for the operation of the quantum replication tower.",
            "矿物复制塔产出的精华，是量子复制塔运转所需的耗材之一。");
        Register("点金精华", "Alchemy Essence");
        Register("I点金精华",
            "The essence produced by the golden tower is one of the consumables required for the operation of the quantum replication tower.",
            "点金塔产出的精华，是量子复制塔运转所需的耗材之一。");
        Register("分解精华", "Deconstruction Essence");
        Register("I分解精华",
            "The essence produced by the decomposition tower is one of the consumables required for the operation of the quantum replication tower.",
            "分解塔产出的精华，是量子复制塔运转所需的耗材之一。");
        Register("转化精华", "Conversion Essence");
        Register("I转化精华",
            "The essence produced by the conversion tower is one of the consumables required for the operation of the quantum replication tower.",
            "转化塔产出的精华，是量子复制塔运转所需的耗材之一。");
    }

    #region 添加新物品

    /// <summary>
    /// 添加部分物品
    /// </summary>
    public static void AddFractionalPrototypeAndEssence() {
        // EItemType
        // Unknown,
        // Resource,   原矿（铁矿、铜矿等）
        // Material,   原矿熔炼的材料（铁板、铜板、增产剂、钛合金等）
        // Component,  材料加工的产物（磁线圈、电路板等）
        // Product,    消耗品（弹药、燃料棒、无人机等）
        // Logistics,  运输相关（传送带、分拣器、储物仓、电线杆等）
        // Production, 实体机器（发电机、制作台等）
        // Decoration, 地基
        // Turret,     进攻建筑
        // Defense,    防御建筑
        // DarkFog,    黑雾掉落
        // Matrix,     矩阵

        // item.UnlockKey 未设置的话，为正数，表示物品解锁需要看配方是否解锁
        // item.UnlockKey = -1 表示物品直接解锁
        // item.UnlockKey = -2 表示黑雾物品
        // recipe.IconPath = "" 表示配方不需要独有图标，直接使用产物[0]的图标
        // recipe.Handcraft = false 表示配方禁止手动制造
        // recipe.NonProductive = true 表示增产剂仅能加速，不能增产
        // RecipeUnlocked Patch 用于调整配方解锁状态（Item直接用UnlockKey=-1，就不需要patch item的）

        ProtoRegistry.RegisterItem(IFE商店刷新提示, "商店刷新提示", "I商店刷新提示",
            Tech1134IconPath, 0, 100, EItemType.Material);

        ItemProto item;
        RecipeProto recipe;

        item = ProtoRegistry.RegisterItem(IFE电磁奖券, "电磁奖券", "I电磁奖券",
            "Assets/fe/electromagnetic-ticket", tab分馏 * 1000 + 101, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE电磁奖券,
            ERecipeType.Assemble, 120, [I电磁矩阵], [10], [IFE电磁奖券], [1],
            "I电磁奖券", TFE电磁奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE能量奖券, "能量奖券", "I能量奖券",
            "Assets/fe/energy-ticket", tab分馏 * 1000 + 102, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.red, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE能量奖券,
            ERecipeType.Assemble, 150, [I能量矩阵], [10], [IFE能量奖券], [1],
            "I能量奖券", TFE能量奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE结构奖券, "结构奖券", "I结构奖券",
            "Assets/fe/structure-ticket", tab分馏 * 1000 + 103, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE结构奖券,
            ERecipeType.Assemble, 180, [I结构矩阵], [10], [IFE结构奖券], [1],
            "I结构奖券", TFE结构奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE信息奖券, "信息奖券", "I信息奖券",
            "Assets/fe/information-ticket", tab分馏 * 1000 + 104, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE信息奖券,
            ERecipeType.Assemble, 210, [I信息矩阵], [10], [IFE信息奖券], [1],
            "I信息奖券", TFE信息奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE引力奖券, "引力奖券", "I引力奖券",
            "Assets/fe/gravity-ticket", tab分馏 * 1000 + 105, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE引力奖券,
            ERecipeType.Assemble, 240, [I引力矩阵], [10], [IFE引力奖券], [1],
            "I引力奖券", TFE引力奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE宇宙奖券, "宇宙奖券", "I宇宙奖券",
            "Assets/fe/universe-ticket", tab分馏 * 1000 + 106, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.white, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE宇宙奖券,
            ERecipeType.Assemble, 300, [I宇宙矩阵], [10], [IFE宇宙奖券], [1],
            "I宇宙奖券", TFE宇宙奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE黑雾奖券, "黑雾奖券", "I黑雾奖券",
            "Assets/fe/dark-fog-ticket", tab分馏 * 1000 + 107, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE黑雾奖券,
            ERecipeType.Assemble, 600, [I黑雾矩阵], [200], [IFE黑雾奖券], [1],
            "I黑雾奖券", TFE黑雾奖券, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;


        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚普通, "分馏塔原胚普通", "I分馏塔原胚普通",
            "Assets/fe/frac-proto-normal", tab分馏 * 1000 + 201, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.white, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚精良, "分馏塔原胚精良", "I分馏塔原胚精良",
            "Assets/fe/frac-proto-uncommon", tab分馏 * 1000 + 202, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚稀有, "分馏塔原胚稀有", "I分馏塔原胚稀有",
            "Assets/fe/frac-proto-rare", tab分馏 * 1000 + 203, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚史诗, "分馏塔原胚史诗", "I分馏塔原胚史诗",
            "Assets/fe/frac-proto-epic", tab分馏 * 1000 + 204, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚传说, "分馏塔原胚传说", "I分馏塔原胚传说",
            "Assets/fe/frac-proto-legendary", tab分馏 * 1000 + 205, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔原胚定向, "分馏塔原胚定向", "I分馏塔原胚定向",
            "Assets/fe/frac-proto-directional", tab分馏 * 1000 + 206, 30, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.red, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE分馏塔原胚定向, ERecipeType.Assemble, 300,
            [IFE分馏塔原胚普通, IFE分馏塔原胚精良, IFE分馏塔原胚稀有, IFE分馏塔原胚史诗, IFE分馏塔原胚传说], [1, 1, 1, 1, 1],
            [IFE分馏塔原胚定向], [1],
            "I分馏塔原胚定向", TFE分馏塔原胚, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE分馏配方通用核心, "分馏配方通用核心", "I分馏配方通用核心",
            "Assets/fe/frac-recipe-core", tab分馏 * 1000 + 207, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏塔增幅芯片, "分馏塔增幅芯片", "I分馏塔增幅芯片",
            "Assets/fe/building-increase-chip", tab分馏 * 1000 + 208, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        item.UnlockKey = -1;


        item = ProtoRegistry.RegisterItem(IFE复制精华, "复制精华", "I复制精华",
            "Assets/fe/copy-essence", tab分馏 * 1000 + 501, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.cyan, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE点金精华, "点金精华", "I点金精华",
            "Assets/fe/alchemy-essence", tab分馏 * 1000 + 502, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分解精华, "分解精华", "I分解精华",
            "Assets/fe/deconstruction-essence", tab分馏 * 1000 + 503, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE转化精华, "转化精华", "I转化精华",
            "Assets/fe/conversion-essence", tab分馏 * 1000 + 504, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        item.UnlockKey = -1;
    }

    #endregion

    #region 计算物品价值，以及交互塔可接受物品范围

    public const float maxValue = float.MaxValue;
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly float[] itemValue = new float[12000];
    /// <summary>
    /// 交互塔可接收的所有物品id
    /// </summary>
    public static int[] needs = [];
#if DEBUG
    private const string ITEM_VALUE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
    private const string ITEM_VALUE_CSV_PATH = $@"{ITEM_VALUE_CSV_DIR}\itemValue.csv";
#endif

    /// <summary>
    /// 计算所有物品的价值
    /// </summary>
    public static void CalculateItemValues() {
        //设置普通原矿价值
        itemValue[I铁矿] = 1.0f;
        itemValue[I铜矿] = 1.0f;
        itemValue[IGB铝矿] = 1.0f;
        itemValue[IGB钨矿] = 1.0f;
        itemValue[I煤矿] = 1.0f;
        itemValue[I石矿] = 1.0f;
        itemValue[IGB硫矿] = 1.2f;
        itemValue[IGB放射性矿物] = 1.2f;
        itemValue[I木材] = 1.0f;
        itemValue[I植物燃料] = 1.0f;
        itemValue[I沙土] = 1.0f;
        //设置母星系其他星球普通原矿价值
        itemValue[I硅石] = 2.0f;
        itemValue[I钛石] = 2.0f;
        //设置其他星系珍奇矿物价值
        itemValue[I可燃冰] = 5.0f;
        itemValue[I金伯利矿石] = 8.0f;
        itemValue[I分形硅石] = 8.0f;
        itemValue[I有机晶体] = 8.0f;
        itemValue[I光栅石] = 20.0f;
        itemValue[I刺笋结晶] = 20.0f;
        itemValue[I单极磁石] = 200.0f;
        //设置气巨、冰巨可开采物品价值
        itemValue[I氢] = 2.0f;
        itemValue[I重氢] = 5.0f;
        itemValue[IGB氦] = 20.0f;
        //itemValue[I可燃冰] = 2.0f;
        //itemValue[IGB氨] = 3.0f;
        //设置可直接抽取的物品价值
        itemValue[I原油] = 1.0f;
        itemValue[IGB海水] = 1.0f;
        itemValue[I水] = 1.0f;
        itemValue[IGB盐酸] = 5.0f;
        itemValue[I硫酸] = 5.0f;
        itemValue[IGB硝酸] = 5.0f;
        itemValue[IGB氨] = 5.0f;
        itemValue[IGB二氧化硫] = 5.0f;
        itemValue[IGB二氧化碳] = 5.0f;
        itemValue[IGB氮] = 3.0f;
        //设置黑雾掉落价值
        itemValue[I能量碎片] = 2f;
        itemValue[I黑雾矩阵] = 2.5f;
        itemValue[I物质重组器] = 4.5f;
        itemValue[I硅基神经元] = 6.0f;
        itemValue[I负熵奇点] = 7.5f;
        itemValue[I核心素] = 30f;
        //设置临界光子价值
        itemValue[I临界光子] = 400.0f;
        //设置分馏塔原胚价值
        itemValue[IFE分馏塔原胚普通] = 200.0f;
        itemValue[IFE分馏塔原胚精良] = 400.0f;
        itemValue[IFE分馏塔原胚稀有] = 800.0f;
        itemValue[IFE分馏塔原胚史诗] = 1600.0f;
        itemValue[IFE分馏塔原胚传说] = 3200.0f;
        //设置精华价值
        itemValue[IFE复制精华] = 500.0f;
        itemValue[IFE点金精华] = 500.0f;
        itemValue[IFE分解精华] = 500.0f;
        itemValue[IFE转化精华] = 500.0f;
        //不存在的物品价值都设为特定值，这样也会将上面某些物品重置为maxValue（某些Mod未开启的情况下会有）
        for (int i = 0; i < itemValue.Length; i++) {
            if (itemValue[i] == 0 || !LDB.items.Exist(i)) {
                itemValue[i] = maxValue;
            }
        }
        //获取所有配方（排除分馏配方、含有多功能集成组件的配方、GridIndex超限配方）
        var iEnumerable = LDB.recipes.dataArray.Where(r =>
            r.Type != ERecipeType.Fractionate
            && !r.Items.Contains(IMS多功能集成组件)
            && !r.Results.Contains(IMS多功能集成组件)
            && r.GridIndexValid());
        var recipes = iEnumerable.ToArray();

        //迭代计算价值
        bool changed;
        int iteration = 0;

        do {
            changed = false;
            iteration++;

            foreach (var recipe in recipes) {
                // 复制配方数据
                List<int> inputIDs = recipe.Items.ToList();
                List<int> outputIDs = recipe.Results.ToList();
                List<int> inputCounts = recipe.ItemCounts.ToList();
                List<int> outputCounts = recipe.ResultCounts.ToList();
                // 抵消输入输出中的相同物品
                bool haveSameItem;
                do {
                    haveSameItem = false;
                    for (int i = 0; i < inputIDs.Count; i++) {
                        for (int j = 0; j < outputIDs.Count; j++) {
                            if (inputIDs[i] == outputIDs[j]) {
                                // 比较数量大小并抵消
                                if (inputCounts[i] > outputCounts[j]) {
                                    inputCounts[i] -= outputCounts[j];
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                } else if (inputCounts[i] < outputCounts[j]) {
                                    outputCounts[j] -= inputCounts[i];
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                } else {
                                    // 数量相等，完全抵消
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                }
                                haveSameItem = true;
                                break;
                            }
                        }
                        if (haveSameItem) break;
                    }
                } while (haveSameItem);

                // 检查输入物品是否都有已知价值
                bool canProcess = true;
                foreach (int itemId in inputIDs) {
                    if (Math.Abs(itemValue[itemId] - maxValue) < 0.0001f) {
                        canProcess = false;
                        break;
                    }
                }
                if (!canProcess) continue;

                // 计算输入总价值和输出总单位数
                float inputValue = 0;
                for (int i = 0; i < inputIDs.Count; i++) {
                    inputValue += inputCounts[i] * itemValue[inputIDs[i]];
                }

                int outputUnits = outputCounts.Sum();

                // 如果输出总单位数为0，则跳过（没有净产出）
                if (outputUnits <= 0) continue;

                // 计算时间成本，原料价值越高则单位时间的价值越高
                // 别问为什么参数是 0.03 和 1.5，问就是经验
                float adjustedTimeValue = recipe.TimeSpend / 60.0f * (0.03f * inputValue + 1.5f);

                // 计算单位价值
                float unitValue = (inputValue + adjustedTimeValue) / outputUnits;

                // 更新输出物品价值（取最小值）
                foreach (int itemId in outputIDs) {
                    if (unitValue < itemValue[itemId]) {
                        itemValue[itemId] = unitValue;
                        ItemProto item = LDB.items.Select(itemId);
                        // LogDebug($"更新物品{item.name}({itemId})价值为{unitValue:F3}("
                        //          + $"{inputValue / outputUnits:F3}+{adjustedTimeValue / outputUnits:F3})");
                        if (itemId == I蓄电器) {
                            itemValue[I蓄电器满] = unitValue * 2;
                        }
                        changed = true;
                    }
                }
            }
        } while (changed && iteration < 10);

        //设置核心、芯片价值
        itemValue[IFE分馏配方通用核心] = itemValue[IFE宇宙奖券] / 0.05f;
        itemValue[IFE分馏塔增幅芯片] = itemValue[IFE宇宙奖券] / 0.03f;

        //设置多功能集成组件的价值
        iEnumerable = LDB.recipes.dataArray.Where(r => r.Items.Length == 1
                                                       && r.Items[0] == IMS多功能集成组件
                                                       && r.Results.Length > 0
                                                       && !r.Results.Contains(IMS多功能集成组件));
        float maxCalculatedValue = 0f;
        // 为每个配方分别计算多功能集成组件的价值
        foreach (var recipe in iEnumerable) {
            // 计算产物总价值
            float outputValue = 0f;
            for (int i = 0; i < recipe.Results.Length; i++) {
                outputValue += recipe.ResultCounts[i] * itemValue[recipe.Results[i]];
            }
            float inputCount = recipe.ItemCounts[0];
            float timeSpend = recipe.TimeSpend / 60.0f;
            // 根据公式反向推算多功能集成组件的价值
            // 产物价值 = 原材料价值 + 制作时间价值
            // outputValue = inputCount * x + timeSpend * (0.03f * inputCount * x + 1.5f)
            // 其中 x 是 itemValue[IMS多功能集成组件]
            //
            // 展开得到：
            // outputValue = inputCount * x + timeSpend * 0.03f * inputCount * x + timeSpend * 1.5f
            // outputValue = x * inputCount * (1 + timeSpend * 0.03f) + timeSpend * 1.5f
            //
            // 解得：
            // x = (outputValue - timeSpend * 1.5f) / (inputCount * (1 + timeSpend * 0.03f))
            if (inputCount > 0 && (inputCount * (1 + timeSpend * 0.03f)) > 0) {
                float calculatedValue = (outputValue - timeSpend * 1.5f)
                                        / (inputCount * (1 + timeSpend * 0.03f));
                maxCalculatedValue = Math.Max(maxCalculatedValue, calculatedValue);
            }
        }
        // 使用所有配方计算结果的最大值
        if (maxCalculatedValue > 0 && maxCalculatedValue < maxValue && maxCalculatedValue > itemValue[IMS多功能集成组件]) {
            itemValue[IMS多功能集成组件] = maxCalculatedValue;
        }

#if DEBUG
        //按照从小到大的顺序输出所有物品的原材料点数
        if (Directory.Exists(ITEM_VALUE_CSV_DIR)) {
            using StreamWriter sw = new StreamWriter(ITEM_VALUE_CSV_PATH);
            sw.WriteLine("ID,名称,价值");
            Dictionary<int, float> dic = [];
            for (int i = 0; i < itemValue.Length; i++) {
                if (LDB.items.Exist(i)) {
                    dic[i] = itemValue[i];
                }
            }
            foreach (var p in dic.OrderBy(p => p.Value)) {
                ItemProto item = LDB.items.Select(p.Key);
                sw.WriteLine($"{p.Key},{item.name},{p.Value:F2}");
            }
        }
#endif
        //根据物品价值构建交互塔可接受物品列表
        needs = LDB.items.dataArray
            .Where(item => itemValue[item.ID] < maxValue && item.GridIndexValid())
            .Select(item => item.ID)
            .ToArray();
    }

    #endregion

    #region 将物品根据前置科技分类到不同矩阵层级

    public static readonly int[] itemToMatrix = new int[12000];

    public static void ClassifyItemsToMatrix() {
        //       物品状态                         missingTech    preTech
        //         正常                              false        tech
        //黑雾特有材料（UnlockKey=-2），或资源        false        null
        // 找不到主制作配方的对应科技                 true         null
        foreach (var item in LDB.items.dataArray) {
            int topMatrixID;
            if (item.ID == IFE分馏配方通用核心 || item.ID == IFE分馏塔增幅芯片) {
                //核心与芯片只有转化配方，归到宇宙矩阵
                topMatrixID = I宇宙矩阵;
            } else if (item.Type == EItemType.Matrix) {
                //矩阵归到自己的层级，而非上一层级
                topMatrixID = item.ID switch {
                    IGB玻色矩阵 => I能量矩阵,
                    IGB耗散矩阵 => I信息矩阵,
                    IGB奇点矩阵 => I引力矩阵,
                    _ => item.ID
                };
            } else if (item.UnlockKey == -1 || item.Type == EItemType.Resource || item.ID == I沙土) {
                //原矿归到电磁矩阵
                topMatrixID = I电磁矩阵;
            } else if (item.UnlockKey == -2) {
                //黑雾特有掉落归到黑雾矩阵
                topMatrixID = I黑雾矩阵;
            } else if (item.preTech != null) {
                //大部分物品归到前置科技所属的矩阵层级。如果找不到前置科技所属的矩阵层级，归到电磁矩阵
                int id = GetTopMatrixID(item.preTech);
                topMatrixID = id > 0 ? id : I电磁矩阵;
            } else if (!item.missingTech) {
                //黑雾特有材料或资源
                topMatrixID = item.UnlockKey == -2 ? I黑雾矩阵 : I电磁矩阵;
            } else {
                //主制作配方无前置科技（铁块），或没有主制作配方（分馏配方通用核心）
                //此时尝试从其他配方的原料确认该物品可能的层级。如果仍未找到，归到黑雾矩阵
                List<RecipeProto> recipes = LDB.recipes.dataArray
                    .Where(r => r.Items.Contains(item.ID)).ToList();
                if (recipes.Count == 0) {
                    topMatrixID = I黑雾矩阵;
                } else {
                    topMatrixID = int.MaxValue;
                    foreach (RecipeProto recipe in recipes) {
                        if (recipe.preTech != null) {
                            int id = GetTopMatrixID(recipe.preTech);
                            if (id > 0 && id < topMatrixID) {
                                topMatrixID = id;
                            }
                        }
                    }
                    if (topMatrixID == int.MaxValue) {
                        topMatrixID = I黑雾矩阵;
                    }
                }
            }
            itemToMatrix[item.ID] = topMatrixID;
            // LogDebug($"物品{item.name}({item.ID})归类到{LDB.items.Select(topMatrixID).name}({topMatrixID})");
        }
    }

    private static int GetTopMatrixID(TechProto tech) {
        if (tech.IsHiddenTech || tech.Items.Contains(I黑雾矩阵)) {
            return I黑雾矩阵;
        }
        int topMatrixID = 0;
        for (int j = 0; j < tech.Items.Length; j++) {
            int matrixID = tech.Items[j];
            if (LDB.items.Select(matrixID).Type == EItemType.Matrix) {
                matrixID = matrixID switch {
                    IGB玻色矩阵 => I能量矩阵,
                    IGB耗散矩阵 => I信息矩阵,
                    IGB奇点矩阵 => I引力矩阵,
                    _ => matrixID
                };
                topMatrixID = Math.Max(topMatrixID, matrixID);
            }
        }
        return topMatrixID;
    }

    #endregion

    #region 分馏数据中心背包（也就是Mod物品缓存区数据）

    public static readonly long[] centerItemCount = new long[12000];
    public static readonly long[] centerItemInc = new long[12000];

    #endregion

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int itemDataDicSize = r.ReadInt32();
        for (int i = 0; i < itemDataDicSize; i++) {
            int itemId = r.ReadInt32();
            long count = r.ReadInt64();
            long inc = r.ReadInt64();
            if (count < 0) {
                count = 0;
            }
            if (inc < 0) {
                inc = 0;
            } else if (inc > count * 10) {
                inc = count * 10;
            }
            if (itemId >= 0 && itemId < centerItemCount.Length) {
                centerItemCount[itemId] = count;
                centerItemInc[itemId] = inc;
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        List<int> centerItemId = [];
        for (int i = 0; i < centerItemCount.Length; i++) {
            if (centerItemCount[i] > 0) {
                centerItemId.Add(i);
            }
        }
        w.Write(centerItemId.Count);
        foreach (int itemId in centerItemId) {
            w.Write(itemId);
            w.Write(centerItemCount[itemId]);
            w.Write(centerItemInc[itemId]);
        }
    }

    public static void IntoOtherSave() {
        for (int i = 0; i < centerItemCount.Length; i++) {
            centerItemCount[i] = 0;
            centerItemInc[i] = 0;
        }
    }

    #endregion
}
