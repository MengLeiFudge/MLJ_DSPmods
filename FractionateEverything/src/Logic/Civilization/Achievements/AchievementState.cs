using System;
using System.Collections.Generic;
using System.IO;
using FE.Utils;

namespace FE.Logic.Civilization.Achievements;

/// <summary>
/// 保存当前存档已经完成并自动生效的文明成就键。
/// </summary>
public static class AchievementState {
    private static readonly HashSet<string> completed = [];

    public static bool IsCompleted(string achievementKey) => completed.Contains(achievementKey);

    public static bool Complete(string achievementKey) => completed.Add(achievementKey);

    public static void Import(BinaryReader r) {
        completed.Clear();
        r.ReadBlocks(("Completed", br => {
            int count = Math.Max(0, br.ReadInt32());
            for (int i = 0; i < count; i++) {
                completed.Add(br.ReadString());
            }
        }));
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(("Completed", bw => {
            bw.Write(completed.Count);
            foreach (string achievementKey in completed) {
                bw.Write(achievementKey);
            }
        }));
    }

    public static void IntoOtherSave() => completed.Clear();
}
