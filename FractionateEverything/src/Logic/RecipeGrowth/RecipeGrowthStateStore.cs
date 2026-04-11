using System.Collections.Generic;
using System.IO;
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
            states[key] = new RecipeGrowthState {
                Level = r.ReadInt32(),
                GrowthExp = r.ReadInt32(),
                PityProgress = r.ReadInt32(),
                UnlockSourceFlags = (RecipeUnlockSourceFlags)r.ReadInt32(),
                LastTouchedTick = r.ReadInt64(),
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
