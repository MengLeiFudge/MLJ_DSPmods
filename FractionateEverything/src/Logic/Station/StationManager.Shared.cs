using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FE.Logic.Manager;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

public static partial class StationManager {
    /// <summary>按钮额外间距（像素）</summary>
    private const float ExtraSpacing = 12f;
    /// <summary>模式按钮高度</summary>
    private const float BtnHeight = 26f;
    /// <summary>按钮Y轴偏移量</summary>
    private const float BtnYOffset = 14f;
    /// <summary>按钮宽度+间距，用于UI宽度调整计算</summary>
    private static float spacingX;
    /// <summary>槽位弹窗状态：记录弹窗是否由本Mod接管</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsMyPopup = new();
    /// <summary>槽位弹窗状态：记录当前是传输模式(true)还是容量模式(false)</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsTransfer = new();
    /// <summary>槽位弹窗状态：记录弹窗RectTransform引用</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), RectTransform>
        slotPopupBoxRect =
            new();

    /// <summary>获取或缓存RectTransform的原始Vector2值</summary>
    private static Vector2 GetOrCacheOriginal(Dictionary<RectTransform, Vector2> cache, RectTransform rect,
        Func<RectTransform, Vector2> getter) {
        // 检查缓存中是否已有该RectTransform的原始值
        if (cache.TryGetValue(rect, out Vector2 value)) {
            return value;
        }

        // 首次访问时调用getter获取原始值并存入缓存
        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    /// <summary>获取或缓存RectTransform的原始float值</summary>
    private static float GetOrCacheOriginal(Dictionary<RectTransform, float> cache, RectTransform rect,
        Func<RectTransform, float> getter) {
        // 检查缓存中是否已有该RectTransform的原始float值
        if (cache.TryGetValue(rect, out float value)) {
            return value;
        }

        // 首次访问时调用getter获取原始值并存入缓存
        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    /// <summary>
    /// 判断建筑ID是否为物流交互站（行星内或星际）
    /// </summary>
    /// <param name="buildingID">建筑物品ID</param>
    /// <returns>是物流交互站返回true，否则返回false</returns>
    private static bool IsInteractionStation(int buildingID) {
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站;
    }

    /// <summary>
    /// 获取经过上限限制后的集装数量
    /// </summary>
    /// <param name="value">滑块数值</param>
    /// <returns>限制后的整数值</returns>
    private static int GetClampedPilerCount(float value) {
        // 获取当前交互站的最大堆叠数（强化等级上限）
        int maxStack = GetInteractionStationMaxStack();
        // 将滑块浮点值四舍五入为整数
        int newValue = Mathf.RoundToInt(value);
        // 如果超过上限则限制为上限值，否则保持原值
        return newValue > maxStack ? maxStack : newValue;
    }

    /// <summary>
    /// 获取物流交互站的最大堆叠数（强化等级）
    /// </summary>
    /// <returns>最大堆叠数量</returns>
    private static int GetInteractionStationMaxStack() {
        // 从LDB获取交互站物品原型并返回其最大堆叠数
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        return building.MaxStack();
    }

    /// <summary>
    /// 刷新物流交互站的集装UI显示
    /// </summary>
    /// <param name="station">物流站组件</param>
    /// <param name="minPilerSlider">集装滑块</param>
    /// <param name="minPilerValue">集装数值文本</param>
    /// <param name="minPilerGroup">集装组GameObject</param>
    /// <param name="pilerTechGroup">科技组GameObject</param>
    private static void RefreshInteractionStationPilerUI(StationComponent station, Slider minPilerSlider,
        Text minPilerValue, GameObject minPilerGroup, GameObject pilerTechGroup) {
        // 获取交互站的最大堆叠数并设置为滑块最大值
        int maxStack = GetInteractionStationMaxStack();
        minPilerSlider.maxValue = maxStack;

        // 获取当前集装数量，若为0（自动模式）则显示最大值
        int pilerCount = station.pilerCount;
        int showValue = pilerCount == 0 ? maxStack : pilerCount;
        minPilerSlider.value = showValue;
        minPilerValue.text = showValue.ToString();

        // 当最大堆叠数大于1时（已解锁堆叠功能），显示集装相关UI
        if (maxStack > 1) {
            minPilerGroup.SetActive(true);
            pilerTechGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 从独立面板存储槽位获取弹窗状态键值
    /// </summary>
    /// <param name="storage">存储槽位</param>
    /// <returns>(实体ID, 槽位索引)元组</returns>
    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIStationStorage storage) {
        // 存储槽位为空时返回无效键值
        if (storage == null) {
            return (0L, -1);
        }

        // 获取物流站实体ID（为空则返回0）和槽位索引
        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    /// <summary>
    /// 从总控面板存储槽位获取弹窗状态键值
    /// </summary>
    /// <param name="storage">总控面板存储槽位</param>
    /// <returns>(实体ID, 槽位索引)元组</returns>
    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIControlPanelStationStorage storage) {
        // 存储槽位为空时返回无效键值
        if (storage == null) {
            return (0L, -1);
        }

        // 获取物流站实体ID（为空则返回0）和槽位索引
        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    /// <summary>
    /// 从状态字典读取弹窗标记，不存在时返回false
    /// </summary>
    /// <param name="stateDictionary">状态字典</param>
    /// <param name="key">键值</param>
    /// <returns>标记状态</returns>
    private static bool GetPopupFlag(ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> stateDictionary,
        (long stationEntityId, int slotIndex) key) {
        // 统一读取字典中的布尔状态，不存在时按 false 处理。
        return stateDictionary.TryGetValue(key, out bool value) && value;
    }

    /// <summary>
    /// 清理指定键的所有弹窗状态
    /// </summary>
    /// <param name="key">键值</param>
    private static void ClearPopupState((long stationEntityId, int slotIndex) key) {
        // 一个键对应三份弹窗状态，这里一次性清干净，避免残留脏状态。
        slotIsMyPopup.TryRemove(key, out _);
        slotIsTransfer.TryRemove(key, out _);
        slotPopupBoxRect.TryRemove(key, out _);
    }
}
