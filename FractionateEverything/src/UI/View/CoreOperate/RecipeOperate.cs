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
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class RecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static Text txtCurrItem;
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
    private static Text txtCoreCount;
    private static Text[] txtRecipeInfo = new Text[30];
    private static float txtRecipeInfoBaseY = 0;

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
        //Register("增产点数", "Proliferator Points");//原版已翻译
        //Register("其他", "Others");//原版已翻译

        Register("完全处理后的输出如下：", "The fully processed output is as follows:");
        Register("配方已完全升级！", "Recipe has been completely upgraded!");
        Register("配方已到最高品质！", "Recipe has reached the highest quality!");
        Register("配方品质可突破，突破条件：",
            "Recipe quality can be broken through. Conditions for breaking through:");
        Register("达到当前品质最高等级（", "Reaching the highest current quality level (");
        Register("）", ")");
        Register("达到当前等级经验上限（", "Reach the current level experience cap (");
        Register("拥有足够的同名回响（", "Have sufficient echoes of the same name (");

        Register("当前物品尚未解锁，或科技层次不足！",
            "The current item has not been unlocked, or the technology level is insufficient!");
        Register("配方回响数目已达到上限！", "The number of recipe echoes has reached the limit!");
        Register("配方回响数目已达到突破要求，暂时无法兑换！",
            "The number of recipe echoes has reached the breakthrough requirement and cannot be exchanged for the time being!");
        Register("配方经验已达上限！", "Recipe experience has reached the limit!");
        Register("配方已升至当前品质最高等级！", "Recipe has been upgraded to the highest quality level currently available!");
        Register("配方回响数目不足！", "Insufficient number of recipe echoes!");
    }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "配方操作");
        float x = 0f;
        float y = 18f + 7f;
        txtCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        btnSelectedItem = wnd.AddImageButton(x + txtCurrItem.preferredWidth + 5, y, tab,
            SelectedItem.ID, "button-change-item",
            () => { OnButtonChangeItemClick(false); }, () => { OnButtonChangeItemClick(true); });
        //todo: 修复按钮提示窗后移除该内容
        wnd.AddTipsButton2(x + txtCurrItem.preferredWidth + 5 + btnSelectedItem.Width + 5, y, tab,
            "提示", "配方操作提示按钮说明1");
        wnd.AddComboBox(GetPosition(1, 4).Item1, y, tab, "配方类型")
            .WithItems(RecipeTypeShortNames).WithSize(200, 0).WithConfigEntry(RecipeTypeEntry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏配方通用核心);
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
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
        txtRecipeInfoBaseY = y;
        for (int i = 0; i < txtRecipeInfo.Length; i++) {
            txtRecipeInfo[i] = wnd.AddText2(x, y, tab, "动态刷新");
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedItem.ItemId = SelectedItem.ID;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        txtCoreCount.text = $"x {GetItemTotalCount(IFE分馏配方通用核心)}";
        int line = 0;
        if (recipe == null) {
            txtRecipeInfo[line].text = "配方不存在！".Translate().WithColor(Red);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
        } else if (recipe.Locked) {
            txtRecipeInfo[line].text = $"{recipe.TypeNameWC} {"分馏配方未解锁".Translate().WithColor(Red)}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
        } else {
            txtRecipeInfo[line].text = $"{recipe.TypeNameWC} {recipe.LvExpWC}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = $"{"费用".Translate()} 1 {SelectedItem.name}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
            if (recipeType == ERecipe.QuantumCopy) {
                QuantumCopyRecipe recipe0 = GetRecipe<QuantumCopyRecipe>(recipeType, SelectedItem.ID);
                txtRecipeInfo[line].text = $"         {recipe0.EssenceCost:F3} {"每种精华".Translate()}";
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            }
            txtRecipeInfo[line].text = $"{"成功率".Translate()} {recipe.SuccessRate:P3}".WithColor(Orange)
                                       + "      "
                                       + $"{"损毁率".Translate()} {recipe.DestroyRate:P3}".WithColor(Red);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                txtRecipeInfo[line].text = $"{(isFirst ? "产出".Translate() : "    ")} {info}";
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                txtRecipeInfo[line].text = $"{(isFirst ? "其他".Translate() : "    ")} {info}";
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }

            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = $"{LDB.items.Select(recipe.InputID).name} x 1 {"完全处理后的输出如下：".Translate()}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
            txtRecipeInfo[line].text = GetSameRecipeStr(recipe, 0);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
            if (!GenesisBook.Enable) {
                txtRecipeInfo[line].text = GetSameRecipeStr(recipe, 1);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                txtRecipeInfo[line].text = GetSameRecipeStr(recipe, 2);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            }
            txtRecipeInfo[line].text = GetSameRecipeStr(recipe, 4);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
            txtRecipeInfo[line].text = GetSameRecipeStr(recipe, 10);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            if (recipe.FullUpgrade) {
                txtRecipeInfo[line].text = "配方已完全升级！".Translate().WithColor(Orange);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            } else if (recipe.IsMaxQuality) {
                txtRecipeInfo[line].text = "配方已到最高品质！".Translate().WithColor(Blue);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            } else {
                txtRecipeInfo[line].text = "配方品质可突破，突破条件：".Translate();
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                txtRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityMaxLevel ? "√" : "x")}] "
                    + $"{"达到当前品质最高等级（".Translate()}{recipe.Level} / {recipe.CurrQualityMaxLevel}{"）".Translate()}"
                        .WithColor(recipe.IsCurrQualityMaxLevel ? Green : Red);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                txtRecipeInfo[line].text =
                    $"[{(recipe.IsCurrQualityCurrLevelMaxExp ? "√" : "x")}] "
                    + $"{"达到当前等级经验上限（".Translate()}{(int)recipe.Exp} / {recipe.CurrQualityCurrLevelExp}{"）".Translate()}"
                        .WithColor(recipe.IsCurrQualityCurrLevelMaxExp ? Green : Red);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
                txtRecipeInfo[line].text =
                    $"[{(recipe.IsEnoughMemoryToBreak ? "√" : "x")}] "
                    + $"{"拥有足够的同名回响（".Translate()}{recipe.Memory} / {recipe.BreakCurrQualityNeedMemory}{"）".Translate()}"
                        .WithColor(recipe.IsEnoughMemoryToBreak ? Green : Red);
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            }
            //todo: 展示配方特殊加成
            // txtRecipeInfo[line].text = "特殊突破加成：无";
            // line++;
        }
        for (; line < txtRecipeInfo.Length; line++) {
            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, 0);
        }
    }

    private static string GetSameRecipeStr(BaseRecipe recipe, int fluidInputIncAvg) {
        var recipe0 = recipe as QuantumCopyRecipe;
        ItemProto building = LDB.items.Select(recipe.RecipeType.GetSpriteItemId());
        float pointsBonus = (float)ProcessManager.MaxTableMilli(fluidInputIncAvg);
        float buffBonus1 = building.ReinforcementBonusFracSuccess();
        float buffBonus2 = building.ReinforcementBonusMainOutputCount();
        float buffBonus3 = building.ReinforcementBonusAppendOutputRate();
        //成功率
        float successRate = recipe0 == null
            ? recipe.SuccessRate * (1 + pointsBonus) * (1 + buffBonus1)
            : recipe.SuccessRate * (1 + buffBonus1);
        //损毁率
        float destroyRate = recipe.DestroyRate;
        //最终产物转化率
        float processRate = (1 - destroyRate) * successRate / (destroyRate + (1 - destroyRate) * successRate);
        Dictionary<int, (float, bool, bool)> outputDic = [];
        float essenceCostAvg = 0.0f;
        foreach (var info in recipe.OutputMain) {
            int outputId = info.OutputID;
            float outputCount = processRate;
            outputCount *= info.SuccessRate;
            outputCount *= info.OutputCount * (1 + recipe.MainOutputCountInc + buffBonus2);
            if (recipe0 != null) {
                float EssenceDec2 = pointsBonus * 0.5f / (float)ProcessManager.MaxTableMilli(10);
                essenceCostAvg = recipe0.EssenceCost * (1 - recipe0.EssenceDec) * (1 - EssenceDec2);
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
            float outputCount = processRate;
            outputCount *= info.SuccessRate * (1 + recipe.AppendOutputRatioInc) * (1 + buffBonus3);
            outputCount *= info.OutputCount;
            if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                tuple.Item1 += outputCount;
            } else {
                tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRate);
            }
            outputDic[outputId] = tuple;
        }
        StringBuilder sb = new($"{"增产点数".Translate()} {fluidInputIncAvg:D2}    ");
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        foreach (var p in outputDic) {
            var tuple = p.Value;
            sb.Append($"{(tuple.Item2 || sandboxMode ? LDB.items.Select(p.Key).name : "???")}"
                      + $" x {(tuple.Item3 || sandboxMode ? tuple.Item1.ToString("F3") : "???")}  ");
        }
        if (recipe0 != null) {
            sb.Append($"{"每种精华".Translate()} x -{essenceCostAvg:F3}");
        }
        return sb.ToString();
    }

    private static void GetRecipe(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (!GameMain.history.ItemUnlocked(recipe.InputID)
            || !GameMain.history.ItemUnlocked(ItemManager.itemToMatrix[recipe.InputID])) {
            UIMessageBox.Show("提示".Translate(),
                "当前物品尚未解锁，或科技层次不足！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsMaxMemory) {
            UIMessageBox.Show("提示".Translate(),
                "配方回响数目已达到上限！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = IFE分馏配方通用核心;
        int takeCount = recipe.Locked ? 1 : Math.Max(0, recipe.BreakCurrQualityNeedMemory - recipe.Memory);
        if (takeCount == 0) {
            UIMessageBox.Show("提示".Translate(),
                "配方回响数目已达到突破要求，暂时无法兑换！".Translate(),
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
                if (!TakeItemWithTip(takeId, takeCount, out _)) {
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
                "配方不存在！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.Locked) {
            UIMessageBox.Show("提示".Translate(),
                $"{"分馏配方未解锁".Translate()}{"！".Translate()}",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.FullUpgrade) {
            UIMessageBox.Show("提示".Translate(),
                "配方已完全升级！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsCurrQualityCurrLevelMaxExp) {
            UIMessageBox.Show("提示".Translate(),
                "配方经验已达上限！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = I沙土;
        float needExp = maxLevel ? recipe.GetExpToMaxLevel() : recipe.GetExpToNextLevel();
        if (maxLevel && needExp <= 0) {
            UIMessageBox.Show("提示".Translate(),
                "配方已升至当前品质最高等级！".Translate(),
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
                if (!TakeItemWithTip(takeId, takeCount, out _)) {
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
                "配方不存在！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.Locked) {
            UIMessageBox.Show("提示".Translate(),
                $"{"分馏配方未解锁".Translate()}{"！".Translate()}",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.FullUpgrade) {
            UIMessageBox.Show("提示".Translate(),
                "配方已完全升级！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsMaxQuality) {
            UIMessageBox.Show("提示".Translate(),
                "配方已到最高品质！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (!recipe.IsEnoughMemoryToBreak) {
            UIMessageBox.Show("提示".Translate(),
                "配方回响数目不足！".Translate(),
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
                    if (!TakeItemWithTip(takeId, takeCount, out _)) {
                        return;
                    }
                    recipe.AddExp(needExp, false);
                }
                //购买经验突破品质，直至突破成功，或沙土不足
                int nextQuality = recipe.NextQuality;
                while (recipe.Quality != nextQuality) {
                    needExp = recipe.GetExpToNextLevel();
                    takeCount = (int)Math.Ceiling(needExp * 0.5);
                    if (!TakeItemWithTip(takeId, takeCount, out _)) {
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
