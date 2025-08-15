using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class RecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static int SelectedItemId => SelectedItem.ID;
    private static Text textCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick(bool showLocked) {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = window.anchoredPosition.x + window.rect.width / 2;
        float y = window.anchoredPosition.y + window.rect.height / 2;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(SelectedRecipeType, item.ID);
            return recipe != null && (showLocked || recipe.Unlocked);
        });
    }

    private static ConfigEntry<int> RecipeTypeEntry;
    private static string[] RecipeTypeNames;
    private static ERecipe[] RecipeTypes = [
        ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.QuantumCopy,
        ERecipe.Alchemy, ERecipe.Deconstruction, ERecipe.Conversion,
    ];
    private static ERecipe SelectedRecipeType => RecipeTypes[RecipeTypeEntry.Value];
    private static BaseRecipe SelectedRecipe => GetRecipe<BaseRecipe>(SelectedRecipeType, SelectedItem.ID);
    private static Text[] textRecipeInfo = new Text[30];

    public static void AddTranslations() {
        Register("配方操作", "Recipe Operate");
    }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
        RecipeTypeNames = new string[RecipeTypes.Length];
        for (int i = 0; i < RecipeTypeNames.Length; i++) {
            RecipeTypeNames[i] = RecipeTypes[i].GetShortName();
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "配方操作");
        float x = 0f;
        float y = 10f;
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

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
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
