using System.Collections.Generic;
using FE.Logic.Fractionation.State;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Fractionation.Process;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.Components;
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

public static partial class FracRecipeOperate {
    private static void ShowProductLine(int line, ItemProto itemProto, OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        string count = forceShow || info.ShowOutputCount ? info.OutputCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? info.SuccessRatio.ToString("P3") : "???";

        // 左侧：概率文本
        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        // 中间：物品图标
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        // 右侧：数量
        txtRecipeInfo[line].text = $"×{count}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    private static void ShowConversionProductLine(int line, ConversionRecipe recipe, ItemProto itemProto, OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        bool showCount = forceShow || info.ShowOutputCount;
        string randomCount = showCount ? info.OutputCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? info.SuccessRatio.ToString("P3") : "???";
        string lockedCount = recipe.TryGetLockedOutputPlan(info.OutputID,
            out ConversionRecipe.LockedOutputPlan lockedPlan)
            ? (showCount ? lockedPlan.OutputCount.ToString("F3") : "???")
            : "???";

        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        txtRecipeInfo[line].text = $"{"随机".Translate()}×{randomCount}  {"单锁".Translate()}×{lockedCount}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    private static void ShowRectificationProductLine(int line, RectificationRecipe recipe, OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        int fragmentCount = GetRectificationDisplayFragmentCount(recipe.InputID, selectedInc.Value);
        string count = forceShow || info.ShowOutputCount ? fragmentCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? 1.0f.ToString("P3") : "???";

        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = LDB.items.Select(info.OutputID);
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        txtRecipeInfo[line].text = $"×{count}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    // ==================== 等效处理（滑条 + 竖向输出列表） ====================

    private static int ShowEqProcessingSection(int line, BaseRecipe recipe, ItemProto building) {
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = "增产点数".Translate();
        txtRecipeInfo[line].SetPosition(0f, 0f);
        incSliders[line].gameObject.SetActive(true);
        line++;

        ShowTextLine(line++, "每个原料平均产出：".Translate());

        if (recipe is RectificationRecipe rectificationRecipe) {
            // 精馏配方是稳定压缩：不参与成功率/损毁/双倍/返料公式，直接显示当前条件下的真实残片数。
            int fragmentCount = GetRectificationDisplayFragmentCount(rectificationRecipe.InputID, selectedInc.Value);
            btnRecipeInfoIcons[line].gameObject.SetActive(true);
            btnRecipeInfoIcons[line].Proto = LDB.items.Select(IFE残片);
            NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);
            txtRecipeInfo[line].text = $"×{fragmentCount:F3}";
            txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
            txtProductLeft[line].gameObject.SetActive(false);
            return line + 1;
        }

        // E = fracRatio / (1 - fracRatio*r)，其中 fracRatio=(1-d)*s，r=remainInputRatio
        float plrRatio = building?.PlrRatio() ?? 1.0f;
        float pointsBonus = (float)ProcessManager.MaxTableMilli(selectedInc.Value) * plrRatio;
        float successBoost = (building?.SuccessBoost() ?? 0f) + Achievements.GetSuccessRateBonus();
        float successRatio = Mathf.Clamp01(recipe.SuccessRatio * (1 + pointsBonus) * (1 + successBoost));
        float fracRatio = (1 - recipe.DestroyRatio) * successRatio;
        float remainInputRatio = recipe.RemainInputRatio;
        float repeatRatio = fracRatio * remainInputRatio;
        float repeatMultiplier = repeatRatio >= 0.9999f ? 10000.0f : 1.0f / (1.0f - repeatRatio);
        float mainOutputBonus = 1.0f + recipe.DoubleOutputRatio;

        ConversionRecipe conversionRecipe = recipe as ConversionRecipe;
        List<(int id, float cnt, float lockedCnt, bool showCount)> outputs = [];
        Dictionary<int, int> outputIndex = [];

        foreach (var info in recipe.OutputMain) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * mainOutputBonus * repeatMultiplier;
            float lockedCnt = GetLockedEquivalentCount(conversionRecipe, id, fracRatio, mainOutputBonus,
                repeatMultiplier);
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, elc, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, elc >= 0f ? elc : lockedCnt, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, lockedCnt, info.ShowSuccessRatio));
            }
        }
        foreach (var info in recipe.OutputAppend) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * repeatMultiplier;
            float lockedCnt = GetLockedEquivalentCount(conversionRecipe, id, fracRatio, mainOutputBonus,
                repeatMultiplier);
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, elc, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, elc >= 0f ? elc : lockedCnt, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, lockedCnt, info.ShowSuccessRatio));
            }
        }

        bool showDetails = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;

        foreach (var (id, cnt, lockedCnt, showCount) in outputs) {
            ItemProto outItem = LDB.items.Select(id);
            string outCount = showDetails || showCount ? cnt.ToString("F3") : "???";
            string lockedOutCount = showDetails || showCount ? lockedCnt.ToString("F3") : "???";

            txtProductLeft[line].gameObject.SetActive(false);

            btnRecipeInfoIcons[line].gameObject.SetActive(true);
            btnRecipeInfoIcons[line].Proto = outItem;
            NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

            txtRecipeInfo[line].text = conversionRecipe != null && lockedCnt >= 0f
                ? $"{"随机".Translate()}×{outCount}  {"单锁".Translate()}×{lockedOutCount}"
                : $"×{outCount}";
            txtRecipeInfo[line].SetPosition(ProductTextX, 0f);

            line++;
        }

        return line;
    }

    private static float GetLockedEquivalentCount(ConversionRecipe recipe, int outputId, float fracRatio,
        float mainOutputBonus, float repeatMultiplier) {
        if (recipe == null || !recipe.TryGetLockedOutputPlan(outputId,
                out ConversionRecipe.LockedOutputPlan lockedPlan)) {
            return -1f;
        }

        return fracRatio * lockedPlan.OutputCount * mainOutputBonus * repeatMultiplier;
    }

    private static int GetRectificationDisplayFragmentCount(int inputId, int inputInc) {
        int fragmentCount = GetRectificationFragmentYield(inputId, RectificationTower.PlrRatio);
        if (RectificationTower.EnableAfterglowExtraction && inputInc >= 4) {
            fragmentCount += 1;
        }
        if (RectificationTower.EnableHyperphaseCompression
            && (inputId == GetCurrentProgressMatrixId() || inputId == I黑雾矩阵)) {
            fragmentCount += 1;
        }
        return fragmentCount;
    }

    // ==================== 建筑特殊特质 ====================

    private static int ShowBuildingFeatures(int line, ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                ShowTextLine(line++,
                    $"{"牺牲特性".Translate()}：{FeatureStatus(InteractionTower.EnableSacrificeTrait)}  "
                    + $"{"维度共鸣".Translate()}：{FeatureStatus(InteractionTower.EnableDimensionalResonance)}");
                break;
            case IFE矿物复制塔:
                ShowTextLine(line++,
                    $"{"质能裂变".Translate()}：{FeatureStatus(MineralReplicationTower.EnableMassEnergyFission)}  "
                    + $"{"零压循环".Translate()}：{FeatureStatus(MineralReplicationTower.EnableZeroPressureCycle)}");
                break;
            case IFE转化塔:
                ShowTextLine(line++,
                    $"{"因果追踪".Translate()}：{FeatureStatus(ConversionTower.EnableCausalTracing)}  "
                    + $"{"单锁".Translate()}：{FeatureStatus(ConversionTower.EnableSingleLock)}");
                break;
            case IFE点数聚集塔:
                ShowTextLine(line++,
                    $"{"虚空喷射".Translate()}：{FeatureStatus(PointAggregateTower.EnableVoidSpray)}  "
                    + $"{"双倍点数".Translate()}：{FeatureStatus(PointAggregateTower.EnableDoublePoints)}  "
                    + $"{"最大增产等级".Translate()} {PointAggregateTower.MaxInc}");
                break;
        }
        return line;
    }

    // ==================== 辅助显示方法 ====================

    private static void ShowIconLine(int line, ItemProto itemProto, string text) {
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], 0f, 0f);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(TextOffsetWithIcon, 0f);
    }

    private static void ShowTextLine(int line, string text) {
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(0f, 0f);
    }

    private static void HideIconOnLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
    }

    private static void HideAllLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        txtProductLeft[line].text = "";
        txtRecipeInfo[line].text = "";
        txtRecipeInfo[line].SetPosition(0f, 0f);
    }

    private static string FeatureStatus(bool enabled) =>
        enabled ? "已启用".Translate().WithColor(Green) : "未启用".Translate().WithColor(Gray);
}
