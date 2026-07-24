using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.FracRecipes.Runtime;
using FE.Logic.Fractionation.Process;
using static FE.Utils.Utils;

namespace FE.Logic.Achievements;

/// <summary>
/// 成就域的翻译、初始化、低频评估、存档和运行投影入口。
/// </summary>
public static class AchievementModule {
    public static void AddTranslations() {
        Register("成就-首项协议", "First Recovered Protocol");
        Register("成就-完整阶段", "First Completed Stage");
        Register("成就-首次科技投入", "First Ancient Technology");
        Register("成就-千次分馏", "One Thousand Fractionations");
    }

    public static void Initialize() {
        AchievementCatalog.Initialize();
        RefreshRuntimeProjection();
    }

    public static void Tick() {
        if (NebulaMultiplayerModAPI.IsMultiplayerActive && NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        if (!AchievementService.Tick()) {
            return;
        }

        RefreshRuntimeProjection();
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }

    public static void Import(BinaryReader r) => AchievementState.Import(r);

    public static void Export(BinaryWriter w) => AchievementState.Export(w);

    public static void AfterImport() => RefreshRuntimeProjection();

    public static void IntoOtherSave() {
        AchievementState.IntoOtherSave();
        AchievementService.IntoOtherSave();
        RefreshRuntimeProjection();
    }

    private static void RefreshRuntimeProjection() {
        RecipeModifierCache.Reset();
        foreach (AchievementDefinition definition in AchievementService.GetCompletedDefinitions()) {
            switch (definition.RewardType) {
                case AchievementRewardType.RecipeTypeSuccessRate:
                    RecipeModifierCache.AddSuccessRateBonus(definition.RecipeType, definition.RewardValue);
                    break;
                case AchievementRewardType.AllRecipeSuccessRate:
                    RecipeModifierCache.AddAllRecipeSuccessRateBonus(definition.RewardValue);
                    break;
            }
        }
        ProcessManager.RefreshFractionatorRuntimeConfig();
    }
}
