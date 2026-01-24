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
    private static Text[] txtRecipeInfo = new Text[30];
    private static float txtRecipeInfoBaseY = 0;
    private static MySlider incSlider;
    private static ConfigEntry<int> selectedInc;

    public static void AddTranslations() {
        Register("分馏配方", "Fractionate Recipe");

        Register("当前物品", "Current item");
        Register("配方操作提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("解锁配方", "Unlock recipe");
        Register("兑换回响", "Exchange echo");
        Register("无法解锁", "Can not unlock");
        Register("升至下一级", "Upgrade to next level");
        Register("升至最高级", "Upgrade to max level");
        Register("突破品质", "Breakthrough quality");

        Register("回响", "Echo");

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
            "提示", "配方操作提示按钮说明1");
        var txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "配方类型");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(RecipeTypeShortNames).WithSize(200, 0).WithConfigEntry(RecipeTypeEntry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE分馏配方通用核心));
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        if (!GameMain.sandboxToolsEnabled) {
            wnd.AddButton(0, 1, y, tab, "兑换回响",
                onClick: () => { SelectedRecipe.GetRecipeEcho(); });
        } else {
            wnd.AddButton(0, 5, y, tab, "重置回响",
                onClick: () => { SelectedRecipe.ChangeEchoTo(0); });
            wnd.AddButton(1, 5, y, tab, "回响-10",
                onClick: () => { SelectedRecipe.ChangeEchoTo(SelectedRecipe.Echo - 10); });
            wnd.AddButton(2, 5, y, tab, "回响-1",
                onClick: () => { SelectedRecipe.ChangeEchoTo(SelectedRecipe.Echo - 1); });
            wnd.AddButton(3, 5, y, tab, "回响+1",
                onClick: () => { SelectedRecipe.ChangeEchoTo(SelectedRecipe.Echo + 1); });
            wnd.AddButton(4, 5, y, tab, "回响+10",
                onClick: () => { SelectedRecipe.ChangeEchoTo(SelectedRecipe.Echo + 10); });
            y += 36f;
            wnd.AddButton(0, 5, y, tab, "重置等级",
                onClick: () => { SelectedRecipe.ChangeLevelTo(0); });
            wnd.AddButton(1, 5, y, tab, "等级-10",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level - 10); });
            wnd.AddButton(2, 5, y, tab, "等级-1",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level - 1); });
            wnd.AddButton(3, 5, y, tab, "等级+1",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level + 1); });
            wnd.AddButton(4, 5, y, tab, "等级+10",
                onClick: () => { SelectedRecipe.ChangeLevelTo(SelectedRecipe.Level + 10); });
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
        for (int i = 0; i < txtRecipeInfo.Length; i++) {
            txtRecipeInfo[i] = wnd.AddText2(x, y, tab, "动态刷新");
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedItem.Proto = SelectedItem;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        txtCoreCount.text = $"x {GetItemTotalCount(IFE分馏配方通用核心)}";

        int line = 0;
        incSlider.gameObject.SetActive(false);
        if (recipe == null) {
            txtRecipeInfo[line].text = "配方不存在！".Translate().WithColor(Red);
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
        } else if (recipe.Locked) {
            txtRecipeInfo[line].text = $"{recipe.TypeNameWC} {"分馏配方未解锁".Translate().WithColor(Red)}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
        } else {
            txtRecipeInfo[line].text = $"{recipe.TypeNameWC}    {recipe.LvExpWC}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            txtRecipeInfo[line].text = $"{"费用".Translate()} 1 {SelectedItem.name}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;
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

            txtRecipeInfo[line].text = $"{"增产点数".Translate()}";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);

            incSlider.SetPosition(120, txtRecipeInfoBaseY + 24f * line);
            incSlider.gameObject.SetActive(true);
            line++;

            string sameRecipeStr = GetSameRecipeStr(recipe, selectedInc.Value);
            string[] strs = sameRecipeStr.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in strs) {
                txtRecipeInfo[line].text = str;
                txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
                line++;
            }

            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + 24f * line);
            line++;

            //todo: 展示配方特殊加成（配方特殊加成/分馏塔对配方加成）
            // txtRecipeInfo[line].text = "特殊突破加成：无";
            // line++;
        }
        for (; line < txtRecipeInfo.Length; line++) {
            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, 0);
        }
    }

    private static string GetSameRecipeStr(BaseRecipe recipe, int fluidInputIncAvg) {
        ItemProto building = LDB.items.Select(recipe.RecipeType.GetSpriteItemId());
        float pointsBonus = (float)ProcessManager.MaxTableMilli(fluidInputIncAvg);
        float buffBonus1 = building.ReinforcementBonusFracSuccess();
        float buffBonus2 = building.ReinforcementBonusMainOutputCount();
        float buffBonus3 = building.ReinforcementBonusAppendOutputRate();
        //成功率
        float successRate = recipe.SuccessRate * (1 + pointsBonus) * (1 + buffBonus1);
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
        int takeId = IFE分馏配方通用核心;
        int takeCount = 1;
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
                    recipe.RewardEcho(true);
                }
            },
            null);
    }

    private static void ChangeEchoTo(this BaseRecipe recipe, int target) {
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
        recipe.ChangeEchoTo(target);
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
