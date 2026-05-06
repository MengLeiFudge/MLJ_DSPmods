using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View.ProgressTask;
using FE.UI.View.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static partial class FracRecipeOperate {
    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        btnSelectedItem.Proto = SelectedItem;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        RecipeDisplaySnapshot snapshot = recipe == null ? default : RecipeGrowthQueries.GetSnapshot(recipe);
        ItemProto building = LDB.items.Select(recipeType.GetSpriteItemId());
        int line = 0;
        foreach (MySlider slider in incSliders) {
            slider?.gameObject.SetActive(false);
        }
        RefreshSandboxButtons(recipe, snapshot);

        if (recipe == null) {
            ShowTextLine(line++, "配方不存在！".Translate().WithColor(Red));
        } else if (!snapshot.IsUnlocked) {
            string headerLocked = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            int recipeColor = recipe.MatrixID - I电磁矩阵;
            ShowTextLine(line++, $"{headerLocked.WithColor(recipeColor)} {"分馏配方未解锁".Translate().WithColor(Red)}");
        } else {
            // ---- 左列内容 ----

            // 第1行：配方类型-原料名称（剥离强化等级）
            string headerName = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            ShowTextLine(line++, headerName.WithColor(recipe.MatrixID - I电磁矩阵));
            ShowTextLine(line++, "");// 空行

            if (recipe is RectificationRecipe) {
                ShowTextLine(line++,
                    $"{"成功率".Translate()} {1.0f:P3}（稳定压缩）".WithColor(Orange));
                ShowTextLine(line++,
                    "精馏塔不参与成功率判定，献祭/成就成功率加成不会改变残片结算。".WithColor(Gray));
                ShowTextLine(line++,
                    $"{"损毁率".Translate()} {0.0f:P3}（稳定压缩）".WithColor(Green));
            } else {
                float sacrificeBoost = building?.SuccessBoost() ?? 0f;
                float progressBoost = Achievements.GetSuccessRateBonus();
                float actualSuccessRatio = Mathf.Clamp01(recipe.SuccessRatio
                                                         * (1f + sacrificeBoost)
                                                         * (1f + progressBoost));
                ShowTextLine(line++,
                    $"{"成功率".Translate()} {recipe.SuccessRatio:P3} × {(1f + sacrificeBoost):F3} × {(1f + progressBoost):F3} = {actualSuccessRatio:P3}"
                        .WithColor(Orange));
                ShowTextLine(line++,
                    $"(献祭 +{sacrificeBoost:P2} / 成就 +{progressBoost:P2})"
                        .WithColor(Gray));

                // 损毁率
                float baseDestroyRatio = snapshot.DestroyRatio
                                         + GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
                float destroyReduction = GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
                string destroyText = $"{"损毁率".Translate()} {baseDestroyRatio:P3}";
                if (destroyReduction > 0f) {
                    destroyText += $"（成就 -{destroyReduction:P3}，实际 {recipe.DestroyRatio:P3}）";
                }
                ShowTextLine(line++, destroyText.WithColor(Red));
            }
            ShowTextLine(line++, "");// 空行

            // 主产物：标签独占一行，下方竖向列表
            if (recipe.OutputMain.Count > 0) {
                ShowTextLine(line, "产出".Translate().WithColor(Orange));
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputMain) {
                if (recipe is RectificationRecipe rectificationRecipe) {
                    ShowRectificationProductLine(line++, rectificationRecipe, info);
                } else {
                    if (recipe is ConversionRecipe conversionRecipe) {
                        ShowConversionProductLine(line++, conversionRecipe, LDB.items.Select(info.OutputID), info);
                    } else {
                        ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
                    }
                }
            }

            // 副产物：标签独占一行，下方竖向列表
            if (recipe.OutputAppend.Count > 0) {
                ShowTextLine(line, "其他".Translate().WithColor(Orange));
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputAppend) {
                if (recipe is ConversionRecipe conversionRecipe) {
                    ShowConversionProductLine(line++, conversionRecipe, LDB.items.Select(info.OutputID), info);
                } else {
                    ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
                }
            }

            ShowTextLine(line++, "");// 空行

            // 等效处理：增产点数滑条 + 竖向输出列表
            line = ShowEqProcessingSection(line, recipe, building);

            ShowTextLine(line++, "");// 空行

            // 建筑强化效果
            if (building != null) {
                ShowIconLine(line++, building,
                    $"{"建筑强化加成".Translate()} {building.name}  {"等级".Translate()} +{building.Level()}");

                ShowTextLine(line++,
                    $"{"堆叠".Translate()} x{building.MaxStack()}  "
                    + $"{"能耗比".Translate()} {building.EnergyRatio():P0}  "
                    + $"{"增产效率".Translate()} x{building.PlrRatio():F1}");

                float sBoost = building.SuccessBoost();
                ShowTextLine(line++,
                    $"{"成功率加成".Translate()} +{sBoost:P1}"
                        .WithColor(sBoost > 0 ? Orange : Gray));

                bool fluidEnh = building.EnableFluidEnhancement();
                ShowTextLine(line++,
                    $"{"流体增强".Translate()}："
                    + (fluidEnh
                        ? "已启用".Translate().WithColor(Green)
                        : "未启用".Translate().WithColor(Gray)));

                line = ShowBuildingFeatures(line, building);
            }
        }

        // 隐藏剩余左列行
        for (; line < InfoLineCount; line++) {
            HideAllLine(line);
        }

        // 更新右列：配方强化等级表
        UpdateLevelColumn(recipe, snapshot);
    }

    /// <summary>
    /// 沙盒页顶部四个按钮统一按当前配方的真实等级边界刷新状态。
    /// </summary>
    private static void RefreshSandboxButtons(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        if (!GameMain.sandboxToolsEnabled) {
            foreach (UIButton button in recipeSandboxBtn) {
                button.gameObject.SetActive(false);
            }
            return;
        }

        foreach (UIButton button in recipeSandboxBtn) {
            button.gameObject.SetActive(true);
        }

        bool hasRecipe = recipe != null;
        int recipeLevel = hasRecipe ? snapshot.Level : 0;
        bool unlocked = hasRecipe && snapshot.IsUnlocked;
        int maxLevel = hasRecipe ? snapshot.MaxLevel : 0;

        recipeSandboxBtn[0].button.interactable = unlocked && recipeLevel > 0;
        recipeSandboxBtn[1].button.interactable = hasRecipe && recipeLevel > 0;
        recipeSandboxBtn[2].button.interactable = hasRecipe && recipeLevel < maxLevel;
        recipeSandboxBtn[3].button.interactable = hasRecipe && recipeLevel < maxLevel;
    }

    // ==================== 右列：强化等级表 ====================
}
