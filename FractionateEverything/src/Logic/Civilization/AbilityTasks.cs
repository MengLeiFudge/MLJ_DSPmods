using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Civilization.Monument;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>一次性能力的任务事实与条件；完成状态只归原版科技，不另存奖励开关。</summary>
public static partial class AbilityTasks {
    private static readonly object Sync = new();
    private static readonly Dictionary<string, long> Counters = [];
    private static bool dirty;
    public static readonly int[] TechnologyIds = [TFE遗迹测绘, TFE定向解析, TFE休眠保护, TFE终极仓储, TFE丰碑通关];

    public static int GetStage(int id) => id switch {
        TFE遗迹测绘 => 3, TFE定向解析 => 4, TFE休眠保护 => 5, _ => 6,
    };

    /// <summary>调用方登记已经发生的真实结果；本锁不调用其他领域，工厂线程可安全累积。</summary>
    internal static void Record(string key, long count) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return; }
        lock (Sync) {
            Counters.TryGetValue(key, out long previous);
            Counters[key] = previous + Math.Min(count, long.MaxValue - previous);
            dirty = true;
        }
    }

    internal static long Count(string key) {
        lock (Sync) { return Counters.TryGetValue(key, out long value) ? value : 0; }
    }

    public static (string Label, double Current, double Required)[] GetRequirements(int id) => id switch {
        TFE遗迹测绘 => [
            ("FE.Task.Recover".Translate(), Count("Recovery"), 2),
            ("FE.Task.Analysis".Translate(), Count("Analysis"), 128),
            ("FE.Task.Cultivation".Translate(), Count("Cultivation"), 64),
        ],
        TFE定向解析 => [
            ("FE.Task.Analysis".Translate(), Count("Analysis"), 512),
            ("FE.Task.Conversion".Translate(), Count("Conversion"), 512),
            ("FE.Task.Breeding".Translate(), Count("Breeding"), 64),
        ],
        TFE休眠保护 => [
            ("FE.Task.Cultivation".Translate(), Count("Cultivation"), 256),
            ("FE.Task.Digestion".Translate(), Count("Digestion"), 256),
            ("FE.Task.Breeding".Translate(), Count("Breeding"), 256),
        ],
        TFE终极仓储 => [
            ("FE.Task.Capacity".Translate(), SubspaceExpansion.CapacityScale, SubspaceExpansion.UltimateRequiredScale),
            ("FE.Task.Upload".Translate(), Count("Upload"), 20000),
            ("FE.Task.Download".Translate(), Count("Download"), 20000),
            ("FE.Task.Production".Translate(), Count("LogisticsUse"), 20000),
        ],
        TFE丰碑通关 => [("FE.Task.Processing".Translate(), MonumentManager.ProcessingLevel, 14)],
        _ => [],
    };

    public static string GetDescription(int id) {
        string progress = string.Join("\n", GetRequirements(id).Select(requirement =>
            $"{requirement.Label}：{Math.Min(requirement.Current, requirement.Required):0.##}/{requirement.Required:0.##}"));
        string effect = GrowthTechnologyManager.TryGetTechnologyUnlockText(LDB.techs.Select(id), out string text) ? text : "";
        return "FE.Ability.Description".Translate() + "\n\n" + progress + "\n\n" + effect;
    }

    internal static void Tick() {
        if (NebulaMultiplayerModAPI.IsClient || GameMain.history == null) { return; }
        foreach (int id in TechnologyIds) {
            if (GameMain.history.TechUnlocked(id)
                || id != TFE丰碑通关 && ResearchManager.CurrentMatrixStage < GetStage(id)) { continue; }
            if (!GetRequirements(id).All(requirement => requirement.Current >= requirement.Required)) { continue; }
            GameMain.history.UnlockTechUnlimited(id, true);
            lock (Sync) { dirty = true; }
            CivilizationModule.QueueNotification(string.Format("FE.Task.Completed".Translate(), LDB.techs.Select(id).name));
        }
        if (GrowthTechnologyManager.MonumentCompleted) { PermanentProfileStore.RecordCompletion(); }
    }

    internal static bool TakeDirty() { lock (Sync) { bool value = dirty; dirty = false; return value; } }
    internal static void Clear() { lock (Sync) { Counters.Clear(); UploadedMaterials.Clear(); DownloadedMaterials.Clear(); dirty = false; } }

    internal static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.WriteBlocks(
                ("UploadedMaterials", ExportUploadedMaterials),
                ("DownloadedMaterials", ExportDownloadedMaterials),
                ("Counters", w => w.WriteList(Counters.OrderBy(pair => pair.Key).ToArray(), (entry, pair) => {
                    entry.Write(pair.Key); entry.Write(pair.Value);
                })),
                ("CompletedTechnologies", w => w.WriteList(TechnologyIds.Where(id => GameMain.history?.TechUnlocked(id) == true).ToArray(),
                    (entry, id) => entry.Write(id)))
            );
        }
    }

    internal static void Import(BinaryReader reader) {
        Clear();
        reader.ReadBlocks(
            ("UploadedMaterials", ImportUploadedMaterials),
            ("DownloadedMaterials", ImportDownloadedMaterials),
            ("Counters", r => r.ReadList(entry => {
                string key = entry.ReadString(); long count = entry.ReadInt64();
                if (key.Length > 64 || count < 0) { throw new InvalidDataException("能力任务进度无效。"); }
                lock (Sync) { Counters.Add(key, count); }
            }, 64)),
            ("CompletedTechnologies", r => r.ReadList(entry => {
                int id = entry.ReadInt32();
                if (NebulaMultiplayerModAPI.IsClient && TechnologyIds.Contains(id)
                    && GameMain.history?.TechUnlocked(id) == false) { GameMain.history.UnlockTechUnlimited(id, true); }
            }, TechnologyIds.Length))
        );
    }
}
