using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.Logic.Building;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class StationManager {
    /// <summary>传输模式枚举：双向同步、仅上传、仅下载</summary>
    private enum ETransferMode {
        Sync = 0,
        Upload = 1,
        Download = 2
    }

    /// <summary>容量模式枚举：有限上传、无限上传</summary>
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

    /// <summary>独立物流站面板：窗口原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> windowOriginalWidth = [];
    /// <summary>独立物流站面板：滑块原始尺寸缓存</summary>
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalSize = [];
    /// <summary>独立物流站面板：滑块原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalPosition = [];

    /// <summary>总控面板：主窗口原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalMasterWidth = [];
    /// <summary>总控面板：顶部栏位原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalTopGroupWidth = [];
    /// <summary>总控面板：背景栏位原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalBgWidth = [];
    /// <summary>总控面板：状态筛选组原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, float> filterOriginalStateGroupPosition = [];
    /// <summary>总控面板：右侧组原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalRightGroupWidth = [];

    /// <summary>独立物流站面板：记录栏位是否已加宽</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storageWidth = new();
    /// <summary>独立物流站面板：记录弹窗当前是否处于偏移状态</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storagePopup = new();
    /// <summary>独立物流站面板：记录弹窗原始X坐标，避免多次点击累加偏移</summary>
    private static readonly Dictionary<RectTransform, float> storagePopupOriginalX = [];

    /// <summary>独立物流站面板：传输模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> transferGameObjects = new();
    /// <summary>独立物流站面板：容量模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> capacityGameObjects = new();

    /// <summary>总控面板：原版物流按钮原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, float> controlPanelSdButtonOriginalPosition = [];
    /// <summary>总控面板：记录弹窗当前是否处于偏移状态</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, bool> controlPanelStoragePopup = new();
    /// <summary>总控面板：记录弹窗原始X坐标，保证左移/回归可逆</summary>
    private static readonly Dictionary<RectTransform, float> controlPanelStoragePopupOriginalX = [];
    /// <summary>总控面板：传输模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelTransferGameObjects = new();
    /// <summary>总控面板：容量模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelCapacityGameObjects = new();

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
    /// 调整独立物流站面板中滑块控件的位置和尺寸
    /// </summary>
    /// <param name="window">物流站窗口实例</param>
    /// <param name="shouldWiden">是否需要加宽</param>
    private static void AdjustSliders(UIStationWindow window, bool shouldWiden) {
        // 如果窗口为空，直接返回
        if (window == null) {
            return;
        }

        // 计算宽度变化量：如果需要加宽则使用spacingX，否则为0
        float delta = shouldWiden ? spacingX : 0f;

        // 定义需要调整的所有滑块控件及其对应的数值文本组件
        (Slider slider, Component valueText)[] controls = [
            (window.maxChargePowerSlider, window.maxChargePowerValue),
            (window.maxTripDroneSlider, window.maxTripDroneValue),
            (window.maxTripVesselSlider, window.maxTripVesselValue),
            (window.warperDistanceSlider, window.warperDistanceValue),
            (window.minDeliverDroneSlider, window.minDeliverDroneValue),
            (window.minDeliverVesselSlider, window.minDeliverVesselValue),
            (window.maxMiningSpeedSlider, window.maxMiningSpeedValue),
            (window.minPilerSlider, window.minPilerValue)
        ];
        // 遍历所有滑块控件，调整其位置和尺寸
        foreach ((Slider slider, Component valueText) in controls) {
            // 跳过空的滑块控件
            if (slider == null) {
                continue;
            }

            // 获取滑块的RectTransform组件
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            if (sliderRect == null) {
                continue;
            }

            // 获取滑块的原始尺寸和位置（首次访问时缓存）
            Vector2 originalSize = GetOrCacheOriginal(sliderOriginalSize, sliderRect, x => x.sizeDelta);
            Vector2 originalPosition = GetOrCacheOriginal(sliderOriginalPosition, sliderRect, x => x.anchoredPosition);

            // 调整滑块的宽度和水平位置
            sliderRect.sizeDelta = new Vector2(originalSize.x + delta, originalSize.y);
            sliderRect.anchoredPosition = new Vector2(originalPosition.x + delta, originalPosition.y);

            // 如果存在数值文本，也调整其位置
            RectTransform valueRect = valueText?.GetComponent<RectTransform>();
            if (valueRect != null) {
                // 获取数值文本的原始位置（首次访问时缓存）
                Vector2 originalValuePosition =
                    GetOrCacheOriginal(sliderOriginalPosition, valueRect, x => x.anchoredPosition);
                // 调整数值文本的水平位置
                valueRect.anchoredPosition =
                    new Vector2(originalValuePosition.x + delta, originalValuePosition.y);
            }
        }
    }

    /// <summary>
    /// 在总控面板中查找模式按钮的Transform
    /// </summary>
    /// <param name="storage">总控面板存储槽位</param>
    /// <param name="buttonName">按钮名称</param>
    /// <returns>找到的Transform，未找到则返回null</returns>
    private static Transform FindControlPanelModeButtonTransform(UIControlPanelStationStorage storage,
        string buttonName) {
        // 参数校验：存储槽位为空时返回null
        if (storage == null) {
            return null;
        }

        // 优先在存储槽位自身子对象中查找
        Transform byStorage = storage.transform.Find(buttonName);
        if (byStorage != null) {
            return byStorage;
        }

        // 未找到时，在原版物流按钮的父对象中继续查找
        Transform parent = storage.localSdButton?.transform?.parent;
        return parent?.Find(buttonName);
    }

    /// <summary>
    /// 确保总控面板间距已计算，如未计算则通过存储槽位按钮尺寸计算
    /// </summary>
    /// <param name="inspector">总控面板检查器实例</param>
    /// <returns>计算得到的间距值</returns>
    private static float EnsureControlPanelSpacing(UIControlPanelStationInspector inspector) {
        // 如果间距已计算过，直接返回缓存值
        if (spacingX > 0f) {
            return spacingX;
        }

        // 通过存储槽位预置体的原版按钮尺寸计算间距
        RectTransform rectTransform = inspector?.storageUIPrefab?.localSdButton?.GetComponent<RectTransform>();
        if (rectTransform != null) {
            spacingX = rectTransform.sizeDelta.x + ExtraSpacing;
        }

        return spacingX;
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
    /// 判断窗口中的物流站是否为Mod添加的交互站
    /// </summary>
    /// <param name="window">物流站窗口</param>
    /// <param name="station">物流站组件</param>
    /// <returns>是Mod交互站返回true，否则返回false</returns>
    private static bool IsModStation(UIStationWindow window, StationComponent station) {
        // 校验窗口、工厂和实体ID的有效性
        if (window?.factory == null || station == null || station.entityId <= 0) {
            return false;
        }

        // 从实体池获取建筑ID并判断是否为交互站
        int buildingID = window.factory.entityPool[station.entityId].protoId;
        return IsInteractionStation(buildingID);
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
    /// 设置独立物流站面板的加宽状态
    /// </summary>
    /// <param name="window">物流站窗口</param>
    /// <param name="shouldWiden">是否加宽</param>
    private static void SetWindowWidenState(UIStationWindow window, bool shouldWiden) {
        // 校验窗口和窗口Transform的有效性
        if (window?.windowTrans == null) {
            return;
        }

        // 调用AdjustSliders调整所有滑块的位置和尺寸
        AdjustSliders(window, shouldWiden);
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

    /// <summary>
    /// 将独立物流站面板弹窗切换到“右移”或“回归原位”。
    /// </summary>
    private static void SetStoragePopupShift(UIStationStorage storage, bool shiftRight) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        // 第一次遇到该弹窗时，缓存“原始 X 坐标”。
        float originalX = GetOrCacheOriginal(storagePopupOriginalX, popupRect, x => x.anchoredPosition.x);
        // 独立面板规则：mod 按钮 -> 右移；原版按钮 -> 回归。
        float targetX = shiftRight ? originalX + spacingX : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);

        // 记录当前是否处于“偏移态”，用于后续恢复。
        if (shiftRight) {
            storagePopup[storage] = true;
        } else {
            storagePopup.TryRemove(storage, out _);
        }
    }

    /// <summary>
    /// 将总控面板弹窗切换到“左移”或“回归原位”。
    /// </summary>
    private static void SetControlPanelStoragePopupShift(UIControlPanelStationStorage storage, bool shiftLeft) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        // 第一次遇到该弹窗时，缓存“原始 X 坐标”。
        float originalX = GetOrCacheOriginal(controlPanelStoragePopupOriginalX, popupRect, x => x.anchoredPosition.x);
        // 总控面板规则：原版按钮 -> 左移；mod 按钮 -> 回归。
        float targetX = shiftLeft ? originalX - spacingX : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);

        // 记录当前是否处于“偏移态”，用于后续恢复。
        if (shiftLeft) {
            controlPanelStoragePopup[storage] = true;
        } else {
            controlPanelStoragePopup.TryRemove(storage, out _);
        }
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
