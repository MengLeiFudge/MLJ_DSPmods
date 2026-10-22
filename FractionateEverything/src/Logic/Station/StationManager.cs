using System.Collections.Concurrent;
using System.IO;
using FE.Logic.Station.Definitions;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站管理器。
/// 负责交互站原型注册、固定运行属性、与数据中心的物品同步、两套站点面板的扩展按钮，以及弹窗状态与存档读写。
/// </summary>
public static partial class StationManager {
    /// <summary>
    /// 注册两类物流交互站原型。
    /// </summary>
    public static void AddStations() {
        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    /// <summary>
    /// 设置物流交互站材质与颜色。
    /// </summary>
    public static void SetStationMaterial() {
        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    /// <summary>
    /// 刷新物流交互站血量与能耗。
    /// </summary>
    public static void UpdateHpAndEnergy() {
        PlanetaryInteractionStation.UpdateHpAndEnergy();
        InterstellarInteractionStation.UpdateHpAndEnergy();
    }

    /// <summary>
    /// 读取物流交互站当前允许的处理堆叠上限；该值来自全局堆叠科技，不属于建筑等级。
    /// </summary>
    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxStack,
            _ => 1
        };
    }

    /// <summary>
    /// 读取物流交互站的固定交互能耗倍率。
    /// </summary>
    public static float InteractEnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.InteractEnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.InteractEnergyRatio,
            _ => 1.0f
        };
    }

    public static void Import(BinaryReader r) {
        slotTransferMode.Clear();
        slotCapacityMode.Clear();
        r.ReadBlocks(
            ("SlotTransferMode", br => {
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

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("SlotTransferMode", bw => {
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

    public static void IntoOtherSave() {
        Clear();
    }
}
