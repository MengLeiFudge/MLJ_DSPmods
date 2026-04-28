using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public sealed class RecipeGrowthStateStore {
    private readonly Dictionary<RecipeKey, RecipeGrowthState> states = [];

    public RecipeGrowthState GetOrCreate(BaseRecipe recipe) {
        RecipeKey key = RecipeKey.FromRecipe(recipe);
        if (!states.TryGetValue(key, out RecipeGrowthState state)) {
            RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
            state = new RecipeGrowthState {
                Level = rule.DefaultLevel,
            };
            states[key] = state;
        }
        return state;
    }

    public void Import(BinaryReader r) {
        states.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            RecipeKey key = new((ERecipe)r.ReadInt32(), r.ReadInt32());
            int level = r.ReadInt32();
            int growthExp = r.ReadInt32();
            int pityProgress = r.ReadInt32();
            RecipeUnlockSourceFlags flags = (RecipeUnlockSourceFlags)r.ReadInt32();
            long lastTouchedTick = r.ReadInt64();
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            RecipeGrowthRule rule = recipe != null
                ? RecipeGrowthRules.GetRule(recipe)
                : new RecipeGrowthRule(
                    RecipeFamily.Unknown, RecipeGrowthMode.None, 5, 0, 0, 1, false, false, false);
            states[key] = new RecipeGrowthState {
                Level = RecipeGrowthRules.ClampLevel(rule, level),
                GrowthExp = growthExp < 0 ? 0 : growthExp,
                PityProgress = pityProgress < 0 ? 0 : pityProgress,
                UnlockSourceFlags = flags
                                    & (RecipeUnlockSourceFlags.TechBaseline
                                       | RecipeUnlockSourceFlags.Draw
                                       | RecipeUnlockSourceFlags.Processing
                                       | RecipeUnlockSourceFlags.DarkFogDrop
                                       | RecipeUnlockSourceFlags.Sandbox
                                       | RecipeUnlockSourceFlags.LegacyImport),
                LastTouchedTick = lastTouchedTick < 0 ? 0 : lastTouchedTick,
            };
        }
    }

    public void Export(BinaryWriter w) {
        w.Write(states.Count);
        foreach (KeyValuePair<RecipeKey, RecipeGrowthState> pair in states) {
            RecipeKey key = pair.Key;
            RecipeGrowthState state = pair.Value;
            w.Write((int)key.RecipeType);
            w.Write(key.InputId);
            w.Write(state.Level);
            w.Write(state.GrowthExp);
            w.Write(state.PityProgress);
            w.Write((int)state.UnlockSourceFlags);
            w.Write(state.LastTouchedTick);
        }
    }

    public void IntoOtherSave() {
        states.Clear();
    }
}
