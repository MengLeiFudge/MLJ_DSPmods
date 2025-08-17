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
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class RecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
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
    private static ERecipe SelectedRecipeType => RecipeTypes[RecipeTypeEntry.Value];
    private static BaseRecipe SelectedRecipe => GetRecipe<BaseRecipe>(SelectedRecipeType, SelectedItem.ID);
    private static Text[] textRecipeInfo = new Text[30];
    private static float textRecipeInfoBaseY = 0;

    public static void AddTranslations() {
        Register("配方操作", "Recipe Operate");
        Register("每种精华", "Each essence");
    }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "配方操作");
        float x = 0f;
        float y = 20f;
        textCurrItem = wnd.AddText2(x, y, tab, "当前物品：", 15, "textCurrItem");
        btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5f, y, tab,
            SelectedItem.ID, "button-change-item",
            () => { OnButtonChangeItemClick(false); }, () => { OnButtonChangeItemClick(true); },
            "切换说明", "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换");
        //todo: 修复按钮提示窗后移除该内容
        wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5f + 60, y, tab,
            "切换说明", "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换");
        wnd.AddComboBox(x + 250, y, tab, "配方类型").WithItems(RecipeTypeShortNames).WithSize(150f, 0f)
            .WithConfigEntry(RecipeTypeEntry);
        y += 50f;
        wnd.AddButton(x, y, 300, tab, "使用分馏配方通用核心兑换此配方", 16, "button-get-recipe",
            () => { ExchangeItem2Recipe(IFE分馏配方通用核心, 1, SelectedRecipe); });
        wnd.AddButton(x + 350, y, 300, tab, "使用沙土兑换配方经验", 16, "button-get-recipe-exp",
            () => { ExchangeSand2RecipeExp(SelectedRecipe); });
        y += 36f;
        textRecipeInfoBaseY = y;
        for (int i = 0; i < textRecipeInfo.Length; i++) {
            textRecipeInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"text-recipe-info-{i}");
            // y += 20f;
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
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
        } else {
            textRecipeInfo[line].text = recipe.Unlocked
                ? $"{recipe.TypeNameWC} {recipe.LvExpWC}"
                : $"{recipe.TypeNameWC} {"配方未解锁".WithColor(Red)}";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = $"费用 1 {SelectedItem.name}";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            if (recipeType == ERecipe.QuantumCopy) {
                QuantumCopyRecipe recipe0 = GetRecipe<QuantumCopyRecipe>(recipeType, SelectedItem.ID);
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} {"每种精华".Translate()}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            }
            textRecipeInfo[line].text = $"成功率 {recipe.SuccessRate:P3}".WithColor(Orange)
                                        + "      "
                                        + $"损毁率 {recipe.DestroyRate:P3}".WithColor(Red);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                textRecipeInfo[line].text = $"{(isFirst ? "产出" : "    ")} {info}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                textRecipeInfo[line].text = $"{(isFirst ? "其他" : "    ")} {info}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }

            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = $"{LDB.items.Select(recipe.InputID).name} x 1 完全处理后的输出如下：";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 0);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            if (!GenesisBook.Enable) {
                textRecipeInfo[line].text = GetSameRecipeStr(recipe, 1);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text = GetSameRecipeStr(recipe, 2);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            }
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 4);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            textRecipeInfo[line].text = GetSameRecipeStr(recipe, 10);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            if (recipe.FullUpgrade) {
                textRecipeInfo[line].text = "当前配方已完全升级！".WithColor(Orange);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            } else if (recipe.IsMaxQuality) {
                textRecipeInfo[line].text = "当前配方已到最高品质，未达到满级！".WithColor(Blue);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            } else {
                textRecipeInfo[line].text = "当前配方品质可突破，突破条件：";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityMaxLevel ? "√" : "x")}] 达到当前品质最高等级（{recipe.Level} / {recipe.CurrQualityMaxLevel}）"
                        .WithColor(recipe.IsCurrQualityMaxLevel ? Green : Red);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityCurrLevelMaxExp ? "√" : "x")}] 达到当前等级经验上限（{(int)recipe.Exp} / {recipe.CurrQualityCurrLevelExp}）"
                        .WithColor(recipe.IsCurrQualityCurrLevelMaxExp ? Green : Red);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsEnoughMemoryToBreak ? "√" : "x")}] 拥有足够的同名回响（{recipe.Memory} / {recipe.BreakCurrQualityNeedMemory}）"
                        .WithColor(recipe.IsEnoughMemoryToBreak ? Green : Red);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            }
            // textRecipeInfo[line].text = "特殊突破加成：无";
            // line++;
        }
        for (; line < textRecipeInfo.Length; line++) {
            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, 0);
        }
    }

    private static string GetSameRecipeStr(BaseRecipe recipe, int fluidInputIncAvg) {
        //增产剂影响后的概率
        float successRate = recipe.SuccessRate * (1.0f + (float)MaxTableMilli(fluidInputIncAvg));
        //损毁概率
        float destroyRate = recipe.DestroyRate;
        //最终产物处理率
        float processRate = (1 - destroyRate) * successRate / (destroyRate + (1 - destroyRate) * successRate);
        Dictionary<int, (float, bool, bool)> outputDic = [];
        QuantumCopyRecipe recipe0 = recipe as QuantumCopyRecipe;
        float essenceCount = 0.0f;
        foreach (var info in recipe.OutputMain) {
            int outputId = info.OutputID;
            float outputCount = processRate * info.SuccessRate * info.OutputCount * recipe.MainOutputCountInc;
            if (recipe0 != null) {
                essenceCount += outputCount * recipe0.EssenceCost * recipe0.EssenceCostDec;
            }
            if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                tuple.Item1 += outputCount;
            } else {
                tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRate);
            }
            outputDic[outputId] = tuple;
        }
        foreach (var info in recipe.OutputAppend) {
            int outputId = info.OutputID;
            float outputCount = processRate * info.SuccessRate * info.OutputCount * recipe.AppendOutputCountInc;
            if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                tuple.Item1 += outputCount;
            } else {
                tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRate);
            }
            outputDic[outputId] = tuple;
        }
        StringBuilder sb = new StringBuilder($"{"增产点数".Translate()} {fluidInputIncAvg:D2}    ");
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        foreach (var p in outputDic) {
            var tuple = p.Value;
            sb.Append($"{(tuple.Item2 || sandboxMode ? LDB.items.Select(p.Key).name : "???")}"
                      + $" x {(tuple.Item3 || sandboxMode ? tuple.Item1.ToString("F3") : "???")}  ");
        }
        if (recipe0 != null) {
            sb.Append($"{"每种精华".Translate()} x -{essenceCount:F3}");
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
