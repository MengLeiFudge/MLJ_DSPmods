using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.FracRecipes;
using FE.Utils;

namespace FE.Logic.Civilization.Protocols;

/// <summary>
/// 保存协议发现、完整度、优先目标和检索保底状态。
/// </summary>
public static class ProtocolProgressStore {
    public sealed class ProtocolProgress {
        public bool Discovered;
        public int Completeness;
    }

    public sealed class StageRetrievalProgress {
        public bool HasPreferredRecipe;
        public RecipeKey PreferredRecipe;
        public int FailureStreak;
        public int DiscoveryStreak;
    }

    private static readonly Dictionary<RecipeKey, ProtocolProgress> protocols = [];
    private static readonly Dictionary<string, StageRetrievalProgress> retrievalProgress = [];

    public static ProtocolProgress GetOrCreate(RecipeKey recipeKey) {
        if (!protocols.TryGetValue(recipeKey, out ProtocolProgress progress)) {
            progress = new ProtocolProgress();
            protocols[recipeKey] = progress;
        }
        return progress;
    }

    public static StageRetrievalProgress GetStageProgress(string stageKey) {
        if (!retrievalProgress.TryGetValue(stageKey, out StageRetrievalProgress progress)) {
            progress = new StageRetrievalProgress();
            retrievalProgress[stageKey] = progress;
        }
        return progress;
    }

    public static bool IsComplete(RecipeKey recipeKey) => GetOrCreate(recipeKey).Completeness >= 100;

    public static void MarkComplete(RecipeKey recipeKey) {
        ProtocolProgress progress = GetOrCreate(recipeKey);
        progress.Discovered = true;
        progress.Completeness = 100;
    }

    public static void Import(BinaryReader r) {
        protocols.Clear();
        retrievalProgress.Clear();
        r.ReadBlocks(
            ("Protocols", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    RecipeKey recipeKey = new((ERecipe)br.ReadInt32(), br.ReadInt32());
                    protocols[recipeKey] = new ProtocolProgress {
                        Discovered = br.ReadBoolean(),
                        Completeness = Math.Max(0, Math.Min(100, br.ReadInt32())),
                    };
                }
            }),
            ("Retrieval", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    string stageKey = br.ReadString();
                    bool hasPreferred = br.ReadBoolean();
                    RecipeKey preferred = new((ERecipe)br.ReadInt32(), br.ReadInt32());
                    retrievalProgress[stageKey] = new StageRetrievalProgress {
                        HasPreferredRecipe = hasPreferred,
                        PreferredRecipe = preferred,
                        FailureStreak = Math.Max(0, br.ReadInt32()),
                        DiscoveryStreak = Math.Max(0, br.ReadInt32()),
                    };
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Protocols", bw => {
                bw.Write(protocols.Count);
                foreach (KeyValuePair<RecipeKey, ProtocolProgress> pair in protocols) {
                    bw.Write((int)pair.Key.RecipeType);
                    bw.Write(pair.Key.InputId);
                    bw.Write(pair.Value.Discovered);
                    bw.Write(pair.Value.Completeness);
                }
            }),
            ("Retrieval", bw => {
                bw.Write(retrievalProgress.Count);
                foreach (KeyValuePair<string, StageRetrievalProgress> pair in retrievalProgress) {
                    bw.Write(pair.Key);
                    bw.Write(pair.Value.HasPreferredRecipe);
                    bw.Write((int)pair.Value.PreferredRecipe.RecipeType);
                    bw.Write(pair.Value.PreferredRecipe.InputId);
                    bw.Write(pair.Value.FailureStreak);
                    bw.Write(pair.Value.DiscoveryStreak);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        protocols.Clear();
        retrievalProgress.Clear();
    }
}
