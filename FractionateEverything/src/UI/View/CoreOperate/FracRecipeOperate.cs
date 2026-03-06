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
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class FracRecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static Text txtCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick(bool showLocked, float y) {
        //物品选取窗口左上角的X值（anchoredPosition是中心点）
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        //物品选取窗口左上角的Y值（anchoredPosition是中心点）
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
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

    private const int InfoLineCount = 35;
    private static Text[] txtRecipeInfo = new Text[InfoLineCount];
    private static MyImageButton[] btnRecipeInfoIcons = new MyImageButton[InfoLineCount];
    private static float txtRecipeInfoBaseY;
    private static MySlider incSlider;
    private static ConfigEntry<int> selectedInc;

    private const float IconSize = 24f;
    private const float TextOffsetWithIcon = 28f;
    private const float LineHeight = 24f;

    public static void AddTranslations() {
        Register("分馏配方", "Fractionate Recipe");

        Register("当前物品", "Current item");
        Register("分馏配方提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("解锁配方", "Unlock recipe");
        Register("兑换回响", "Exchange echo");
        Register("无法解锁", "Can not unlock");
        Register("升至下一级", "Upgrade to next level");
        Register("升至最高级", "Upgrade to max level");

        Register("回响", "Echo");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("费用", "Cost");
        Register("每种精华", "Each essence");
        Register("成功率", "Success Ratio");
        Register("损毁率", "Destroy Ratio");
        Register("产出", "Output");
        //Register("增产点数", "Proliferator Points");//原版已翻译
        //Register("其他", "Others");//原版已翻译

        Register("完全处理后的输出如下：", "The fully processed output is as follows:");
        Register("配方已完全升级！", "Recipe has been completely upgraded!");

        Register("当前物品尚未解锁，或科技层次不足！",
            "The current item has not been unlocked, or the technology level is insufficient!");
        Register("配方回响数目已达到上限！", "The number of recipe echoes has reached the limit!");
        Register("配方回响数目已达到突破要求，暂时无法兑换！",
            "The number of recipe echoes has reached the breakthrough requirement and cannot be exchanged for the time being!");
        Register("配方经验已达上限！", "Recipe experience has reached the limit!");
        Register("配方已升至当前品质最高等级！", "Recipe has been upgraded to the highest level currently available!");
        Register("配方回响数目不足！", "Insufficient number of recipe echoes!");

        Register("建筑强化加成", "Building Enhancement Bonuses");
        Register("等级", "Level");
        Register("堆叠", "Stack");
        Register("能耗比", "Energy Ratio");
        Register("增产效率", "Proliferator Efficiency");
        Register("流体增强", "Fluid Enhancement");
        Register("成功率加成", "Success Boost");
        Register("已启用", "Enabled");
        Register("未启用", "Disabled");
        Register("牺牲特性", "Sacrifice Trait");
        Register("维度共鸣", "Dimensional Resonance");
        Register("质能裂变", "Mass-Energy Fission");
        Register("零压循环", "Zero Pressure Cycle");
        Register("因果追踪", "Causal Tracing");
        Register("单锁", "Single Lock");
        Register("虚空喷射", "Void Spray");
        Register("双倍点数", "Double Points");
        Register("最大增产等级", "Max Inc Level");
    }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
        selectedInc = configFile.Bind("Recipe Operate", "Selected Inc", 0, "想要查看的最终输出的增产点数");
        if (selectedInc.Value is < 0 or > 10) {
            selectedInc.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "分馏配方");
        float x = 0f;
        float y = 18f + 7f;
        txtCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        float popupY = y + (36f + 7f) / 2;
        btnSelectedItem = wnd.AddImageButton(x + txtCurrItem.preferredWidth + 5, y, tab,
            SelectedItem, "button-change-item").WithClickEvent(
            () => { OnButtonChangeItemClick(false, popupY); },
            () => { OnButtonChangeItemClick(true, popupY); });
        wnd.AddTipsButton2(x + txtCurrItem.preferredWidth + 5 + btnSelectedItem.Width + 5, y, tab,
            "提示", "分馏配方提示按钮说明1");
        var txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "配方类型");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(RecipeTypeShortNames).WithSize(200, 0).WithConfigEntry(RecipeTypeEntry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE分馏配方核心));
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        if (!GameMain.sandboxToolsEnabled) {
            wnd.AddButton(0, 1, y, tab, "兑换回响",
                onClick: () => { SelectedRecipe.GetRecipeEcho(); });
        } else {
            wnd.AddButton(0, 4, y, tab, "重置等级",
                onClick: () => { SelectedRecipe.ChangeLevelTo(0); });
            wnd.AddButton(1, 4, y, tab, "等级-1",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level - 1); });
            wnd.AddButton(2, 4, y, tab, "等级+1",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level + 1); });
            wnd.AddButton(3, 4, y, tab, "等级升满",
                onClick: () => { SelectedRecipe.ChangeLevelTo(10); });
        }
        int[] rang;
        if (!GenesisBook.Enable) {
            rang = [0, 1, 2, 4, 10];
        } else {
            rang = [0, 4, 10];
        }
        incSlider = wnd.AddSlider(0f, 0f, tab,
            selectedInc, rang, null, 200f);

        y += 36f;
        txtRecipeInfoBaseY = y;
        for (int i = 0; i < InfoLineCount; i++) {
            txtRecipeInfo[i] = wnd.AddText2(x, y, tab, "动态刷新");
        }
        for (int i = 0; i < InfoLineCount; i++) {
            var btn = MyImageButton.CreateImageButton(0, 0, tab, null);
            btn.WithSize(IconSize, IconSize);
            btn.gameObject.SetActive(false);
            btnRecipeInfoIcons[i] = btn;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedItem.Proto = SelectedItem;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        txtCoreCount.text = $"x {GetItemTotalCount(IFE分馏配方核心)}";
        ItemProto building = LDB.items.Select(recipeType.GetSpriteItemId());

        int line = 0;
        incSlider.gameObject.SetActive(false);

        if (recipe == null) {
            ShowTextLine(line++, "配方不存在！".Translate().WithColor(Red));
        } else if (recipe.Locked) {
            ShowTextLine(line++, $"{recipe.TypeNameWC} {"分馏配方未解锁".Translate().WithColor(Red)}");
        } else {
            ShowTextLine(line++, $"{recipe.TypeNameWC}");
            ShowTextLine(line++, "");

            ShowIconLine(line++, LDB.items.Select(recipe.InputID),
                $"x 1   {"成功率".Translate()} {recipe.SuccessRatio:P3}".WithColor(Orange)
                + "      "
                + $"{"损毁率".Translate()} {recipe.DestroyRatio:P3}".WithColor(Red));

            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                ShowIconLine(line++, LDB.items.Select(info.OutputID),
                    $"{(isFirst ? "产出".Translate() : "    ")} {info}");
                isFirst = false;
            }

            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                ShowIconLine(line++, LDB.items.Select(info.OutputID),
                    $"{(isFirst ? "其他".Translate() : "    ")} {info}");
                isFirst = false;
            }

            ShowTextLine(line++, "");

            ShowIconLine(line++, LDB.items.Select(recipe.InputID),
                $"x 1 {"完全处理后的输出如下：".Translate()}");

            HideIconOnLine(line);
            txtRecipeInfo[line].text = $"{"增产点数".Translate()}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + LineHeight * line);
            incSlider.SetPosition(120, txtRecipeInfoBaseY + LineHeight * line);
            incSlider.gameObject.SetActive(true);
            line++;

            string sameRecipeStr = GetSameRecipeStr(recipe, selectedInc.Value, building);
            string[] strs = sameRecipeStr.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in strs) {
                ShowTextLine(line++, str);
            }

            ShowTextLine(line++, "");

            if (building != null) {
                ShowIconLine(line++, building,
                    $"{"建筑强化加成".Translate()} {building.name}  {"等级".Translate()} +{building.Level()}");

                ShowTextLine(line++,
                    $"{"堆叠".Translate()} x{building.MaxStack()}  " +
                    $"{"能耗比".Translate()} {building.EnergyRatio():P0}  " +
                    $"{"增产效率".Translate()} x{building.PlrRatio():F1}");

                float successBoost = building.SuccessBoost();
                ShowTextLine(line++,
                    $"{"成功率加成".Translate()} +{successBoost:P1}"
                        .WithColor(successBoost > 0 ? Orange : Gray));

                bool fluidEnh = building.EnableFluidEnhancement();
                ShowTextLine(line++,
                    $"{"流体增强".Translate()}：" +
                    (fluidEnh
                        ? "已启用".Translate().WithColor(Green)
                        : "未启用".Translate().WithColor(Gray)));

                line = ShowBuildingFeatures(line, building);
            }
        }

        for (; line < InfoLineCount; line++) {
            HideAllLine(line);
        }
    }

    private static int ShowBuildingFeatures(int line, ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                ShowTextLine(line++,
                    $"{"牺牲特性".Translate()}：{FeatureStatus(InteractionTower.EnableSacrificeTrait)}  " +
                    $"{"维度共鸣".Translate()}：{FeatureStatus(InteractionTower.EnableDimensionalResonance)}");
                break;
            case IFE矿物复制塔:
                ShowTextLine(line++,
                    $"{"质能裂变".Translate()}：{FeatureStatus(MineralReplicationTower.EnableMassEnergyFission)}  " +
                    $"{"零压循环".Translate()}：{FeatureStatus(MineralReplicationTower.EnableZeroPressureCycle)}");
                break;
            case IFE转化塔:
                ShowTextLine(line++,
                    $"{"因果追踪".Translate()}：{FeatureStatus(ConversionTower.EnableCausalTracing)}  " +
                    $"{"单锁".Translate()}：{FeatureStatus(ConversionTower.EnableSingleLock)}");
                break;
            case IFE点数聚集塔:
                ShowTextLine(line++,
                    $"{"虚空喷射".Translate()}：{FeatureStatus(PointAggregateTower.EnableVoidSpray)}  " +
                    $"{"双倍点数".Translate()}：{FeatureStatus(PointAggregateTower.EnableDoublePoints)}  " +
                    $"{"最大增产等级".Translate()} {PointAggregateTower.MaxInc}");
                break;
        }
        return line;
    }

    private static void ShowIconLine(int line, ItemProto itemProto, string text) {
        float lineY = txtRecipeInfoBaseY + LineHeight * line;
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], 0, lineY);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(TextOffsetWithIcon, lineY);
    }

    private static void ShowTextLine(int line, string text) {
        float lineY = txtRecipeInfoBaseY + LineHeight * line;
        HideIconOnLine(line);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(0, lineY);
    }

    private static void HideIconOnLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
    }

    private static void HideAllLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = "";
        txtRecipeInfo[line].SetPosition(0, 0);
    }

    private static string FeatureStatus(bool enabled) =>
        enabled ? "已启用".Translate().WithColor(Green) : "未启用".Translate().WithColor(Gray);

    private static string GetSameRecipeStr(BaseRecipe recipe, int fluidInputIncAvg, ItemProto building) {
        // 同时应用建筑的增产效率加成（PlrRatio），与实际游戏逻辑一致
        float plrRatio = building?.PlrRatio() ?? 1.0f;
        float pointsBonus = (float)ProcessManager.MaxTableMilli(fluidInputIncAvg) * plrRatio;
        float successBoost = building?.SuccessBoost() ?? 0f;
        //成功率
        float successRatio = recipe.SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        //损毁率
        float destroyRatio = recipe.DestroyRatio;
        //最终产物转化率（考虑"直通后继续处理"）
        float processRatio = (1 - destroyRatio) * successRatio / (destroyRatio + (1 - destroyRatio) * successRatio);
        //原料不消耗会触发再次处理，采用几何级数期望系数：1 / (1 - processRatio * remainInputRatio)
        float remainInputRatio = recipe.RemainInputRatio;
        float processRepeatRatio = processRatio * remainInputRatio;
        float repeatMultiplier = processRepeatRatio >= 0.9999f ? 10000.0f : 1.0f / (1.0f - processRepeatRatio);
        float mainOutputBonus = 1.0f + recipe.DoubleOutputRatio;
        Dictionary<int, (float, bool, bool)> outputDic = [];
        foreach (var info in recipe.OutputMain) {
            int outputId = info.OutputID;
            float outputCount = processRatio;
            outputCount *= info.SuccessRatio;
            outputCount *= info.OutputCount;
            outputCount *= mainOutputBonus;
            outputCount *= repeatMultiplier;
            if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                tuple.Item1 += outputCount;
            } else {
                tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRatio);
            }
            outputDic[outputId] = tuple;
        }
        foreach (var info in recipe.OutputAppend) {
            int outputId = info.OutputID;
            float outputCount = processRatio;
            outputCount *= info.SuccessRatio;
            outputCount *= info.OutputCount;
            outputCount *= repeatMultiplier;
            if (outputDic.TryGetValue(outputId, out (float, bool, bool) tuple)) {
                tuple.Item1 += outputCount;
            } else {
                tuple = (outputCount, info.ShowOutputName, info.ShowSuccessRatio);
            }
            outputDic[outputId] = tuple;
        }
        StringBuilder sb = new($"{"增产点数".Translate()} {fluidInputIncAvg:D2}    ");
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        int lineCount = 1;
        foreach (var p in outputDic) {
            var tuple = p.Value;
            if (sb.Length > 80 * lineCount) {
                sb.AppendLine();
                sb.Append("    ");
                lineCount++;
            }
            sb.Append($"{(tuple.Item2 || sandboxMode ? LDB.items.Select(p.Key).name : "???")}"
                      + $" x {(tuple.Item3 || sandboxMode ? tuple.Item1.ToString("F3") : "???")}  ");
        }
        return sb.ToString();
    }

    private static void GetRecipeEcho(this BaseRecipe recipe) {
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
            || !GameMain.history.ItemUnlocked(recipe.MatrixID)) {
            UIMessageBox.Show("提示".Translate(),
                "当前物品尚未解锁，或科技层次不足！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = IFE分馏配方核心;
        int takeCount = (int)Math.Pow(2, recipe.Level);
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
                    recipe.RewardThis(true);
                }
            },
            null);
    }

    private static void ChangeLevelTo(this BaseRecipe recipe, int target) {
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
        recipe.ChangeLevelTo(target);
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
