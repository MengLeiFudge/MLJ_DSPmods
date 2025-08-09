using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class TabRecipeAndBuilding {
    public static RectTransform _windowTrans;

    #region 选择物品

    public static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    public static int SelectedItemId => SelectedItem.ID;
    private static Text textCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick(bool showLocked) {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = _windowTrans.anchoredPosition.x + _windowTrans.rect.width / 2;
        float y = _windowTrans.anchoredPosition.y + _windowTrans.rect.height / 2;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(SelectedRecipeType, item.ID);
            return recipe != null && (showLocked || recipe.Unlocked);
        });
    }

    #endregion

    #region 配方详情

    public static ConfigEntry<int> RecipeTypeEntry;
    public static string[] RecipeTypeNames;
    public static ERecipe[] RecipeTypes = [
        ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.QuantumCopy,
        ERecipe.Alchemy, ERecipe.Deconstruction, ERecipe.Conversion,
    ];
    public static ERecipe SelectedRecipeType => RecipeTypes[RecipeTypeEntry.Value];
    public static BaseRecipe SelectedRecipe => GetRecipe<BaseRecipe>(SelectedRecipeType, SelectedItem.ID);
    private static Text[] textRecipeInfo = new Text[30];

    #endregion

    #region 建筑加成

    public static ConfigEntry<int> BuildingTypeEntry;
    public static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[BuildingTypeEntry.Value]);
    public static string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(),
        "量子复制塔".Translate(), "点金塔".Translate(), "分解塔".Translate(), "转化塔".Translate()
    ];
    public static int[] BuildingIds = [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔];

    private static Text textBuildingInfo1;
    private static UIButton btnBuildingInfo1;
    private static Text textBuildingInfo2;
    private static UIButton btnBuildingInfo2;
    private static Text textBuildingInfo3;
    private static UIButton btnBuildingInfo3;
    private static Text textBuildingInfo4;
    private static UIButton btnTip4;
    private static UIButton btnBuildingInfo4;

    #endregion

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
        RecipeTypeNames = new string[RecipeTypes.Length];
        for (int i = 0; i < RecipeTypeNames.Length; i++) {
            RecipeTypeNames[i] = RecipeTypes[i].GetShortName();
        }
        BuildingTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Building Type", 0, "想要查看的建筑类型。");
        if (BuildingTypeEntry.Value < 0 || BuildingTypeEntry.Value >= BuildingTypeNames.Length) {
            BuildingTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "配方&建筑", "tab-group-fe1");
        {
            var tab = wnd.AddTab(trans, "配方详情");
            x = 0f;
            y = 10f;
            textCurrItem = wnd.AddText2(x, y + 5f, tab, "当前物品：", 15, "textCurrItem");
            btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5f, y, tab,
                SelectedItem.ID, "button-change-item",
                () => { OnButtonChangeItemClick(false); }, () => { OnButtonChangeItemClick(true); },
                "切换说明", "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换");
            //todo: 修复按钮提示窗后移除该内容
            wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5f + 60, y + 11f, tab,
                "切换说明", "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换");
            wnd.AddComboBox(x + 250, y + 5f, tab, "配方类型").WithItems(RecipeTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(RecipeTypeEntry);
            y += 50f;
            wnd.AddButton(x, y, 300, tab, "使用分馏配方通用核心兑换此配方", 16, "button-get-recipe",
                () => { ExchangeItem2Recipe(IFE分馏配方通用核心, 1, SelectedRecipe); });
            wnd.AddButton(x + 350, y, 300, tab, "使用沙土兑换配方经验", 16, "button-get-recipe-exp",
                () => { ExchangeSand2RecipeExp(SelectedRecipe); });
            y += 36f;
            for (int i = 0; i < textRecipeInfo.Length; i++) {
                textRecipeInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"text-recipe-info-{i}");
                y += 20f;
            }
        }
        {
            var tab = wnd.AddTab(trans, "建筑加成");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "建筑类型").WithItems(BuildingTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(BuildingTypeEntry);
            y += 36f;

            wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
            y += 36f;
            textBuildingInfo1 = wnd.AddText2(x, y, tab, "流动输出堆叠", 15, "text-building-info-1");
            wnd.AddTipsButton2(x + 200, y + 6, tab, "流动输出堆叠",
                "启用后，流动输出（即侧面的输出）会尽可能以4堆叠进行输出。");
            btnBuildingInfo1 = wnd.AddButton(x + 350, y, tab, "启用", 16, "button-enable-fluid-output-stack",
                SetFluidOutputStack);
            y += 36f;
            textBuildingInfo2 = wnd.AddText2(x, y, tab, "产物输出最大堆叠", 15, "text-building-info-2");
            wnd.AddTipsButton2(x + 200, y + 6, tab, "产物输出最大堆叠",
                "产物输出（即正面的输出）会尽可能以该项的数目进行输出。");
            btnBuildingInfo2 = wnd.AddButton(x + 350, y, tab, "堆叠+1", 16, "button-add-max-product-output-stack",
                AddMaxProductOutputStack);
            y += 36f;
            textBuildingInfo3 = wnd.AddText2(x, y, tab, "分馏永动", 15, "text-building-info-3");
            wnd.AddTipsButton2(x + 200, y + 6, tab, "分馏永动",
                "启用后，当产物缓存到达一定数目后，建筑将不再处理输入的物品，而是直接将其搬运到流动输出。\n该功能可以确保环路的持续运行。");
            btnBuildingInfo3 = wnd.AddButton(x + 350, y, tab, "启用", 16, "button-enable-frac-forever",
                SetFracForever);
            y += 36f;
            textBuildingInfo4 = wnd.AddText2(x, y, tab, "点数聚集效率层次", 15, "text-building-info-4");
            btnTip4 = wnd.AddTipsButton2(x + 200, y + 6, tab, "点数聚集效率层次",
                "点数聚集的效率层次会影响产物的输出速率、产物的最大增产点数，上限为7。");
            btnBuildingInfo4 = wnd.AddButton(x + 350, y, tab, "层次+1", 16, "button-add-point-aggregate-level",
                AddPointAggregateLevel);
            y += 36f;
        }
    }

    public static void UpdateUI() {
        btnSelectedItem.SetSprite(SelectedItem.iconSprite);
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        int line = 0;
        if (recipe == null) {
            textRecipeInfo[line].text = "配方不存在！".WithColor(Red);
            line++;
        } else {
            textRecipeInfo[line].text = recipe.Unlocked
                ? $"{recipe.TypeNameWC} {recipe.LvExpWC}"
                : $"{recipe.TypeNameWC} {"配方未解锁".WithColor(Red)}";
            line++;

            textRecipeInfo[line].text = "";
            line++;

            textRecipeInfo[line].text = $"费用 1 {SelectedItem.name}";
            line++;
            if (recipeType == ERecipe.QuantumCopy) {
                QuantumCopyRecipe recipe0 = GetRecipe<QuantumCopyRecipe>(recipeType, SelectedItem.ID);
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} 复制精华";
                line++;
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} 点金精华";
                line++;
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} 分解精华";
                line++;
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} 转化精华";
                line++;
            }
            textRecipeInfo[line].text = $"成功率 {recipe.SuccessRate:P3}".WithColor(Orange)
                                        + "      "
                                        + $"损毁率 {recipe.DestroyRate:P3}".WithColor(Red);
            line++;
            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                textRecipeInfo[line].text = $"{(isFirst ? "产出" : "    ")} {info}";
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                textRecipeInfo[line].text = $"{(isFirst ? "其他" : "    ")} {info}";
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }

            textRecipeInfo[line].text = "";
            line++;

            textRecipeInfo[line].text = $"{LDB.items.Select(recipe.InputID).name} x 1 完全处理后的输出如下：";
            line++;
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 0);
            line++;
            if (!GenesisBook.Enable) {
                textRecipeInfo[line].text = GetSameRecipeStr(recipe, 1);
                line++;
                textRecipeInfo[line].text = GetSameRecipeStr(recipe, 2);
                line++;
            }
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 4);
            line++;
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 10);
            line++;

            textRecipeInfo[line].text = "";
            line++;

            if (recipe.FullUpgrade) {
                textRecipeInfo[line].text = "当前配方已完全升级！".WithColor(Orange);
                line++;
            } else if (recipe.IsMaxQuality) {
                textRecipeInfo[line].text = "当前配方已到最高品质，未达到满级！".WithColor(Blue);
                line++;
            } else {
                textRecipeInfo[line].text = "当前配方品质可突破，突破条件：";
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityMaxLevel ? "√" : "x")}] 达到当前品质最高等级（{recipe.Level} / {recipe.CurrQualityMaxLevel}）"
                        .WithColor(recipe.IsCurrQualityMaxLevel ? Green : Red);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityCurrLevelMaxExp ? "√" : "x")}] 达到当前等级经验上限（{(int)recipe.Exp} / {recipe.CurrQualityCurrLevelExp}）"
                        .WithColor(recipe.IsCurrQualityCurrLevelMaxExp ? Green : Red);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsEnoughMemoryToBreak ? "√" : "x")}] 拥有足够的同名回响（{recipe.Memory} / {recipe.BreakCurrQualityNeedMemory}）"
                        .WithColor(recipe.IsEnoughMemoryToBreak ? Green : Red);
                line++;
            }
            // textRecipeInfo[line].text = "特殊突破加成：无";
            // line++;
        }
        for (; line < textRecipeInfo.Length; line++) {
            textRecipeInfo[line].text = "";
        }

        textBuildingInfo1.text = SelectedBuilding.EnableFluidOutputStack()
            ? "已启用流动输出堆叠".WithColor(Orange)
            : "未启用流动输出堆叠".WithColor(Red);
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnBuildingInfo1.gameObject.SetActive(!SelectedBuilding.EnableFluidOutputStack());

        string s = $"产物输出堆叠：{SelectedBuilding.MaxProductOutputStack()}";
        textBuildingInfo2.text = SelectedBuilding.MaxProductOutputStack() >= 4
            ? s.WithColor(Orange)
            : s.WithQualityColor(SelectedBuilding.MaxProductOutputStack());
        btnBuildingInfo2.gameObject.SetActive(SelectedBuilding.MaxProductOutputStack() < 4);

        textBuildingInfo3.text = SelectedBuilding.EnableFracForever()
            ? "已启用分馏永动".WithColor(Orange)
            : "未启用分馏永动".WithColor(Red);
        btnBuildingInfo3.gameObject.SetActive(!SelectedBuilding.EnableFracForever());

        if (SelectedBuilding.ID == IFE点数聚集塔) {
            s = $"点数聚集效率层次：{PointAggregateTower.Level}";
            textBuildingInfo4.text = s.WithPALvColor(PointAggregateTower.Level);
            textBuildingInfo4.enabled = true;
            btnTip4.gameObject.SetActive(true);
            btnBuildingInfo4.gameObject.SetActive(!PointAggregateTower.IsMaxLevel);
        } else {
            textBuildingInfo4.enabled = false;
            btnTip4.gameObject.SetActive(false);
            btnBuildingInfo4.gameObject.SetActive(false);
        }
    }

    private static string GetSameRecipeStr(BaseRecipe recipe, int fluidInputIncAvg) {
        float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
        float inputCount = 1.0f;
        Dictionary<int, (float, bool, bool)> outputDic = [];
        QuantumCopyRecipe recipe0 = recipe as QuantumCopyRecipe;
        float essenceCount = 0.0f;
        int iterations = 0;
        while (inputCount > 1e-6 && iterations < 1000) {
            iterations++;
            //输入减去损毁的量
            inputCount *= 1.0f - recipe.DestroyRate;
            //计算有多少物品会被处理，增产会影响这一轮处理的数目
            float processCount = Math.Min(inputCount, inputCount * recipe.SuccessRate * successRatePlus);
            //输入减去被处理的量
            inputCount -= processCount;
            //如果是量子复制配方，累加扣除精华的数目
            if (recipe0 != null) {
                essenceCount += processCount * recipe0.EssenceCost;
            }
            //计算被处理的物品能产出多少物品
            foreach (var info in recipe.OutputMain) {
                int outputId = info.OutputID;
                float outputCount = processCount * info.SuccessRate * info.OutputCount;
                if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                    tuple.Item1 += outputCount;
                } else {
                    tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRate);
                }
                outputDic[outputId] = tuple;
            }
            foreach (var info in recipe.OutputAppend) {
                int outputId = info.OutputID;
                float outputCount = processCount * info.SuccessRate * info.OutputCount;
                if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                    tuple.Item1 += outputCount;
                } else {
                    tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRate);
                }
                outputDic[outputId] = tuple;
            }
        }
        StringBuilder sb = new StringBuilder($"增产点数{fluidInputIncAvg}：");
        foreach (var p in outputDic) {
            var tuple = p.Value;
            sb.Append(
                $"{(tuple.Item2 ? LDB.items.Select(p.Key).name : "???")} x {(tuple.Item3 ? tuple.Item1.ToString("F3") : "???")}  ");
        }
        if (recipe0 != null) {
            sb.Append($"{LDB.items.Select(IFE复制精华).name} x -{essenceCount:F3}  ")
                .Append($"{LDB.items.Select(IFE点金精华).name} x -{essenceCount:F3}  ")
                .Append($"{LDB.items.Select(IFE分解精华).name} x -{essenceCount:F3}  ")
                .Append($"{LDB.items.Select(IFE转化精华).name} x -{essenceCount:F3}  ");
        }
        return sb.ToString();
    }

    private static void SetFluidOutputStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.EnableFluidOutputStack()) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 2;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 启用流动输出堆叠吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFluidOutputStack(true);
                UIMessageBox.Show("提示", "已启用流动输出堆叠！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    private static void AddMaxProductOutputStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        if (SelectedBuilding.MaxProductOutputStack() >= 4) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 将产物输出堆叠 +1 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.MaxProductOutputStack(SelectedBuilding.MaxProductOutputStack() + 1);
                UIMessageBox.Show("提示", "已将产物输出堆叠 +1！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    private static void SetFracForever() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.EnableFracForever()) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 3;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 启用分馏永动吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFracForever(true);
                UIMessageBox.Show("提示", "已启用分馏永动！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    private static void AddPointAggregateLevel() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        if (PointAggregateTower.Level >= 7) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 将点数聚集效率层次 +1 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                PointAggregateTower.Level++;
                UIMessageBox.Show("提示", "已将点数聚集效率层次 +1！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
