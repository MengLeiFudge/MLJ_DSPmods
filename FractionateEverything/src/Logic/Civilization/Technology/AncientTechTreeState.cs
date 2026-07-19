using System;
using System.Collections.Generic;
using System.IO;
using FE.Utils;

namespace FE.Logic.Civilization.Technology;

/// <summary>
/// 保存单存档远古文明科技点和节点等级。
/// </summary>
public static class AncientTechTreeState {
    private static readonly Dictionary<string, int> nodeLevels = [];

    public static int AvailablePoints { get; private set; }
    public static int TotalPointsEarned { get; private set; }
    public static int TotalPointsSpent { get; private set; }

    public static int GetLevel(string nodeKey) =>
        nodeKey != null && nodeLevels.TryGetValue(nodeKey, out int level) ? level : 0;

    public static void AwardPoint() {
        AwardPoints(1);
    }

    public static void AwardPoints(int count) {
        if (count <= 0) {
            return;
        }
        AvailablePoints += count;
        TotalPointsEarned += count;
    }

    public static bool TrySpend(int cost) {
        if (cost <= 0 || AvailablePoints < cost) {
            return false;
        }
        AvailablePoints -= cost;
        TotalPointsSpent += cost;
        return true;
    }

    public static void SetLevel(string nodeKey, int level) {
        nodeLevels[nodeKey] = Math.Max(0, level);
    }

    public static void Import(BinaryReader r) {
        nodeLevels.Clear();
        AvailablePoints = 0;
        TotalPointsEarned = 0;
        TotalPointsSpent = 0;
        r.ReadBlocks(
            ("Points", br => {
                AvailablePoints = Math.Max(0, br.ReadInt32());
                TotalPointsEarned = Math.Max(0, br.ReadInt32());
                TotalPointsSpent = Math.Max(0, br.ReadInt32());
            }),
            ("Nodes", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    nodeLevels[br.ReadString()] = Math.Max(0, br.ReadInt32());
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Points", bw => {
                bw.Write(AvailablePoints);
                bw.Write(TotalPointsEarned);
                bw.Write(TotalPointsSpent);
            }),
            ("Nodes", bw => {
                bw.Write(nodeLevels.Count);
                foreach (KeyValuePair<string, int> pair in nodeLevels) {
                    bw.Write(pair.Key);
                    bw.Write(pair.Value);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        nodeLevels.Clear();
        AvailablePoints = 0;
        TotalPointsEarned = 0;
        TotalPointsSpent = 0;
    }
}
