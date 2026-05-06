using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.Logic.Building;
using FE.UI.View.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class StationManager {
    #region IModCanSave

    /// <summary>从存档读取交互站的传输与容量模式配置</summary>
    /// <param name="r">BinaryReader 实例</param>
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("SlotTransferMode", br => {
                // 读取保存的实体数量，逐个恢复传输模式
                int entityCount = br.ReadInt32();
                for (int i = 0; i < entityCount; i++) {
                    long entityId = br.ReadInt64();
                    int slotCount = br.ReadInt32();
                    var dict = new ConcurrentDictionary<int, ETransferMode>();
                    for (int j = 0; j < slotCount; j++) {
                        int slotIndex = br.ReadInt32();
                        dict[slotIndex] = NormalizeTransferMode(br.ReadInt32());
                    }
                    slotTransferMode[entityId] = dict;
                }
            }),
            ("SlotCapacityMode", br => {
                // 同步读取每个实体的容量模式配置
                int entityCount = br.ReadInt32();
                for (int i = 0; i < entityCount; i++) {
                    long entityId = br.ReadInt64();
                    int slotCount = br.ReadInt32();
                    var dict = new ConcurrentDictionary<int, ECapacityMode>();
                    for (int j = 0; j < slotCount; j++) {
                        int slotIndex = br.ReadInt32();
                        dict[slotIndex] = NormalizeCapacityMode(br.ReadInt32());
                    }
                    slotCapacityMode[entityId] = dict;
                }
            })
        );
    }

    /// <summary>将交互站的传输与容量模式配置写入存档</summary>
    /// <param name="w">BinaryWriter 实例</param>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("SlotTransferMode", bw => {
                // 记录实体数量以及每个槽的传输模式
                bw.Write(slotTransferMode.Count);
                foreach (var kvp in slotTransferMode) {
                    bw.Write(kvp.Key);
                    bw.Write(kvp.Value.Count);
                    foreach (var slot in kvp.Value) {
                        bw.Write(slot.Key);
                        bw.Write((int)slot.Value);
                    }
                }
            }),
            ("SlotCapacityMode", bw => {
                // 记录容量模式字典信息
                bw.Write(slotCapacityMode.Count);
                foreach (var kvp in slotCapacityMode) {
                    bw.Write(kvp.Key);
                    bw.Write(kvp.Value.Count);
                    foreach (var slot in kvp.Value) {
                        bw.Write(slot.Key);
                        bw.Write((int)slot.Value);
                    }
                }
            })
        );
    }

    /// <summary>在切换存档时清理交互站的缓存状态</summary>
    public static void IntoOtherSave() {
        // 切档时清理所有 UI/状态缓存，避免遗留配置
        Clear();
    }

    #endregion
}
