using System;
using System.IO;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

public static partial class ExplorationManager {
    /// <summary>每个挑战及其航迹、设备集合独立保存，字段增删不改变其他挑战的边界。</summary>
    private static void ExportChallenges(BinaryWriter writer) {
        writer.WriteList(Challenges.Values, (item, c) => item.WriteBlocks(
            ("MarkId", w => w.Write(c.MarkId)), ("Kind", w => w.Write((int)c.Kind)),
            ("Reward", w => w.Write(c.Reward)), ("Started", w => w.Write(c.Started)),
            ("StartTick", w => w.Write(c.StartTick)), ("Owner", w => w.Write(c.Owner)),
            ("Progress", w => w.Write(c.Progress)), ("Required", w => w.Write(c.Required)),
            ("RecipeId", w => w.Write(c.RecipeId)), ("RequiredMachines", w => w.Write(c.RequiredMachines)),
            ("QualifiedMachines", w => w.Write(c.QualifiedMachines)), ("OutsideSince", w => w.Write(c.OutsideSince)),
            ("Route", w => w.WriteList(c.Route, (pointWriter, point) => {
                pointWriter.Write(point.x); pointWriter.Write(point.y); pointWriter.Write(point.z);
            })),
            ("Machines", w => w.WriteList(c.Machines, (machine, pair) => machine.WriteBlocks(
                ("Id", field => field.Write(pair.Key)), ("Entity", field => field.Write(pair.Value.Entity)),
                ("Produced", field => field.Write(pair.Value.Produced)))))
        ));
    }

    /// <summary>恢复挑战字段，地点引用由探索聚合入口在全部块读完后核对。</summary>
    private static void ImportChallenges(BinaryReader reader) {
        reader.ReadList(item => {
            SignalChallenge c = new();
            item.ReadBlocks(
                ("MarkId", r => c.MarkId = r.ReadInt32()), ("Kind", r => c.Kind = (SignalChallengeKind)r.ReadInt32()),
                ("Reward", r => c.Reward = r.ReadInt32()), ("Started", r => c.Started = r.ReadBoolean()),
                ("StartTick", r => c.StartTick = r.ReadInt64()), ("Owner", r => c.Owner = r.ReadInt32()),
                ("Progress", r => c.Progress = r.ReadInt32()), ("Required", r => c.Required = r.ReadInt32()),
                ("RecipeId", r => c.RecipeId = r.ReadInt32()), ("RequiredMachines", r => c.RequiredMachines = r.ReadInt32()),
                ("QualifiedMachines", r => c.QualifiedMachines = r.ReadInt32()), ("OutsideSince", r => c.OutsideSince = r.ReadInt64()),
                ("Route", r => c.Route = r.ReadList(pointReader => {
                    Vector3 point = new(pointReader.ReadSingle(), pointReader.ReadSingle(), pointReader.ReadSingle());
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) || point.sqrMagnitude < 1) {
                        throw new InvalidDataException("探索航迹坐标无效。");
                    }
                    return point;
                }, 7).ToArray()),
                ("Machines", r => r.ReadList(machine => {
                    int id = 0, entity = 0, produced = 0;
                    machine.ReadBlocks(("Id", field => id = field.ReadInt32()),
                        ("Entity", field => entity = field.ReadInt32()), ("Produced", field => produced = field.ReadInt32()));
                    if (id <= 0 || entity <= 0 || produced < 0 || c.Machines.ContainsKey(id)) {
                        throw new InvalidDataException("挑战设备记录无效。");
                    }
                    c.Machines.Add(id, (entity, produced));
                }))
            );
            if (c.MarkId <= 0 || !Enum.IsDefined(typeof(SignalChallengeKind), c.Kind)
                || c.Reward is < 1 or > 240 || c.Progress < 0 || c.Progress > c.Required || c.Required is < 1 or > 360
                || c.RequiredMachines is < 2 or > 3 || c.QualifiedMachines is < 0 or > 1000000
                || c.StartTick < 0 || c.OutsideSince < 0 || c.Owner is < -1 or > 65535 || Challenges.ContainsKey(c.MarkId)) {
                throw new InvalidDataException("探索挑战状态无效。");
            }
            if (c.Kind == SignalChallengeKind.Route && (c.Route.Length != c.Required + 1 || c.Progress >= c.Required)) {
                throw new InvalidDataException("探索航迹状态无效。");
            }
            foreach (var machine in c.Machines.Values) {
                if (machine.Produced > c.Required) { throw new InvalidDataException("挑战设备记录无效。"); }
            }
            Challenges.Add(c.MarkId, c);
        }, 2);
    }

    /// <summary>读取已有探索格式6中的顺序布局挑战。</summary>
    private static void ImportLegacyChallenges(BinaryReader reader) {
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
