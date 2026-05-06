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
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

public static partial class FracRecipeOperate {
    private static void UpdateLevelColumn(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        int currentLevel = recipe == null ? 0 : snapshot.Level;

        // 标题行
        string headerText;
        if (recipe == null) {
            headerText = "";
            foreach (Text text in txtLevelInfo) {
                text.text = "";
            }
            return;
        } else if (!snapshot.IsUnlocked) {
            headerText = "配方未解锁".Translate();
        } else {
            headerText = $"{"当前配方等级".Translate()} Lv{currentLevel}";
        }
        txtLevelInfo[0].text = headerText.WithColor(snapshot.IsUnlocked ? Orange : Red);

        int maxLevel = snapshot.MaxLevel;
        for (int lvl = 0; lvl <= maxLevel; lvl++) {
            int lineIdx = lvl + 1;
            string lvlText = snapshot.LevelDescriptions[lvl];

            string coloredText;
            if (!snapshot.IsUnlocked) {
                coloredText = lvlText.WithColor(Gray);// 未解锁：全灰
            } else if (lvl == currentLevel) {
                coloredText = lvlText.WithColor(Orange);// 当前等级：橙色高亮
            } else if (lvl < currentLevel) {
                coloredText = lvlText.WithColor(Green);// 已达到：绿色
            } else {
                coloredText = lvlText;// 未达到：默认白色
            }

            txtLevelInfo[lineIdx].text = coloredText;
        }

        for (int lineIdx = maxLevel + 2; lineIdx < LevelLineCount; lineIdx++) {
            txtLevelInfo[lineIdx].text = "";
        }

        int infoLineIdx = maxLevel + 2;
        if (!snapshot.IsUnlocked) {
            SetRightInfoLine(infoLineIdx++, $"{"解锁方式".Translate()}：{BuildUnlockHint(recipe)}".WithColor(Blue));
        }

        if (!snapshot.IsMaxed) {
            string upgradeHint = BuildUpgradeHint(recipe, snapshot);
            if (!string.IsNullOrEmpty(upgradeHint)) {
                SetRightInfoLine(infoLineIdx++, $"{"升级方式".Translate()}：{upgradeHint}"
                    .WithColor(snapshot.IsUnlocked ? White : Gray));
            }

            string progressHint = BuildUpgradeProgressHint(recipe, snapshot);
            if (!string.IsNullOrEmpty(progressHint)) {
                SetRightInfoLine(infoLineIdx++, $"{"成长进度".Translate()}：{progressHint}".WithColor(Gray));
            }
        } else {
            SetRightInfoLine(infoLineIdx++, $"{"升级方式".Translate()}：{"已完全升级，无需继续成长".Translate()}".WithColor(Green));
        }

        for (; infoLineIdx < LevelLineCount; infoLineIdx++) {
            txtLevelInfo[infoLineIdx].text = "";
        }
    }

    /// <summary>
    /// 右侧等级栏下方的辅助说明统一走这里，避免后续继续分散手写定位。
    /// </summary>
    private static void SetRightInfoLine(int lineIdx, string text) {
        if (lineIdx < 0 || lineIdx >= LevelLineCount) {
            return;
        }

        txtLevelInfo[lineIdx].text = text;
    }

    /// <summary>
    /// 按当前配方家族生成解锁提示，优先输出玩家在当前版本里真正能执行的入口。
    /// </summary>
    private static string BuildUnlockHint(BaseRecipe recipe) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        return rule.Family switch {
            RecipeFamily.MineralCopyNormal when rule.TechBaselineLevel > 0
                => "通过开线抽取获取；部分前期配方也会随科技保底解锁".Translate(),
            RecipeFamily.MineralCopyNormal or RecipeFamily.ConversionMaterialNormal
                => "通过开线抽取获取".Translate(),
            RecipeFamily.BuildingTrainForward or RecipeFamily.BuildingTrainReverse
                => "通过原胚闭环或成长规划获得；相关科技也会保底解锁".Translate(),
            RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog
                => "首次获得对应黑雾物品后解锁".Translate(),
            _ when recipe.RecipeType == ERecipe.Conversion && recipe.MatrixID == I黑雾矩阵
                => "通过黑雾支线成长规划报价获得".Translate(),
            RecipeFamily.ConversionBuilding or RecipeFamily.PointAggregate
                => "通过成长规划或固定入口获得；解锁后直接满级".Translate(),
            RecipeFamily.Rectification
                => "通过科技保底解锁".Translate(),
            _ => "通过开线抽取获取".Translate(),
        };
    }

    /// <summary>
    /// 按成长模式给出升级方式说明；若当前还未解锁，会自动补上“解锁后”前缀。
    /// </summary>
    private static string BuildUpgradeHint(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        string prefix = snapshot.IsUnlocked ? string.Empty : $"{"解锁后".Translate()}";
        return rule.Family switch {
            RecipeFamily.MineralCopyNormal or RecipeFamily.ConversionMaterialNormal
                => prefix + "重复抽到该配方即可升级".Translate(),
            RecipeFamily.BuildingTrainForward
                => prefix + "处理对应原胚获取经验，重复获得时也会直接提升".Translate(),
            RecipeFamily.BuildingTrainReverse
                => prefix + "处理对应原胚获取经验".Translate(),
            RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog
                => prefix + "处理对应黑雾物品获取经验，也可通过成长规划补差".Translate(),
            RecipeFamily.Rectification
                => prefix + "处理对应矩阵获取保底进度".Translate(),
            RecipeFamily.ConversionBuilding or RecipeFamily.PointAggregate
                => snapshot.IsUnlocked
                    ? "已完全升级，无需继续成长".Translate()
                    : prefix + "直接满级".Translate(),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// 只有当前规则存在明确阈值时，才显示经验/保底进度，避免给出虚假的进度条。
    /// </summary>
    private static string BuildUpgradeProgressHint(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        if (!snapshot.IsUnlocked || snapshot.IsMaxed) {
            return string.Empty;
        }

        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int threshold = RecipeGrowthRules.GetUpgradeThreshold(rule, snapshot.Level);
        if (threshold == int.MaxValue) {
            return string.Empty;
        }

        if (rule.UsesPity) {
            return $"{snapshot.PityProgress}/{threshold}";
        }

        if (rule.UsesGrowthExp) {
            return $"{snapshot.GrowthExp}/{threshold}";
        }

        return string.Empty;
    }

    private static float GetBaseDestroyRatio(BaseRecipe recipe, int? level = null) => 0.04f;

    // ==================== 产物显示（格式：概率 | 图标 | 数量） ====================

    /// <summary>
    /// 显示单个产物行：左侧概率文本，中间物品图标，右侧数量。
    /// </summary>
}
