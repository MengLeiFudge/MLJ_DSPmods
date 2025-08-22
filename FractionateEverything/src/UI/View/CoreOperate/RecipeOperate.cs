using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Manager;
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

        Register("当前物品", "Current item");
        Register("配方操作提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("解锁/兑换配方", "Unlock/exchange recipe");
        Register("升至下一级", "Upgrade to next level");
        Register("升至最高级", "Upgrade to max level");
        Register("突破品质", "Breakthrough quality");

        Register("重置", "Reset");
        Register("降级", "Downgrade");
        Register("升级", "Upgrade");
        Register("升满", "Full upgrade");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("费用", "Cost");
        Register("每种精华", "Each essence");
        Register("成功率", "Success Rate");
        Register("损毁率", "Destroy Rate");
        Register("产出", "Output");

        Register("完全处理后的输出如下：", "The fully processed output is as follows:");
        Register("当前配方已完全升级！", "The current recipe has been completely upgraded!");
        Register("当前配方已到最高品质，未达到满级！",
            "The current recipe is of the highest quality, but has not reached the maximum level!");
        Register("当前配方品质可突破，突破条件：",
            "The current recipe quality can be broken through. Conditions for breaking through:");
        Register("达到当前品质最高等级（", "Reaching the highest current quality level (");
        Register("）", ")");
        Register("达到当前等级经验上限（", "Reach the current level experience cap (");
        Register("拥有足够的同名回响（", "Have sufficient echoes of the same name (");
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
        float y = 18f + 7f;
        textCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5, y, tab,
            SelectedItem.ID, "button-change-item",
            () => { OnButtonChangeItemClick(false); }, () => { OnButtonChangeItemClick(true); },
            "提示", "配方操作提示按钮说明1");
        //todo: 修复按钮提示窗后移除该内容
        wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5 + btnSelectedItem.Width + 5, y, tab,
            "提示", "配方操作提示按钮说明1");
        wnd.AddComboBox(GetPosition(1, 2).Item1, y, tab, "配方类型")
            .WithItems(RecipeTypeShortNames).WithSize(200, 0).WithConfigEntry(RecipeTypeEntry);
        y += 36f + 7f;
        if (!GameMain.sandboxToolsEnabled) {
            wnd.AddButton(0, 4, y, tab, "解锁/兑换配方",
                onClick: () => { GetRecipe(SelectedRecipe); });
            wnd.AddButton(1, 4, y, tab, "升至下一级",
                onClick: () => { UpgradeLevel(SelectedRecipe, false); });
            wnd.AddButton(2, 4, y, tab, "升至最高级",
                onClick: () => { UpgradeLevel(SelectedRecipe, true); });
            wnd.AddButton(3, 4, y, tab, "突破品质",
                onClick: () => { BreakthroughQualityUntilSuccess(SelectedRecipe); });
        } else {
            wnd.AddButton(0, 4, y, tab, "重置",
                onClick: () => { Reset(SelectedRecipe); });
            wnd.AddButton(1, 4, y, tab, "降级",
                onClick: () => { Downgrade(SelectedRecipe); });
            wnd.AddButton(2, 4, y, tab, "升级",
                onClick: () => { Upgrade(SelectedRecipe); });
            wnd.AddButton(3, 4, y, tab, "升满",
                onClick: () => { FullUpgrade(SelectedRecipe); });
        }
        y += 36f;
        textRecipeInfoBaseY = y;
        for (int i = 0; i < textRecipeInfo.Length; i++) {
            textRecipeInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"text-recipe-info-{i}");
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
            textRecipeInfo[line].text = "配方不存在！".Translate().WithColor(Red);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
        } else {
            textRecipeInfo[line].text = recipe.Unlocked
                ? $"{recipe.TypeNameWC} {recipe.LvExpWC}"
                : $"{recipe.TypeNameWC} {"分馏配方未解锁".Translate().WithColor(Red)}";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = $"{"费用".Translate()} 1 {SelectedItem.name}";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            if (recipeType == ERecipe.QuantumCopy) {
                QuantumCopyRecipe recipe0 = GetRecipe<QuantumCopyRecipe>(recipeType, SelectedItem.ID);
                textRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} {"每种精华".Translate()}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            }
            textRecipeInfo[line].text = $"{"成功率".Translate()} {recipe.SuccessRate:P3}".WithColor(Orange)
                                        + "      "
                                        + $"{"损毁率".Translate()} {recipe.DestroyRate:P3}".WithColor(Red);
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;
            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                textRecipeInfo[line].text = $"{(isFirst ? "产出".Translate() : "    ")} {info}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                textRecipeInfo[line].text = $"{(isFirst ? "其他".Translate() : "    ")} {info}";
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }

            textRecipeInfo[line].text = "";
            textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
            line++;

            textRecipeInfo[line].text = $"{LDB.items.Select(recipe.InputID).name} x 1 {"完全处理后的输出如下：".Translate()}";
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
                textRecipeInfo[line].text = "当前配方已完全升级！".Translate().WithColor(Orange);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            } else if (recipe.IsMaxQuality) {
                textRecipeInfo[line].text = "当前配方已到最高品质，未达到满级！".Translate().WithColor(Blue);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
            } else {
                textRecipeInfo[line].text = "当前配方品质可突破，突破条件：".Translate();
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityMaxLevel ? "√" : "x")}] "
                    + $"{"达到当前品质最高等级（".Translate()}{recipe.Level} / {recipe.CurrQualityMaxLevel}{"）".Translate()}"
                        .WithColor(recipe.IsCurrQualityMaxLevel ? Green : Red);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityCurrLevelMaxExp ? "√" : "x")}] "
                    + $"{"达到当前等级经验上限（".Translate()}{(int)recipe.Exp} / {recipe.CurrQualityCurrLevelExp}{"）".Translate()}"
                        .WithColor(recipe.IsCurrQualityCurrLevelMaxExp ? Green : Red);
                textRecipeInfo[line].SetPosition(0, textRecipeInfoBaseY + 24f * line);
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.IsEnoughMemoryToBreak ? "√" : "x")}] "
                    + $"{"拥有足够的同名回响（".Translate()}{recipe.Memory} / {recipe.BreakCurrQualityNeedMemory}{"）".Translate()}"
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
        QuantumCopyRecipe recipe0 = recipe as QuantumCopyRecipe;
        //增产剂影响后的概率
        float successRate = recipe.SuccessRate;
        if (recipe0 == null) {
            successRate *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
        }
        //损毁概率
        float destroyRate = recipe.DestroyRate;
        //最终产物处理率
        float processRate = (1 - destroyRate) * successRate / (destroyRate + (1 - destroyRate) * successRate);
        Dictionary<int, (float, bool, bool)> outputDic = [];
        float essenceCountAvg = 0.0f;
        foreach (var info in recipe.OutputMain) {
            int outputId = info.OutputID;
            float outputCount = processRate * info.SuccessRate * info.OutputCount * recipe.MainOutputCountInc;
            if (recipe0 != null) {
                float inc10 = (float)MaxTableMilli(10);
                float EssenceCostProlifeDec = (inc10 - (float)MaxTableMilli(fluidInputIncAvg) * 0.5f) / inc10;
                essenceCountAvg = recipe0.EssenceCost * recipe0.EssenceCostDec * EssenceCostProlifeDec;
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
            float outputCount = processRate * info.SuccessRate * recipe.AppendOutputRatioInc * info.OutputCount;
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
            sb.Append($"{"每种精华".Translate()} x -{essenceCountAvg:F3}");
        }
        return sb.ToString();
    }

    private static void GetRecipe(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (!GameMain.history.ItemUnlocked(ItemManager.itemToMatrix[recipe.InputID])) {
            UIMessageBox.Show("提示".Translate(),
                "当前物品尚未解锁！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsMaxMemory) {
            UIMessageBox.Show("提示".Translate(),
                "该配方回响数目已达到上限！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = IFE分馏配方通用核心;
        int takeCount = recipe.Locked ? 1 : Math.Max(0, recipe.BreakCurrQualityNeedMemory - recipe.Memory);
        if (takeCount == 0) {
            UIMessageBox.Show("提示".Translate(),
                "该配方回响数目已达到突破要求，暂时无法兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
            + $"{"来兑换".Translate()} {recipe.TypeNameWC} {"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                for (int i = 0; i < takeCount; i++) {
                    recipe.RewardThis();
                }
            },
            null);
    }

    private static void UpgradeLevel(BaseRecipe recipe, bool maxLevel) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.Locked) {
            UIMessageBox.Show("提示".Translate(),
                "配方尚未解锁！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.FullUpgrade) {
            UIMessageBox.Show("提示".Translate(),
                "配方已完全升级！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsCurrQualityCurrLevelMaxExp) {
            UIMessageBox.Show("提示".Translate(),
                "配方经验已达上限！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = I沙土;
        float needExp = maxLevel ? recipe.GetExpToMaxLevel() : recipe.GetExpToNextLevel();
        if (maxLevel && needExp <= 0) {
            UIMessageBox.Show("提示".Translate(),
                "配方已升至当前品质最高等级！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeCount = (int)Math.Ceiling(needExp * 0.5);
        ItemProto takeProto = LDB.items.Select(I沙土);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
            + $"{"来兑换".Translate()} {recipe.TypeNameWC} {"配方经验".Translate()} x {(int)needExp} {"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                recipe.AddExp(needExp, false);
            },
            null);
    }

    private static void BreakthroughQualityUntilSuccess(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.Locked) {
            UIMessageBox.Show("提示".Translate(),
                "配方尚未解锁！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.FullUpgrade) {
            UIMessageBox.Show("提示".Translate(),
                "配方已完全升级！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsMaxQuality) {
            UIMessageBox.Show("提示".Translate(),
                "配方已达到最高品质！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (!recipe.IsEnoughMemoryToBreak) {
            UIMessageBox.Show("提示".Translate(),
                "配方回响数目不足！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = I沙土;
        float needExp = recipe.GetExpToMaxLevel();
        int takeCount = (int)Math.Ceiling(needExp * 0.5);
        ItemProto takeProto = LDB.items.Select(I沙土);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()}{"一定量的".Translate()} {takeProto.name} "
            + $"{"来兑换".Translate()} {recipe.TypeNameWC} {"配方经验".Translate()}{"，直至突破成功".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                //升到当前品质满级
                if (!recipe.IsCurrQualityMaxLevel) {
                    if (!TakeItem(takeId, takeCount, out _)) {
                        return;
                    }
                    recipe.AddExp(needExp, false);
                }
                //购买经验突破品质，直至突破成功，或沙土不足
                int nextQuality = recipe.NextQuality;
                while (recipe.Quality != nextQuality) {
                    needExp = recipe.GetExpToNextLevel();
                    takeCount = (int)Math.Ceiling(needExp * 0.5);
                    if (!TakeItem(takeId, takeCount, out _)) {
                        return;
                    }
                    recipe.AddExp(needExp, false);
                }
            },
            null);
    }

    public static void Upgrade(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        recipe?.SandBoxUpDowngrade(true);
    }

    public static void Downgrade(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        recipe?.SandBoxUpDowngrade(false);
    }

    public static void FullUpgrade(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        recipe?.SandBoxMaxUpDowngrade(true);
    }

    public static void Reset(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        recipe?.SandBoxMaxUpDowngrade(false);
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
