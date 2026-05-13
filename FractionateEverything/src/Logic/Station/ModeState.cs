using System;
using System.Collections.Concurrent;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站传输模式和容量模式状态管理。
/// </summary>
public static partial class StationManager {
    /// <summary>
    /// 传输模式枚举：双向同步、仅上传、仅下载。
    /// </summary>
    private enum ETransferMode {
        Sync = 0,
        Upload = 1,
        Download = 2
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

    /// <summary>
    /// 根据当前模式和选项索引获取下一个传输模式（循环切换）
    /// </summary>
    /// <param name="currentMode">当前传输模式</param>
    /// <param name="optionIndex">选项按钮索引（0或1）</param>
    /// <returns>下一个传输模式</returns>
    private static ETransferMode GetNextTransferMode(ETransferMode currentMode, int optionIndex) {
        // 根据选项按钮索引决定模式切换逻辑
        if (optionIndex == 0) {
            // 选项0的切换顺序：Sync -> Upload -> Download -> Sync
            return currentMode switch {
                ETransferMode.Sync => ETransferMode.Upload,
                ETransferMode.Upload => ETransferMode.Download,
                _ => ETransferMode.Sync
            };
        }

        if (optionIndex == 1) {
            // 选项1的切换顺序：Sync -> Download -> Upload -> Sync
            return currentMode switch {
                ETransferMode.Sync => ETransferMode.Download,
                ETransferMode.Upload => ETransferMode.Sync,
                _ => ETransferMode.Upload
            };
        }

        // 其他情况保持当前模式不变
        return currentMode;
    }

    /// <summary>
    /// 清理交互站运行态和 UI 态缓存（切档或导入存档时调用）。
    /// </summary>
    public static void Clear() {
        // 清理所有与交互站相关的运行时数据
        // 包括传输模式、容量模式设置
        lastTickDic.Clear();
        stationBufferDic.Clear();
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
