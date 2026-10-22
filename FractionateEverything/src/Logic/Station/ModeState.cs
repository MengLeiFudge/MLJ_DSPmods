using System;
using System.Collections.Concurrent;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站传输模式和容量模式状态管理。
/// </summary>
public static partial class StationManager {
    /// <summary>
    /// 存档兼容的传输组合；界面分别控制上传与下载。
    /// </summary>
    private enum ETransferMode {
        Sync = 0,
        Upload = 1,
        Download = 2,
        Disabled = 3
    }

    /// <summary>
    /// 容量模式枚举：有限上传、无限上传。
    /// </summary>
    private enum ECapacityMode {
        Limited = 0,
        Infinite = 1
    }

    private static ETransferMode NormalizeTransferMode(int value) {
        return Enum.IsDefined(typeof(ETransferMode), value)
            ? (ETransferMode)value
            : ETransferMode.Sync;
    }

    private static ECapacityMode NormalizeCapacityMode(int value) {
        return Enum.IsDefined(typeof(ECapacityMode), value)
            ? (ECapacityMode)value
            : ECapacityMode.Limited;
    }

    /// <summary>每个交互站实体的每个槽位的传输模式设置</summary>
    private static ConcurrentDictionary<long, ConcurrentDictionary<int, ETransferMode>> slotTransferMode = new();

    /// <summary>每个交互站实体的每个槽位的容量模式设置</summary>
    private static ConcurrentDictionary<long, ConcurrentDictionary<int, ECapacityMode>> slotCapacityMode = new();

    /// <summary>读取槽位模式，用于蓝图和复制参数扩展。</summary>
    private static bool TryGetSlotModes(long entityId, int slotIndex, out int transferMode, out int capacityMode) {
        transferMode = (int)ETransferMode.Sync;
        capacityMode = (int)ECapacityMode.Limited;
        bool hasValue = false;
        if (slotTransferMode.TryGetValue(entityId, out ConcurrentDictionary<int, ETransferMode> transferDictionary)
            && transferDictionary.TryGetValue(slotIndex, out ETransferMode transfer)) {
            transferMode = (int)NormalizeTransferMode((int)transfer);
            hasValue = true;
        }
        if (slotCapacityMode.TryGetValue(entityId, out ConcurrentDictionary<int, ECapacityMode> capacityDictionary)
            && capacityDictionary.TryGetValue(slotIndex, out ECapacityMode capacity)) {
            capacityMode = (int)NormalizeCapacityMode((int)capacity);
            hasValue = true;
        }
        return hasValue;
    }

    /// <summary>写入槽位模式，用于从蓝图、Q 键复制和粘贴设置恢复运行态。</summary>
    private static void SetSlotModes(long entityId, int slotIndex, int transferMode, int capacityMode) {
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ETransferMode>());
        ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
            slotCapacityMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ECapacityMode>());
        transferDictionary[slotIndex] = NormalizeTransferMode(transferMode);
        capacityDictionary[slotIndex] = NormalizeCapacityMode(capacityMode);
    }

    /// <summary>清理指定实体的槽位模式，防止 entityId 复用继承旧状态。</summary>
    private static void RemoveSlotModes(long entityId) {
        slotTransferMode.TryRemove(entityId, out _);
        slotCapacityMode.TryRemove(entityId, out _);
    }

    /// <summary>读取上传选项：0关闭、1有限、2无限；旧组合保留原有行为。</summary>
    private static int GetUploadMode(long entityId, int slot) {
        TryGetSlotModes(entityId, slot, out int transfer, out int capacity);
        return (ETransferMode)transfer is ETransferMode.Sync or ETransferMode.Upload ? capacity + 1 : 0;
    }

    /// <summary>读取独立下载开关，未配置的槽位默认开启。</summary>
    private static bool IsDownloadEnabled(long entityId, int slot) {
        TryGetSlotModes(entityId, slot, out int transfer, out _);
        return (ETransferMode)transfer is ETransferMode.Sync or ETransferMode.Download;
    }

    /// <summary>两套站点窗口共用的上传选项文字。</summary>
    private static string UploadModeText(int mode) => (mode switch {
        1 => "有限上传", 2 => "无限上传", _ => "关闭上传",
    }).Translate();

    /// <summary>原版弹窗提供两个选项，按顺序显示当前上传模式以外的两项。</summary>
    private static int GetUploadOption(int current, int option) => option >= current ? option + 1 : option;

    /// <summary>提交一个方向的修改，另一方向在主机按最新状态保留。</summary>
    private static void ApplyModeOption(PlanetFactory factory, StationComponent station, int slot, bool upload, int option) {
        if (option is < 0 or > 1) { return; }
        int value = upload ? GetUploadOption(GetUploadMode(station.entityId, slot), option) : option;
        FE.Compatibility.Nebula.StationModeSync.Request(factory, station, slot, upload, value);
    }

    /// <summary>应用已校验的单方向设置；存档与蓝图继续使用原有组合编码。</summary>
    internal static void ApplySlotDirection(long entityId, int slot, bool upload, int value) {
        TryGetSlotModes(entityId, slot, out int transfer, out int capacity);
        bool send = (ETransferMode)transfer is ETransferMode.Sync or ETransferMode.Upload;
        bool receive = (ETransferMode)transfer is ETransferMode.Sync or ETransferMode.Download;
        if (upload) { send = value != 0; if (send) { capacity = value - 1; } }
        else { receive = value != 0; }
        ETransferMode mode = send ? (receive ? ETransferMode.Sync : ETransferMode.Upload)
            : receive ? ETransferMode.Download : ETransferMode.Disabled;
        SetSlotModes(entityId, slot, (int)mode, capacity);
    }

    /// <summary>
    /// 清理交互站运行态和 UI 态缓存（切档或导入存档时调用）。
    /// </summary>
    public static void Clear() {
        // 清理所有与交互站相关的运行时数据
        // 包括传输模式、容量模式设置
        lastTickDic.Clear();
        stationBufferDic.Clear();
        transferBufferDic.Clear();
        slotTransferMode.Clear();
        slotCapacityMode.Clear();
        // 清理UI弹窗状态缓存
        storagePopup.Clear();
        controlPanelStoragePopup.Clear();
        // 清理弹窗位置缓存
        storagePopupOriginalX.Clear();
        controlPanelStoragePopupOriginalX.Clear();
        // 清理弹窗状态标记
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }
}
