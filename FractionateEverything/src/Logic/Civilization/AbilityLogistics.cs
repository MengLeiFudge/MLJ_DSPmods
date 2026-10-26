using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

public static partial class AbilityTasks {
    private static readonly Dictionary<(int Planet, int Station, int Item), long> UploadedMaterials = [];
    private static readonly Dictionary<int, long> DownloadedMaterials = [];

    /// <summary>只统计实际入库、异站下载和随后投入生产的数量；来回搬运不会重复增加可用生产额度。</summary>
    internal static void RecordStationTransfer(int planet, int station, int item, int count, bool download) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient || GrowthTechnologyManager.UltimateCapacity) { return; }
        lock (Sync) {
            if (!download) {
                var key = (planet, station, item);
                UploadedMaterials.TryGetValue(key, out long previous);
                // 任务只需两万件，每来源额度有限；已经下载的待用材料重新上传先冲回待用额度。
                DownloadedMaterials.TryGetValue(item, out long pending);
                long returned = Math.Min(pending, count);
                DownloadedMaterials[item] = pending - returned;
                UploadedMaterials[key] = Math.Min(20000L, previous + count - returned);
                Record("Upload", count - returned);
            } else {
                int remaining = count;
                foreach (var key in UploadedMaterials.Keys.Where(key => key.Item == item && (key.Planet != planet || key.Station != station)).ToArray()) {
                    int moved = (int)Math.Min(remaining, UploadedMaterials[key]);
                    remaining -= moved;
                    UploadedMaterials[key] -= moved;
                    if (UploadedMaterials[key] == 0) { UploadedMaterials.Remove(key); }
                    if (remaining == 0) { break; }
                }
                DownloadedMaterials.TryGetValue(item, out long previous);
                DownloadedMaterials[item] = Math.Min(20000L, previous + count - remaining);
                Record("Download", count - remaining);
            }
            dirty = true;
        }
    }

    internal static void RecordMaterialUse(int item, int count) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return; }
        lock (Sync) {
            if (!DownloadedMaterials.TryGetValue(item, out long pending)) { return; }
            long used = Math.Min(pending, count);
            DownloadedMaterials[item] = pending - used;
            Record("LogisticsUse", used);
        }
    }

    private static void ExportUploadedMaterials(BinaryWriter writer) => writer.WriteList(UploadedMaterials, (w, pair) => {
        w.Write(pair.Key.Planet); w.Write(pair.Key.Station); w.Write(pair.Key.Item); w.Write(pair.Value);
    });
    private static void ImportUploadedMaterials(BinaryReader reader) => reader.ReadList(r => {
        var key = (Planet: r.ReadInt32(), Station: r.ReadInt32(), Item: r.ReadInt32()); long count = r.ReadInt64();
        if (key.Planet <= 0 || key.Station <= 0 || key.Item is <= 0 or >= 12000 || count is < 0 or > 20000) { throw new InvalidDataException("物流验证记录无效。"); }
        UploadedMaterials.Add(key, count);
    }, 1000000);
    private static void ExportDownloadedMaterials(BinaryWriter writer) => writer.WriteList(DownloadedMaterials, (w, pair) => {
        w.Write(pair.Key); w.Write(pair.Value);
    });
    private static void ImportDownloadedMaterials(BinaryReader reader) => reader.ReadList(r => {
        int item = r.ReadInt32(); long count = r.ReadInt64();
        if (item is <= 0 or >= 12000 || count is < 0 or > 20000) { throw new InvalidDataException("物流生产验证记录无效。"); }
        DownloadedMaterials.Add(item, count);
    }, 12000);
}
