using System;
using System.IO;
using UnityEngine;

namespace FE.Logic.Civilization;

public static partial class ExplorationManager {
    /// <summary>探索格式6保存最多两个未完成挑战；完成事实沿既有回收档案保存。</summary>
    private static void ExportChallenges(BinaryWriter writer) {
        writer.Write(Challenges.Count);
        foreach (SignalChallenge c in Challenges.Values) {
            writer.Write(c.MarkId); writer.Write((int)c.Kind); writer.Write(c.Reward);
            writer.Write(c.Started); writer.Write(c.StartTick); writer.Write(c.Owner);
            writer.Write(c.Progress); writer.Write(c.Required); writer.Write(c.RecipeId); writer.Write(c.RequiredMachines); writer.Write(c.QualifiedMachines);
            writer.Write(c.OutsideSince); writer.Write(c.Route.Length);
            foreach (Vector3 point in c.Route) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
            writer.Write(c.Machines.Count);
            foreach (var machine in c.Machines) { writer.Write(machine.Key); writer.Write(machine.Value.Entity); writer.Write(machine.Value.Produced); }
        }
    }

    private static void ImportChallenges(BinaryReader reader, int version) {
        if (version < 6) { return; }
        int count = reader.ReadInt32();
        if (count is < 0 or > 2) { throw new InvalidDataException("探索挑战数量无效。"); }
        for (int i = 0; i < count; i++) {
            var c = new SignalChallenge {
                MarkId = reader.ReadInt32(), Kind = (SignalChallengeKind)reader.ReadInt32(), Reward = reader.ReadInt32(),
                Started = reader.ReadBoolean(), StartTick = reader.ReadInt64(), Owner = reader.ReadInt32(),
                Progress = reader.ReadInt32(), Required = reader.ReadInt32(), RecipeId = reader.ReadInt32(),
                RequiredMachines = reader.ReadInt32(), QualifiedMachines = reader.ReadInt32(), OutsideSince = reader.ReadInt64(),
            };
            if (FindMark(c.MarkId) == null || !Enum.IsDefined(typeof(SignalChallengeKind), c.Kind)
                || c.Reward is < 1 or > 240 || c.Progress < 0 || c.Progress > c.Required || c.Required is < 1 or > 360
                || c.RequiredMachines is < 2 or > 3 || c.QualifiedMachines is < 0 or > 1000000 || c.StartTick < 0 || c.OutsideSince < 0 || c.Owner is < -1 or > 65535
                || Challenges.ContainsKey(c.MarkId)) { throw new InvalidDataException("探索挑战状态无效。"); }
            int points = reader.ReadInt32();
            if (points is < 0 or > 7 || c.Kind == SignalChallengeKind.Route && (points != c.Required + 1 || c.Progress >= c.Required)) {
                throw new InvalidDataException("探索航迹状态无效。");
            }
            c.Route = new Vector3[points];
            for (int p = 0; p < points; p++) {
                Vector3 point = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) || point.sqrMagnitude < 1) { throw new InvalidDataException("探索航迹坐标无效。"); }
                c.Route[p] = point;
            }
            int machines = reader.ReadInt32();
            if (machines is < 0 or > 1000000) { throw new InvalidDataException("挑战设备数量无效。"); }
            for (int m = 0; m < machines; m++) {
                int id = reader.ReadInt32(), entity = reader.ReadInt32(), produced = reader.ReadInt32();
                if (id <= 0 || entity <= 0 || produced < 0 || produced > c.Required || c.Machines.ContainsKey(id)) { throw new InvalidDataException("挑战设备记录无效。"); }
                c.Machines.Add(id, (entity, produced));
            }
            Challenges.Add(c.MarkId, c);
        }
    }

    private static void ClearChallenges() { Challenges.Clear(); ExplorationChallengeFacts.Clear(); }
}
