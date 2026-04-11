using FE.Logic.Manager;
using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public static class RecipeGrowthExecutor {
    public static RecipeGrowthResult EnsureUnlockedByTech(BaseRecipe recipe, RecipeGrowthContext context) {
        RecipeGrowthState state = RecipeGrowthManager.Store.GetOrCreate(recipe);
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int previousLevel = state.Level;
        int targetLevel = rule.TechBaselineLevel;
        if (targetLevel > state.Level) {
            state.Level = RecipeGrowthRules.ClampLevel(rule, targetLevel);
            state.UnlockSourceFlags |= RecipeUnlockSourceFlags.TechBaseline;
            state.LastTouchedTick = context.GameTick;
        }
        return BuildResult(rule, previousLevel, state);
    }

    public static RecipeGrowthResult ApplyDrawReward(BaseRecipe recipe, RecipeGrowthContext context) {
        RecipeGrowthState state = RecipeGrowthManager.Store.GetOrCreate(recipe);
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int previousLevel = state.Level;
        int fragmentReward = 0;

        if (state.Level >= rule.MaxLevel) {
            fragmentReward = context.CurrentFocus == GachaFocusType.RectificationEconomy
                ? context.IsSpeedrunMode ? 35 : 20
                : context.IsSpeedrunMode ? 25 : 15;
            return new RecipeGrowthResult(previousLevel, state.Level, previousLevel > 0, previousLevel > 0,
                true, false, fragmentReward);
        }

        if (state.Level <= 0) {
            state.Level = rule.FixedMaxReward ? rule.MaxLevel : RecipeGrowthRules.ClampLevel(rule, rule.DrawUnlockLevel);
        } else {
            state.Level = rule.FixedMaxReward ? rule.MaxLevel : RecipeGrowthRules.ClampLevel(rule, state.Level + 1);
        }

        state.UnlockSourceFlags |= RecipeUnlockSourceFlags.Draw;
        state.LastTouchedTick = context.GameTick;
        return BuildResult(rule, previousLevel, state, fragmentReward);
    }

    public static RecipeGrowthResult ApplyProcessingProgress(BaseRecipe recipe, int inputCount, int successCount,
        RecipeGrowthContext context) {
        RecipeGrowthState state = RecipeGrowthManager.Store.GetOrCreate(recipe);
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int previousLevel = state.Level;
        if (state.Level <= 0 || state.Level >= rule.MaxLevel || !rule.UsesGrowthExp && !rule.UsesPity) {
            return BuildResult(rule, previousLevel, state);
        }

        int gain = inputCount;
        switch (rule.Family) {
            case RecipeFamily.BuildingTrainForward:
                gain += successCount * 6;
                state.GrowthExp += gain;
                break;
            case RecipeFamily.BuildingTrainReverse:
                gain += successCount * 4;
                state.GrowthExp += gain;
                break;
            case RecipeFamily.MineralCopyDarkFog:
            case RecipeFamily.ConversionMaterialDarkFog:
                gain += successCount * 2;
                state.GrowthExp += gain;
                break;
            case RecipeFamily.Rectification:
                state.PityProgress += gain;
                break;
        }

        while (state.Level < rule.MaxLevel) {
            int threshold = RecipeGrowthRules.GetUpgradeThreshold(rule, state.Level);
            if (rule.UsesPity) {
                if (state.PityProgress < threshold) {
                    break;
                }
                state.PityProgress -= threshold;
            } else {
                if (state.GrowthExp < threshold) {
                    break;
                }
                state.GrowthExp -= threshold;
            }
            state.Level++;
        }

        if (state.Level != previousLevel || gain > 0) {
            state.UnlockSourceFlags |= RecipeUnlockSourceFlags.Processing;
            state.LastTouchedTick = context.GameTick;
        }

        return BuildResult(rule, previousLevel, state);
    }

    public static RecipeGrowthResult SetLevelForSandbox(BaseRecipe recipe, int targetLevel, RecipeGrowthContext context) {
        RecipeGrowthState state = RecipeGrowthManager.Store.GetOrCreate(recipe);
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int previousLevel = state.Level;
        state.Level = RecipeGrowthRules.ClampLevel(rule, targetLevel);
        if (state.Level > 0) {
            state.UnlockSourceFlags |= RecipeUnlockSourceFlags.Sandbox;
        }
        state.LastTouchedTick = context.GameTick;
        return BuildResult(rule, previousLevel, state);
    }

    private static RecipeGrowthResult BuildResult(RecipeGrowthRule rule, int previousLevel, RecipeGrowthState state,
        int fragmentReward = 0) {
        bool wasUnlocked = previousLevel > 0;
        bool isUnlocked = state.Level > 0;
        bool isMaxed = state.Level >= rule.MaxLevel;
        return new RecipeGrowthResult(previousLevel, state.Level, wasUnlocked, isUnlocked, isMaxed,
            previousLevel != state.Level, fragmentReward);
    }
}
