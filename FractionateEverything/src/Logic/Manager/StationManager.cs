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

/// <summary>
/// 物流交互站管理器。
/// 负责交互站与数据中心的物品同步、两套站点面板的扩展按钮，以及弹窗状态与存档读写。
/// </summary>
public static class StationManager {
    /// <summary>
    /// 注册交互站扩展按钮的多语言文本。
    /// </summary>
    public static void AddTranslations() {
        // 传输模式按钮文本
        Register("双向同步", "Sync", "双向同步");
        Register("仅上传", "Upload Only", "仅上传");
        Register("仅下载", "Download Only", "仅下载");
        // 容量模式按钮文本
        Register("有限上传", "Limited Upload", "有限上传");
        Register("无限上传", "Infinite Upload", "无限上传");
    }

    /// <summary>交互站逻辑更新周期（每30帧更新一次）</summary>
    private static int updateTick = 30;
    /// <summary>每种物品在数据中心"有限上传"模式下的目标存储数量</summary>
    private static readonly int[] itemModSaveCount = new int[12000];

    /// <summary>
    /// 预计算每种物品在数据中心的“有限上传”目标数量。
    /// </summary>
    public static void CalculateItemModSaveCount() {
        // 遍历所有物品，计算每种物品在数据中心的目标存储数量
        foreach (var item in LDB.items.dataArray) {
            if (item.BuildMode != 0) {
                // 建筑类物品：限制为10组（防止建筑占用过多空间）
                itemModSaveCount[item.ID] = item.StackSize * 10;
            } else {
                // 非建筑物品：基于物品价值计算，最多100组
                // 公式：min(100000/物品价值 + 1, 堆叠数 * 100)
                itemModSaveCount[item.ID] = (int)Math.Min(100000 / itemValue[item.ID] + 1, item.StackSize * 100);
            }
        }
    }

    /// <summary>防止同一 stationPool 在同一 tick 被重复处理</summary>
    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];

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
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject> controlPanelTransferGameObjects = new();
    /// <summary>总控面板：容量模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject> controlPanelCapacityGameObjects = new();

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
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), RectTransform> slotPopupBoxRect =
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
    private static Transform FindControlPanelModeButtonTransform(UIControlPanelStationStorage storage, string buttonName) {
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

    /// <summary>在行星运输周期内按自定义规则处理交互站的上传下载</summary>
    /// <param name="__instance">PlanetTransport 实例</param>
    /// <param name="time">当前游戏 tick 时间</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick))]
    public static void PlanetTransportGameTickPostPatch(PlanetTransport __instance, long time) {
        // 30帧更新一次，取3作为特殊值
        if (time % updateTick != 3L) {
            return;
        }

        try {
            // 避免同一帧内多次处理
            if (lastTickDic.TryGetValue(__instance.stationPool, out long lastTick) && lastTick == time) {
                return;
            }

            lastTickDic[__instance.stationPool] = time;
            // 获取所有的交互站后，执行随机排序，让每个塔都有机会拿到物品
            List<StationComponent> stations = [];
            for (int index = 1; index < __instance.stationCursor; ++index) {
                StationComponent stationComponent = __instance.stationPool[index];
                if (stationComponent == null || stationComponent.id != index) {
                    continue;
                }

                int buildingID = __instance.factory.entityPool[stationComponent.entityId].protoId;
                if (buildingID == IFE行星内物流交互站 || buildingID == IFE星际物流交互站) {
                    stations.Add(stationComponent);
                }
            }

            // 使用 Fisher-Yates 洗牌算法进行真正的随机排序
            for (int i = stations.Count - 1; i > 0; i--) {
                int j = GetRandInt(0, i + 1);
                (stations[i], stations[j]) = (stations[j], stations[i]);
            }

            // 循环所有的交互站
            float downloadThreshold = Miscellaneous.DownloadThreshold;
            float uploadThreshold = Miscellaneous.UploadThreshold;
            foreach (StationComponent stationComponent in stations) {
                // 单个槽位可用最大电量
                long maxSlotEnergy = stationComponent.energy / stationComponent.storage.Length;
                long entityId = stationComponent.entityId;

                // 获取该交互站的槽位模式字典
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(entityId, new ConcurrentDictionary<int, ETransferMode>());
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(entityId, new ConcurrentDictionary<int, ECapacityMode>());

                // 循环交互站的所有的栏位
                for (int i = 0; i < stationComponent.storage.Length; i++) {
                    // StationStore为struct，必须使用引用修改内容
                    ref StationStore store = ref stationComponent.storage[i];
                    if (store.itemId <= 0 || itemValue[store.itemId] >= maxValue) {
                        continue;
                    }

                    // 获取该槽位的传输模式和容量模式
                    ETransferMode transferMode = transferDictionary.GetOrAdd(i, ETransferMode.Sync);
                    ECapacityMode capacityMode = capacityDictionary.GetOrAdd(i, ECapacityMode.Limited);

                    // 根据传输模式决定行为
                    switch (transferMode) {
                        case ETransferMode.Upload: {
                            // 仅上传模式：交互站 -> 数据中心
                            // 当交互站数量超过上传阈值时，将超出部分上传到数据中心
                            if (store.count > store.max * uploadThreshold) {
                                int modTargetCount = itemModSaveCount[store.itemId];
                                long modCurrCount = GetModDataItemCount(store.itemId);

                                // 计算超出阈值部分的物品数量
                                int transferCount = store.count - (int)(store.max * uploadThreshold);
                                // 有限上传模式下，受数据中心目标数量限制
                                if (capacityMode == ECapacityMode.Limited && modCurrCount < modTargetCount) {
                                    transferCount = Math.Min(transferCount, modTargetCount - (int)modCurrCount);
                                } else if (capacityMode == ECapacityMode.Limited && modCurrCount >= modTargetCount) {
                                    // 有限上传且数据中心已达目标数量，停止上传
                                    break;
                                }

                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            }
                            break;
                        }
                        case ETransferMode.Download: {
                            // 仅下载模式：数据中心 -> 交互站
                            // 当总供应量低于下载阈值时，从数据中心下载至阈值数量
                            if (store.totalSupplyCount < store.max * downloadThreshold) {
                                // 使用四舍五入避免浮点精度问题（如10000*0.2=1999.999...->2000）
                                int targetCount = Mathf.RoundToInt(store.max * downloadThreshold);
                                // 目标数量不能低于当前数量
                                if (targetCount < store.count) {
                                    targetCount = store.count;
                                }
                                stationComponent.SetTargetCount(i, targetCount, maxSlotEnergy);
                            }
                            break;
                        }
                        case ETransferMode.Sync:
                        default: {
                            // 双向同步模式
                            // 优先处理上传，再处理下载
                            if (store.count > store.max * uploadThreshold) {
                                // 数量高于上传阈值，上传超出部分，尊重容量模式
                                int transferCount = store.count - (int)(store.max * uploadThreshold);
                                if (capacityMode == ECapacityMode.Limited) {
                                    // 有限上传：受数据中心目标数量限制
                                    int modTargetCount = itemModSaveCount[store.itemId];
                                    long modCurrCount = GetModDataItemCount(store.itemId);
                                    if (modCurrCount < modTargetCount) {
                                        transferCount = Math.Min(transferCount, modTargetCount - (int)modCurrCount);
                                    } else {
                                        // 数据中心已达目标数量，不执行上传
                                        transferCount = 0;
                                    }
                                }
                                // 无限上传时不限制数量
                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            } else if (store.totalSupplyCount < store.max * downloadThreshold) {
                                // 数量低于下载阈值，下载数量至阈值
                                // 使用四舍五入避免浮点精度问题
                                int targetCount = Mathf.RoundToInt(store.max * downloadThreshold);
                                if (targetCount < store.count) {
                                    targetCount = store.count;
                                }
                                stationComponent.SetTargetCount(i, targetCount, maxSlotEnergy);
                            }
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex) {
            LogError(ex);
        }
    }

    /// <summary>设置交互站某槽位的目标物品数量并消耗对应电力</summary>
    /// <param name="stationComponent">操作的交互站组件</param>
    /// <param name="index">槽位索引</param>
    /// <param name="targetCount">目标数量</param>
    /// <param name="maxSlotEnergy">单槽位可用的最大电量</param>
    private static void SetTargetCount(this StationComponent stationComponent, int index, int targetCount,
        long maxSlotEnergy) {
        ref StationStore store = ref stationComponent.storage[index];
        try {
            if (store.count == targetCount || itemValue[store.itemId] == float.MaxValue) {
                return;
            }

            ItemProto itemProto = LDB.items.Select(IFE行星内物流交互站);
            // 物品价值(100价值=1000000J=1MJ，即每1价值，耗电10000J)
            float cost = (float)Math.Sqrt(itemValue[store.itemId]) * 10000 * itemProto.InteractEnergyRatio();
            if (store.count < targetCount) {
                // 将数据中心的物品下载到交互站
                int count = targetCount - store.count;
                // 总耗电大于剩余电量，修改数量
                if (cost * count > maxSlotEnergy) {
                    // 1个都玩不起直接放弃
                    if (cost > maxSlotEnergy) {
                        return;
                    }

                    count = Mathf.FloorToInt(maxSlotEnergy / cost);
                }

                count = TakeItemFromModData(store.itemId, count, out int inc);
                store.count += count;
                store.inc += inc;
                stationComponent.energy -= Mathf.CeilToInt(cost * count);
            } else {
                // 将交互站的物品上传到数据中心
                int count = store.count - targetCount;
                // 总耗电大于剩余电量，修改数量
                if (cost * count > maxSlotEnergy) {
                    // 1个都玩不起直接放弃
                    if (cost > maxSlotEnergy) {
                        return;
                    }

                    count = Mathf.FloorToInt(maxSlotEnergy / cost);
                }

                int inc = store.count <= 0 ? 0 : split_inc(ref store.count, ref store.inc, count);
                AddItemToModData(store.itemId, count, inc);
                stationComponent.energy -= Mathf.CeilToInt(cost * count);
            }
        }
        finally {
            if (PlanetaryInteractionStation.Level >= 3) {
                AddIncToItem(store.count, ref store.inc);
            }
        }
    }


    /**
     * 给所有的塔新增两个按钮
     */
    /// <summary>在单个物流站窗口创建时为每个栏位注入自定义模式按钮</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
    public static void UIStationWindow_OnCreate_Postfix(UIStationWindow __instance) {
        if (__instance?.windowTrans != null) {
            // 缓存窗口的原始宽度，用于之后根据是否为交互站动态扩展
            GetOrCacheOriginal(windowOriginalWidth, __instance.windowTrans, x => x.sizeDelta.x);
        }

        for (int index = 0; index < 6; ++index) {
            UIStationStorage storage = __instance.storageUIs[index];
            // 每个栏位都要注入自定义的传输/容量按钮
            try {
                // 原版本地物流模式的按钮
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                // 原版按钮的宽度
                float btnWidth = refRect.sizeDelta.x;
                // 按钮的宽度+间隔=外面盒子需要增加的宽度
                spacingX = btnWidth + ExtraSpacing;
                // 创建交互模式按钮
                // 希望每个栏位都只有一个同名按钮，不存在时才复制
                string tName = "FE_transferModeButton_" + index;
                Transform tTrans = storage.transform.Find(tName);
                if (tTrans == null) {
                    GameObject go =
                        GameObject.Instantiate(storage.localSdButton.gameObject,
                            storage.localSdButton.transform.parent, false);
                    go.name = tName;
                    transferGameObjects.TryAdd(storage, go);
                }

                // 创建容量按钮
                // 同理为容量模式复制一个按钮，并缓存备用
                string cName = "FE_capacityModeButton_" + index;
                Transform cTrans = storage.transform.Find(cName);
                if (cTrans == null) {
                    GameObject go =
                        GameObject.Instantiate(storage.localSdButton.gameObject,
                            storage.localSdButton.transform.parent, false);
                    go.name = cName;
                    capacityGameObjects.TryAdd(storage, go);
                }

                // 保证弹窗层级高于新增按钮，避免遮挡
                storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
            }
            catch (Exception ex) {
                LogError($"FE StationManager: create mode buttons failed: {ex}");
            }
        }
    }

    /// <summary>窗口销毁前清理缓存的窗口尺寸和按钮状态</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnDestroy))]
    public static void UIStationWindow__OnDestroy_Prefix(UIStationWindow __instance) {
        windowOriginalWidth.Clear();
        sliderOriginalSize.Clear();
        sliderOriginalPosition.Clear();
        storageWidth.Clear();
        storagePopup.Clear();
        storagePopupOriginalX.Clear();
        transferGameObjects.Clear();
        capacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    /// <summary>在总控窗口创建时为所有存储槽注入模式按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnCreate))]
    public static void UIControlPanelWindow_OnCreate_Postfix(UIControlPanelWindow __instance) {
        // 检查总控面板检查器和存储槽数组是否为空
        if (__instance?.stationInspector?.storageUIs == null) {
            return;
        }

        // 遍历总控面板的所有存储槽
        for (int index = 0; index < __instance?.stationInspector.storageUIs.Length; ++index) {
            UIControlPanelStationStorage storage = __instance?.stationInspector.storageUIs[index];
            // 跳过没有本地物流按钮的存储槽
            if (storage?.localSdButton == null) {
                continue;
            }

            try {
                // 获取原版物流按钮的尺寸，用于计算间距
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                if (refRect != null) {
                    // 计算间距：按钮宽度 + 额外间距
                    spacingX = refRect.sizeDelta.x + ExtraSpacing;
                }

                // 每个存储槽需要注入传输模式按钮，若已存在就直接缓存
                string transferName = "FE_cp_transferModeButton_" + index;
                Transform transferTrans = FindControlPanelModeButtonTransform(storage, transferName);
                if (transferTrans == null) {
                    // 创建新的传输模式按钮，使用原版按钮作为模板
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = transferName;
                    Button transferBtn = go.GetComponent<Button>();
                    // 清除原版的点击事件监听
                    transferBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelTransferGameObjects[storage] = go;
                } else {
                    // 按钮已存在，直接缓存引用
                    controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
                }

                // 再注入容量模式按钮，逻辑同上
                string capacityName = "FE_cp_capacityModeButton_" + index;
                Transform capacityTrans = FindControlPanelModeButtonTransform(storage, capacityName);
                if (capacityTrans == null) {
                    // 创建新的容量模式按钮，使用原版按钮作为模板
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = capacityName;
                    Button capacityBtn = go.GetComponent<Button>();
                    // 清除原版的点击事件监听
                    capacityBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelCapacityGameObjects[storage] = go;
                } else {
                    // 按钮已存在，直接缓存引用
                    controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
                }

                // 将弹窗移到最上层，确保显示在按钮之上
                if (storage.popupBoxRect != null) {
                    storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
                }
            }
            catch (Exception ex) {
                // 记录按钮创建失败的错误信息
                LogError($"FE StationManager: create control panel mode buttons failed: {ex}");
            }
        }
    }

    /// <summary>总控窗口销毁前恢复所有 UI 缓存</summary>
    /// <param name="__instance">当前的 UIControlPanelWindow 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnDestroy))]
    public static void UIControlPanelWindow_OnDestroy_Prefix(UIControlPanelWindow __instance) {
        controlPanelSdButtonOriginalPosition.Clear();
        inspectorOriginalTopGroupWidth.Clear();
        inspectorOriginalBgWidth.Clear();
        controlPanelStoragePopup.Clear();
        controlPanelStoragePopupOriginalX.Clear();
        controlPanelTransferGameObjects.Clear();
        controlPanelCapacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    /// <summary>打开总控面板存储槽时初始化自定义按钮并绑定点击事件</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnOpen))]
    public static void UIControlPanelStationStorage_OnOpen_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
            if (transferTrans != null) {
                // 替换转移按钮的监听，确保我们弹窗逻辑被优先调用
                Button transferBtn = transferTrans.GetComponent<Button>();
                transferBtn?.onClick.RemoveAllListeners();
                transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
                transferTrans.gameObject.SetActive(true);
                controlPanelTransferGameObjects[__instance] = transferTrans.gameObject;
            }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
            if (capacityTrans != null) {
                // 同样处理容量按钮，绑定弹窗显示逻辑
                Button capBtn = capacityTrans.GetComponent<Button>();
                capBtn?.onClick.RemoveAllListeners();
                capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
                capacityTrans.gameObject.SetActive(true);
                controlPanelCapacityGameObjects[__instance] = capacityTrans.gameObject;
            }
    }

    /// <summary>关闭总控面板存储槽时移除自定义按钮的事件监听</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnClose))]
    public static void UIControlPanelStationStorage_OnClose_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
        if (transferTrans != null) {
            Button transferBtn = transferTrans.GetComponent<Button>();
            // 关闭时断开自定义监听，防止残留引用继续触发弹窗弹起
            transferBtn.onClick.RemoveAllListeners();
        }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        if (capacityTrans != null) {
            Button capBtn = capacityTrans.GetComponent<Button>();
            // 容量按钮也同步清理事件，保持一致
            capBtn.onClick.RemoveAllListeners();
        }
    }

    /// <summary>刷新总控面板存储槽的 UI 状态并同步自定义按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.RefreshValues))]
    public static void UIControlPanelStationStorage_RefreshValues_Postfix(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        // 使用本地变量方便复用监听逻辑
        if (storage?.station == null || storage.factory == null || storage.masterInspector == null) {
            return;
        }

        int buildingID = storage.factory.entityPool[storage.station.entityId].protoId;
        bool isModStation = IsInteractionStation(buildingID);
        bool isInfoTab = storage.masterInspector.currentTabPanel == EUIControlPanelStationPanel.Info;
        if (!isModStation || !isInfoTab) {
            RestoreControlPanelStorageSlot(storage);
            return;
        }

        RectTransform localRect = storage.localSdButton?.GetComponent<RectTransform>();
        // 原版 SD 按钮需要整体左移，为自定义按钮预留空间
        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (localRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(localRect)) {
                controlPanelSdButtonOriginalPosition[localRect] = localRect.anchoredPosition.x;
            }

            float localOriginal = controlPanelSdButtonOriginalPosition[localRect];
            localRect.anchoredPosition = new Vector2(localOriginal - spacingX, localRect.anchoredPosition.y);
        }

        // 远程按钮也需要移动以保持整体对齐
        if (remoteRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(remoteRect)) {
                controlPanelSdButtonOriginalPosition[remoteRect] = remoteRect.anchoredPosition.x;
            }

            float remoteOriginal = controlPanelSdButtonOriginalPosition[remoteRect];
            remoteRect.anchoredPosition = new Vector2(remoteOriginal - spacingX, remoteRect.anchoredPosition.y);
        }

        // 保证沙盒锁按钮也向左移动，与新增按钮保持一致
        RectTransform keepModeRect = storage.keepModeButton?.GetComponent<RectTransform>();
        if (keepModeRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(keepModeRect)) {
                controlPanelSdButtonOriginalPosition[keepModeRect] = keepModeRect.anchoredPosition.x;
            }

            float keepModeOriginal = controlPanelSdButtonOriginalPosition[keepModeRect];
            keepModeRect.anchoredPosition = new Vector2(keepModeOriginal - spacingX, keepModeRect.anchoredPosition.y);
        }

        var localImgRT = storage.localSdImage?.rectTransform;
        var remoteImgRT = storage.remoteSdImage?.rectTransform;
        // 按钮垂直位置需要基于塔的大小差异调整，避免遮挡原按钮
        float topY;
        float bottomY;
        if (storage.station.isStellar && localImgRT != null && remoteImgRT != null) {
            topY = localImgRT.anchoredPosition.y;
            bottomY = remoteImgRT.anchoredPosition.y;
        } else if (localImgRT != null) {
            float centerY = localImgRT.anchoredPosition.y;
            float halfGap = (localImgRT.sizeDelta.y - BtnHeight) / 2f + 2f;
            topY = centerY + halfGap;
            bottomY = centerY - halfGap;
        } else {
            topY = BtnYOffset;
            bottomY = -BtnYOffset;
        }

        if (localRect == null) {
            return;
        }

        // 确保缓存中有自定义的传输按钮引用
        if (!controlPanelTransferGameObjects.TryGetValue(storage, out _)) {
            Transform transferTrans = FindControlPanelModeButtonTransform(storage, "FE_cp_transferModeButton_" + storage.index);
            if (transferTrans != null) {
                controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
            }
        }

        // 同时保证容量按钮也已准备好
        if (!controlPanelCapacityGameObjects.TryGetValue(storage, out _)) {
            Transform capacityTrans = FindControlPanelModeButtonTransform(storage, "FE_cp_capacityModeButton_" + storage.index);
            if (capacityTrans != null) {
                controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
            }
        }

        // 获取或创建当前站点的传输模式配置
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            // 同步按钮文本与配色，体现当前传输模式
            transferGO.SetActive(storage.localSdButton.gameObject.activeSelf);
            Text transferText = transferGO.GetComponentInChildren<Text>();
            if (transferText != null) {
                transferText.text = transferMode switch {
                    ETransferMode.Sync => "双向同步".Translate(),
                    ETransferMode.Upload => "仅上传".Translate(),
                    ETransferMode.Download => "仅下载".Translate(),
                    _ => "双向同步".Translate()
                };
            }

            Image transferImage = transferGO.GetComponent<Image>();
            if (transferImage != null) {
                transferImage.color = transferMode switch {
                    ETransferMode.Sync => storage.masterInspector.storageNoneSpColor,
                    ETransferMode.Upload => storage.masterInspector.storageDemandColor,
                    ETransferMode.Download => storage.masterInspector.storageSupplyColor,
                    _ => storage.masterInspector.storageNoneSpColor
                };
            }

            RectTransform transferRt = transferGO.GetComponent<RectTransform>();
            // 调整按钮坐标与高度，匹配我们预留的 spacingX 和 BtnHeight
            transferRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, topY);
            transferRt.sizeDelta = new Vector2(transferRt.sizeDelta.x, BtnHeight);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            // 容量按钮根据模式开/关，并同步文字颜色
            ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                slotCapacityMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
            ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
            capacityGO.SetActive(storage.localSdButton.gameObject.activeSelf
                                 && transferMode is ETransferMode.Sync or ETransferMode.Upload);

            Text capText = capacityGO.GetComponentInChildren<Text>();
            if (capText != null) {
                capText.text = capacityMode switch {
                    ECapacityMode.Limited => "有限上传".Translate(),
                    ECapacityMode.Infinite => "无限上传".Translate(),
                    _ => "有限上传".Translate()
                };
            }

            Image capImage = capacityGO.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = capacityMode switch {
                    ECapacityMode.Limited => storage.masterInspector.storageSupplyColor,
                    ECapacityMode.Infinite => storage.masterInspector.storageDemandColor,
                    _ => storage.masterInspector.storageSupplyColor
                };
            }

            RectTransform capRt = capacityGO.GetComponent<RectTransform>();
            // 宽高调整保证新按钮位于原位置之上
            capRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, bottomY);
            capRt.sizeDelta = new Vector2(capRt.sizeDelta.x, BtnHeight);
        }
    }

    /// <summary>拦截总控面板 SD 按钮点击以清理 FE 弹窗状态</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnRemoteSdButtonClick))]
    public static void UIControlPanelStationStorage_OnSdButtonClick_Prefix(UIControlPanelStationStorage __instance) {
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(__instance);
        ClearPopupState(popupStateKey);
        SetControlPanelStoragePopupShift(__instance, true);
    }

    /// <summary>处理总控面板选项按钮 0 的点击逻辑</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton0Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton0Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    /// <summary>处理总控面板选项按钮 1 的点击逻辑</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton1Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton1Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    /// <summary>打开独立物流站存储槽时绑定模式按钮事件</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnOpen))]
    public static void UIStationStorage__OnOpen_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            // 给自定义传输按钮绑定弹窗展示，替代原选项逻辑
            Button transferBtn = tTrans.GetComponent<Button>();
            transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            // 同步为容量按钮绑定弹窗入口
            Button capBtn = cTrans.GetComponent<Button>();
            capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
        }
    }

    /// <summary>关闭独立物流站存储槽时移除模式按钮事件</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnClose))]
    public static void UIStationStorage__OnClose_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            Button transferBtn = tTrans.GetComponent<Button>();
            // 关闭时断开自定义按钮事件，防止下一次打开时重复绑定
            transferBtn.onClick.RemoveAllListeners();
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            Button capBtn = cTrans.GetComponent<Button>();
            // 容量按钮同样清理事件
            capBtn.onClick.RemoveAllListeners();
        }
    }

    /// <summary>刷新独立物流站存储槽时同步按钮位置、颜色与宽度</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        // 重命名
        UIStationStorage storage = __instance;
        // 给局部变量起个名字便于以后对 UI 进行操作
        // 获取塔对应的物品ID
        int buildingID = storage.stationWindow.factory.entityPool[storage.station.entityId].protoId;

        // 栏位本身
        RectTransform rectTransform = storage.transform.GetComponent<RectTransform>();
        // 如果不是我们的交互塔，则恢复原版布局和按钮状态
        if (!IsInteractionStation(buildingID)) {
            SetStoragePopupShift(storage, false);
            // 还原栏位宽度、沙盒锁位置
            if (rectTransform != null && storageWidth.ContainsKey(storage)) {
                storageWidth.TryRemove(storage, out _);
                rectTransform.sizeDelta =
                    new Vector2(rectTransform.sizeDelta.x - spacingX, rectTransform.sizeDelta.y);
                RectTransform keepModeComponent = storage.keepModeButton.GetComponent<RectTransform>();
                keepModeComponent.anchoredPosition = new Vector2(keepModeComponent.anchoredPosition.x + spacingX,
                    keepModeComponent.anchoredPosition.y);
            }

            // 隐藏两个新增的按钮
            if (transferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
                transferGO.SetActive(false);
            }

            if (capacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
                capacityGO.SetActive(false);
            }

            return;
        }

        // 增加栏位宽度、向前移动沙盒锁位置
        // 初次遇到自定义塔时扩展栏位宽度，并把沙盒锁按钮向前移
        if (rectTransform != null && !storageWidth.ContainsKey(storage)) {
            storageWidth.TryAdd(storage, true);
            rectTransform.sizeDelta =
                new Vector2(rectTransform.sizeDelta.x + spacingX, rectTransform.sizeDelta.y);
            RectTransform keepModeComponent = storage.keepModeButton.GetComponent<RectTransform>();
            keepModeComponent.anchoredPosition = new Vector2(keepModeComponent.anchoredPosition.x - spacingX,
                keepModeComponent.anchoredPosition.y);
        }

        // 向前移动两个原版的按钮
        // PS:这两个按钮每次都会自动回去，所有每次都需要移动
        if (storageWidth.ContainsKey(storage)) {
            RectTransform localComponent = storage.localSdButton.GetComponent<RectTransform>();
            localComponent.anchoredPosition = new Vector2(localComponent.anchoredPosition.x - spacingX,
                localComponent.anchoredPosition.y);
            RectTransform remoteComponent = storage.remoteSdButton.GetComponent<RectTransform>();
            remoteComponent.anchoredPosition = new Vector2(remoteComponent.anchoredPosition.x - spacingX,
                remoteComponent.anchoredPosition.y);
        }

        // 根据塔类型计算垂直位置
        var localImgRT = storage.localSdImage?.rectTransform;
        var remoteImgRT = storage.remoteSdImage?.rectTransform;
        float topY, bottomY;
        if (storage.station != null && storage.station.isStellar && localImgRT != null && remoteImgRT != null) {
            // 大塔：使用与官方按钮相同的 Y (14 / -14)
            topY = localImgRT.anchoredPosition.y;
            bottomY = remoteImgRT.anchoredPosition.y;
        } else if (localImgRT != null) {
            // 小塔：位于官方按钮的中心
            float centerY = localImgRT.anchoredPosition.y;
            float halfGap = (localImgRT.sizeDelta.y - BtnHeight) / 2f;
            // 中间增加 4f 的空隙（上下各 +2f）
            const float PlanetExtraGap = 4f;
            halfGap += PlanetExtraGap / 2f;
            topY = centerY + halfGap;
            bottomY = centerY - halfGap;
        } else {
            topY = BtnYOffset;
            bottomY = -BtnYOffset;
        }

        // 原版本地物流模式按钮用于确定我们新按钮的起始坐标
        RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);

        if (transferGameObjects.TryGetValue(storage, out GameObject tTrans)) {
            // 显示按钮并同步当前传输模式的文本与颜色
            // 显示按钮
            tTrans.SetActive(storage.localSdButton.gameObject.activeSelf);
            Text transferText = tTrans.GetComponentInChildren<Text>();
            // 根据当前模式更新文本
            if (transferText != null) {
                transferText.text = eTransferMode switch {
                    ETransferMode.Sync => "双向同步".Translate(), ETransferMode.Upload => "仅上传".Translate(),
                    ETransferMode.Download => "仅下载".Translate(),
                    _ => "双向同步".Translate()
                };
            }
            Image capImage = tTrans.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = eTransferMode switch {
                    ETransferMode.Sync => storage.noneSpColor, ETransferMode.Upload => storage.demandColor,
                    ETransferMode.Download => storage.supplyColor,
                    _ => storage.noneSpColor
                };
            }

            // 更新位置
            RectTransform rt = tTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }

        if (capacityGameObjects.TryGetValue(storage, out GameObject cTrans)) {
            // 根据当前传输/容量设置决定容量按钮是否可见以及文本颜色
            // 通过实体ID拿到整个塔的同步模式
            ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                slotCapacityMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ECapacityMode>());

            // 通过栏位索引，拿到对应栏位的同步模式
            ECapacityMode eCapacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
            cTrans.SetActive(storage.localSdButton.gameObject.activeSelf
                             && eTransferMode is ETransferMode.Sync or ETransferMode.Upload);
            Text capText = cTrans.GetComponentInChildren<Text>();
            if (capText != null) {
                capText.text = eCapacityMode switch {
                    ECapacityMode.Limited => "有限上传".Translate(), ECapacityMode.Infinite => "无限上传".Translate(),
                    _ => "有限上传".Translate()
                };
            }
            Image capImage = cTrans.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = eCapacityMode switch {
                    ECapacityMode.Limited => storage.supplyColor, ECapacityMode.Infinite => storage.demandColor,
                    _ => storage.supplyColor
                };
            }

            RectTransform rt = cTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }
    }


    /// <summary>独立窗口每帧更新时扩展能量条与位置</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnUpdate))]
    public static void UIStationWindow_OnUpdate_Prefix(UIStationWindow __instance) {
        // 重命名
        UIStationWindow stationWindow = __instance;
        StationComponent station = stationWindow.transport.stationPool[stationWindow.stationId];
        // 获取塔对应的物品ID
        int buildingID = stationWindow.factory.entityPool[station.entityId].protoId;
        // 如果不是自定义的塔
        if (!IsInteractionStation(buildingID)) {
            return;
        }

        // 是否解锁曲速
        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;
        // 增加能量条的宽度
        stationWindow.powerGroupRect.sizeDelta =
            new Vector2((station.isStellar ? (logisticShipWarpDrive ? 320f : 380f) : 440f) + spacingX, 40f);
        // 调整能量数值的位置
        float num2 = (float)station.energy / (float)station.energyMax;
        float num4 = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;
        if ((double)num2 > 0.699999988079071) {
            stationWindow.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round((float)((double)num4 * (double)num2 - 30.0)), 0.0f);
        } else {
            stationWindow.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round((float)((double)num4 * (double)num2 + 30.0)), 0.0f);
        }
    }

    /// <summary>切换站点 ID 时更新 UI 面板宽度与文本</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnStationIdChange))]
    public static void UIStationWindow_OnStationIdChange_Postfix(UIStationWindow __instance) {
        __instance.event_lock = true;
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        bool isModStation = IsModStation(__instance, station);
        SetWindowWidenState(__instance, isModStation);
        if (station == null || station.id != __instance.stationId) {
            __instance.event_lock = false;
            return;
        }

        // 修改集装输出的描述
        Component label = __instance.techPilerButton.transform.Find("label");
        Text text = label.GetComponent<Text>();
        // 只处理物流交互站
        if (!isModStation) {
            // 还原，避免不关窗口直接切换的时候显示错误
            text.text = "  使用科技上限";
            __instance.event_lock = false;
            return;
        }

        text.text = "  使用强化上限";
        RefreshInteractionStationPilerUI(
            station,
            __instance.minPilerSlider,
            __instance.minPilerValue,
            __instance.minPilerGroup.gameObject,
            __instance.pilerTechGroup.gameObject
        );

        __instance.event_lock = false;
    }

    /// <summary>
    /// 修改物流交互站的面板的高度
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.RefreshTrans))]
    public static void UIStationWindow_RefreshTrans_Postfix(UIStationWindow __instance, StationComponent station) {
        if (__instance?.factory == null || station == null || station.entityId <= 0) {
            return;
        }

        bool isModStation = IsModStation(__instance, station);
        AdjustSliders(__instance, isModStation);
        RectTransform windowTrans = __instance.windowTrans;
        if (windowTrans == null) {
            return;
        }

        float originalWidth = GetOrCacheOriginal(windowOriginalWidth, windowTrans, x => x.sizeDelta.x);
        float targetWidth = originalWidth + (isModStation ? spacingX : 0f);
        windowTrans.sizeDelta = new Vector2(targetWidth, windowTrans.sizeDelta.y);

        if (!isModStation) {
            return;
        }

        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        if (maxStack <= 1) {
            // 没解锁堆叠，不调整
            return;
        }

        if (station.isStellar) {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth,
                360f + 76f * station.storage.Length + 36f);
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 186f);
        } else {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth,
                300f + 76f * station.storage.Length + 36f);
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 126f);
        }
    }

    /// <summary>在独立面板显示传输模式弹窗并更新按钮提示</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    private static void ShowTransferPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length)
            stationStore = station.storage[storage.index];
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null)
            return;

        // 通过实体和槽位读取当前传输模式，以便为选项按钮提示下一步

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        // option0 / option1 的文字和颜色，按“当前模式”动态提示“点下去会切到什么模式”。
        storage.optionImage0.color = eTransferMode switch {
            ETransferMode.Sync => storage.demandColor,
            ETransferMode.Upload => storage.supplyColor,
            _ => storage.noneSpColor
        };
        storage.optionImage1.color = eTransferMode switch {
            ETransferMode.Sync => storage.supplyColor,
            ETransferMode.Upload => storage.noneSpColor,
            _ => storage.demandColor
        };
        storage.optionText0.text = eTransferMode switch {
            ETransferMode.Sync => "仅上传".Translate(),
            ETransferMode.Upload => "仅下载".Translate(),
            _ => "双向同步".Translate()
        };
        storage.optionText1.text = eTransferMode switch {
            ETransferMode.Sync => "仅下载".Translate(),
            ETransferMode.Upload => "双向同步".Translate(),
            _ => "仅上传".Translate()
        };
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // 按钮点击后记录这是 FE 控制的传输弹窗，以便拦截后续事件
        // 记录：这个弹窗是 FE 接管的、当前是否打开、当前是传输模式弹窗。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧，避免挡住原按钮区域。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>在独立面板显示容量模式弹窗并设置文本与颜色</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    private static void ShowCapacityPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length)
            stationStore = station.storage[storage.index];
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null)
            return;

        // 容量弹窗无需根据当前模式读取字典，直接展示选项
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.demandColor;
        storage.optionImage1.color = storage.supplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // false 代表当前是“容量模式弹窗”，后续点击 option 时走容量逻辑。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>在总控面板显示传输模式弹窗并同步 FE 弹窗状态</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    private static void ShowTransferPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = storage.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }

        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = station != null && storage.factory != null
            ? LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId)
            : null;
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

        // 确认弹窗存在，否则无法展示
        if (storage.popupBoxRect == null) {
            return;
        }

        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = transferMode switch {
            ETransferMode.Sync => storage.masterInspector.storageDemandColor,
            ETransferMode.Upload => storage.masterInspector.storageSupplyColor,
            _ => storage.masterInspector.storageNoneSpColor
        };
        storage.optionImage1.color = transferMode switch {
            ETransferMode.Sync => storage.masterInspector.storageSupplyColor,
            ETransferMode.Upload => storage.masterInspector.storageNoneSpColor,
            _ => storage.masterInspector.storageDemandColor
        };
        storage.optionText0.text = transferMode switch {
            ETransferMode.Sync => "仅上传".Translate(),
            ETransferMode.Upload => "仅下载".Translate(),
            _ => "双向同步".Translate()
        };
        storage.optionText1.text = transferMode switch {
            ETransferMode.Sync => "仅下载".Translate(),
            ETransferMode.Upload => "双向同步".Translate(),
            _ => "仅上传".Translate()
        };
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // 记录弹窗归属与类型，确保 option 点击时只拦截 FE 的弹窗。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 总控面板：点击 mod 按钮时，弹窗回归原位（不再左移）。
        SetControlPanelStoragePopupShift(storage, false);
    }

    /// <summary>在总控面板显示容量模式弹窗并记录弹窗类型</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    private static void ShowCapacityPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = storage.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }

        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = station != null && storage.factory != null
            ? LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId)
            : null;
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

        // 确保弹窗存在，后续才能设置选项与记录状态
        if (storage.popupBoxRect == null) {
            return;
        }

        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.masterInspector.storageDemandColor;
        storage.optionImage1.color = storage.masterInspector.storageSupplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // false 代表当前是“容量模式弹窗”。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 总控面板：点击 mod 按钮时，弹窗回归原位。
        SetControlPanelStoragePopupShift(storage, false);
    }

    /// <summary>拦截原始存储按钮点击并复位弹窗</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnRemoteSdButtonClick))]
    public static void UIStationStorage_OnSdButtonClick_Prefix(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        // 点击原版物流按钮时，先清理 FE 弹窗状态，再把弹窗复位。
        ClearPopupState(popupStateKey);
        SetStoragePopupShift(storage, false);
    }

    // Intercept option clicks when our popup is active
    /// <summary>拦截独立存储选项 0 点击并应用 FE 模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <returns>是否让原版继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton0Click))]
    public static bool UIStationStorage_OnOptionButton0Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    /// <summary>拦截独立存储选项 1 点击并应用 FE 模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <returns>是否让原版继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton1Click))]
    public static bool UIStationStorage_OnOptionButton1Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    /// <summary>基于 FE 弹窗逻辑处理独立面板选项点击并更新模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <param name="idx">选项索引（0/1）</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    private static bool HandleOptionClick(UIStationStorage __instance, int idx) {
        try {
            UIStationStorage storage = __instance;
            // 统一使用 storage 便于后续代码复用
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            // 只有 FE 打开的弹窗才拦截；否则让原游戏逻辑继续执行。
            // 仅当 FE 弹窗激活时才拦截，其他情况交还给原始逻辑
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            UIStationWindow stationWindow = __instance.stationWindow;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                // 防御性检查：弹窗对象变化或已关闭时，不接管点击。
                return true;
            }

            StationStore stationStore = new StationStore();
            // 安全读取槽位数据，避免数组越界
            if (station != null && storage.index < station.storage.Length)
                stationStore = station.storage[storage.index];
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = LDB.items.Select((int)stationWindow.factory.entityPool[station.entityId].protoId);
            if (itemProto1 == null || itemProto2 == null)
                return false;
            // 数据收集完成后继续，根据 popup 类型走不同逻辑
            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                // 当前是“传输模式弹窗”：按 idx 计算下一个传输模式。
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(eTransferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, eTransferMode);
            } else {
                // 当前是“容量模式弹窗”：option0=无限上传，option1=有限上传。
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode eCapacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, eCapacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, eCapacityMode);
                }
            }

            // 选完就关闭弹窗，并标记为“非 FE 弹窗激活态”。
            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;// prevent original OnOptionButton* handlers
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick error: {ex}");
            return true;
        }
    }

    /// <summary>基于 FE 弹窗逻辑处理总控面板选项点击并更新模式</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <param name="idx">选项索引（0/1）</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    private static bool HandleOptionClick(UIControlPanelStationStorage __instance, int idx) {
        try {
            UIControlPanelStationStorage storage = __instance;
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            // 只有 FE 打开的弹窗才拦截；否则走原版逻辑。
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                // 弹窗引用不一致时，不接管，避免误拦截。
                return true;
            }

            StationStore stationStore = new StationStore();
            if (station != null && storage.index < station.storage.Length) {
                stationStore = station.storage[storage.index];
            }
            // 复制槽位数据用于安全检查
            
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = station != null && storage.factory != null
                ? LDB.items.Select(storage.factory.entityPool[station.entityId].protoId)
                : null;
            if (itemProto1 == null || itemProto2 == null) {
                return false;
            }
            // 有效性检查过后开始应用对应模式逻辑

            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                // 传输模式切换。
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(transferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, transferMode);
            } else {
                // 容量模式切换。
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, capacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, capacityMode);
                }
            }

            // 与独立面板保持一致：处理完立即收起弹窗。
            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick(UIControlPanelStationStorage) error: {ex}");
            return true;
        }
    }

    /// <summary>切换总控站点 ID 时刷新面板的堆叠描述和按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnStationIdChange))]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.RefreshTabPanelUI))]
    public static void UIControlPanelStationInspector_OnStationIdChange_Postfix(
        UIControlPanelStationInspector __instance) {
        __instance.event_lock = true;
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            __instance.event_lock = false;
            return;
        }

        // 修改集装输出的描述
        Component label = __instance.techPilerButton.transform.Find("label");
        Text text = label.GetComponent<Text>();
        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            // 还原，避免不关窗口直接切换的时候显示错误
            text.text = "  使用科技上限";
            __instance.event_lock = false;
            return;
        }

        text.text = "  使用强化上限";
        RefreshInteractionStationPilerUI(
            station,
            __instance.minPilerSlider,
            __instance.minPilerValue,
            __instance.minPilerGroup.gameObject,
            __instance.pilerTechGroup.gameObject
        );

        __instance.event_lock = false;
    }

    /// <summary>查找总控面板右侧区域的 RectTransform 引用</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    /// <returns>右侧区域的 RectTransform 或 null</returns>
    private static RectTransform FindInspectorRightGroupRect(UIControlPanelStationInspector inspector) {
        if (inspector == null) {
            return null;
        }

        RectTransform inspectorRect = inspector.rectTransform;
        Transform rightGroupTransform = inspectorRect?.parent?.Find("right-group");
        if (rightGroupTransform == null && inspector.masterWindow != null) {
            rightGroupTransform = inspector.masterWindow.transform.Find("right-group");
        }

        if (rightGroupTransform == null && inspectorRect != null && inspectorRect.name == "right-group") {
            return inspectorRect;
        }

        if (rightGroupTransform == null && inspector.masterWindow != null) {
            RectTransform[] children = inspector.masterWindow.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform child in children) {
                if (child != null && child.name == "right-group") {
                    rightGroupTransform = child;
                    break;
                }
            }
        }

        return rightGroupTransform as RectTransform;
    }

    /// <summary>将右侧区域恢复到原始宽度</summary>
    /// <param name="rightGroupRect">右侧组的 RectTransform</param>
    private static void RestoreInspectorRightGroup(RectTransform rightGroupRect) {
        if (rightGroupRect == null) {
            return;
        }

        if (inspectorOriginalRightGroupWidth.TryGetValue(rightGroupRect, out float originalWidth)) {
            rightGroupRect.sizeDelta = new Vector2(originalWidth, rightGroupRect.sizeDelta.y);
        }
    }

    /// <summary>还原总控面板单个存储槽的按钮位置与状态</summary>
    /// <param name="storage">目标的 UIControlPanelStationStorage</param>
    private static void RestoreControlPanelStorageSlot(UIControlPanelStationStorage storage) {
        if (storage == null) {
            return;
        }

        RectTransform localRect = storage.localSdButton?.GetComponent<RectTransform>();
        if (localRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(localRect, out float localOriginal)) {
            localRect.anchoredPosition = new Vector2(localOriginal, localRect.anchoredPosition.y);
        }

        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (remoteRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(remoteRect, out float remoteOriginal)) {
            remoteRect.anchoredPosition = new Vector2(remoteOriginal, remoteRect.anchoredPosition.y);
        }

        RectTransform keepModeRect = storage.keepModeButton?.GetComponent<RectTransform>();
        if (keepModeRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(keepModeRect, out float keepModeOriginal)) {
            keepModeRect.anchoredPosition = new Vector2(keepModeOriginal, keepModeRect.anchoredPosition.y);
        }

        SetControlPanelStoragePopupShift(storage, false);

        ClearPopupState(GetPopupStateKey(storage));

        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            transferGO.SetActive(false);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            capacityGO.SetActive(false);
        }
    }

    /// <summary>还原总控面板所有存储槽的宽度</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    private static void RestoreInspectorStorageWidths(UIControlPanelStationInspector inspector) {
        if (inspector?.storageUIs == null) {
            return;
        }

        for (int i = 0; i < inspector.storageUIs.Length; i++) {
            UIControlPanelStationStorage storage = inspector.storageUIs[i];
            if (storage == null) {
                continue;
            }

            RectTransform topGroup = storage.transform.Find("top-group") as RectTransform;
            if (topGroup != null
                && inspectorOriginalTopGroupWidth.TryGetValue(topGroup, out float originalTopGroupWidth)) {
                topGroup.sizeDelta = new Vector2(originalTopGroupWidth, topGroup.sizeDelta.y);
            }

            RectTransform bg = storage.transform.Find("bg") as RectTransform;
            if (bg != null && inspectorOriginalBgWidth.TryGetValue(bg, out float originalBgWidth)) {
                bg.sizeDelta = new Vector2(originalBgWidth, bg.sizeDelta.y);
            }
        }
    }

    /// <summary>扩展总控面板存储槽的宽度以容纳模式按钮</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    private static void WidenInspectorStorageWidths(UIControlPanelStationInspector inspector) {
        if (inspector?.storageUIs == null) {
            return;
        }

        for (int i = 0; i < inspector.storageUIs.Length; i++) {
            UIControlPanelStationStorage storage = inspector.storageUIs[i];
            if (storage == null) {
                continue;
            }

            RectTransform topGroup = storage.transform.Find("top-group") as RectTransform;
            if (topGroup != null) {
                float originalTopGroupWidth = GetOrCacheOriginal(inspectorOriginalTopGroupWidth, topGroup,
                    x => x.rect.width > 0f ? x.rect.width : x.sizeDelta.x);
                topGroup.sizeDelta = new Vector2(originalTopGroupWidth + spacingX, topGroup.sizeDelta.y);
            }

            RectTransform bg = storage.transform.Find("bg") as RectTransform;
            if (bg != null) {
                float originalBgWidth = GetOrCacheOriginal(inspectorOriginalBgWidth, bg,
                    x => x.rect.width > 0f ? x.rect.width : x.sizeDelta.x);
                bg.sizeDelta = new Vector2(originalBgWidth + spacingX, bg.sizeDelta.y);
            }
        }
    }

    /// <summary>
    /// 加宽总控面板物流站信息页签（能量条和数值位置）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector._OnUpdate))]
    public static void UIControlPanelStationInspector_OnUpdate_Postfix(UIControlPanelStationInspector __instance) {
        // 只处理物流交互站：检查stationId和工厂是否有效
        if (__instance.stationId == 0 || __instance.factory == null) {
            return;
        }

        // 从stationPool获取物流站组件，并验证ID匹配
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return;
        }

        // 确保总控面板间距已计算
        EnsureControlPanelSpacing(__instance);

        // 获取建筑ID，判断是否为物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        // 获取右侧组RectTransform，用于后续调整
        RectTransform rightGroupRect = FindInspectorRightGroupRect(__instance);
        
        // 如果不是物流交互站，恢复所有UI到原始状态
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            // 恢复所有存储槽的按钮位置和状态
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            // 恢复存储槽宽度到原始值
            RestoreInspectorStorageWidths(__instance);

            // 恢复状态筛选组的位置
            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            // 恢复主窗口宽度到原始值
            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            // 恢复右侧组宽度到原始值
            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        // 只在信息页签生效，其他页签恢复原始状态
        if (__instance.currentTabPanel != EUIControlPanelStationPanel.Info) {
            // 恢复所有存储槽的按钮位置和状态
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            // 恢复存储槽宽度到原始值
            RestoreInspectorStorageWidths(__instance);

            // 恢复状态筛选组的位置
            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            // 恢复主窗口宽度到原始值
            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            // 恢复右侧组宽度到原始值
            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        // 加宽存储槽宽度以适应自定义按钮
        WidenInspectorStorageWidths(__instance);

        // 检查是否解锁曲速科技，影响能量条宽度计算
        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;

        RectTransform powerRect = __instance.powerGroupRect;

        // 根据是否为星际站和曲速解锁状态计算基础能量条宽度
        float basePowerWidth = station.isStellar
            ? (logisticShipWarpDrive ? 320f : 380f)
            : 440f;
        // 设置能量条宽度（加上额外的间距）
        powerRect.sizeDelta = new Vector2(basePowerWidth + spacingX, 40f);

        // 调整能量数值的位置（基于原始长度+spacingX）
        // 计算当前能量百分比
        float num2 = (float)station.energy / (float)station.energyMax;
        // 计算能量数值显示位置的基础长度
        float num4 = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;

        // 根据能量百分比决定数值显示位置，避免与能量条填充部分重叠
        if (num2 > 0.7f) {
            // 高能量时，数值显示在能量条左侧
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 - 30f), 0f);
        } else {
            // 低能量时，数值显示在能量条右侧
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 + 30f), 0f);
        }

        // 调整主窗口宽度
        RectTransform masterRect = __instance.masterWindow != null
            ? __instance.masterWindow.transform as RectTransform
            : null;
        if (masterRect != null) {
            // 首次访问时缓存原始宽度
            if (!inspectorOriginalMasterWidth.ContainsKey(masterRect)) {
                inspectorOriginalMasterWidth[masterRect] = masterRect.sizeDelta.x;
            }
            // 设置主窗口宽度为原始宽度加上额外间距
            masterRect.sizeDelta = new Vector2(inspectorOriginalMasterWidth[masterRect] + spacingX, masterRect.sizeDelta.y);
        }

        // 调整状态筛选组的位置
        RectTransform stateGroupTrans = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
        if (stateGroupTrans != null) {
            // 首次访问时缓存原始位置
            if (!filterOriginalStateGroupPosition.ContainsKey(stateGroupTrans)) {
                filterOriginalStateGroupPosition[stateGroupTrans] = stateGroupTrans.anchoredPosition.x;
            }

            // 设置状态筛选组位置为原始位置加上额外间距
            stateGroupTrans.anchoredPosition =
                new Vector2(filterOriginalStateGroupPosition[stateGroupTrans] + spacingX, stateGroupTrans.anchoredPosition.y);
        }

        // 调整右侧组宽度
        if (rightGroupRect != null) {
            // 首次访问时缓存原始宽度
            if (!inspectorOriginalRightGroupWidth.ContainsKey(rightGroupRect)) {
                inspectorOriginalRightGroupWidth[rightGroupRect] = rightGroupRect.sizeDelta.x;
            }

            // 设置右侧组宽度为原始宽度加上额外间距
            rightGroupRect.sizeDelta = new Vector2(
                inspectorOriginalRightGroupWidth[rightGroupRect] + spacingX, rightGroupRect.sizeDelta.y);
        }
    }

    /// <summary>在独立窗口修改集装滑块时同步强化上限</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    /// <param name="value">新的滑块数值</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnMinPilerValueChange))]
    public static bool UIStationWindow_OnMinPilerValueChange_Prefix(UIStationWindow __instance, float value) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        // 不是自动
        if (!__instance.techPilerCheck.enabled) {
            int newVal = GetClampedPilerCount(value);

            __instance.transport.stationPool[__instance.stationId].pilerCount = newVal;
            __instance.minPilerValue.text = newVal.ToString();
        }

        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>在总控面板修改集装滑块时同步强化上限</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <param name="value">新的滑块数值</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnMinPilerValueChange))]
    public static bool UIControlPanelStationInspector_OnMinPilerValueChange_Prefix(
        UIControlPanelStationInspector __instance, float value) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        // 不是自动
        if (!__instance.techPilerCheck.enabled) {
            int newVal = GetClampedPilerCount(value);

            __instance.transport.stationPool[__instance.stationId].pilerCount = newVal;
            __instance.minPilerValue.text = newVal.ToString();
        }

        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>切换独立窗口集装自动选项并刷新 UI</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnTechPilerClick))]
    public static bool UIStationWindow_OnTechPilerClick_Prefix(UIStationWindow __instance) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        __instance.techPilerCheck.enabled = !__instance.techPilerCheck.enabled;
        int maxStack = GetInteractionStationMaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxStack;
        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>切换总控面板集装自动选项并刷新 UI</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnTechPilerClick))]
    public static bool
        UIControlPanelStationInspector_OnTechPilerClick_Prefix(UIControlPanelStationInspector __instance) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        __instance.techPilerCheck.enabled = !__instance.techPilerCheck.enabled;
        int maxStack = GetInteractionStationMaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxStack;
        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>
    /// 实际处理时，物流交互站的集装上限使用强化上限
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._station_output_parallel))]
    private static IEnumerable<CodeInstruction> GameLogic__station_output_parallel_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_S),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_S),// entitySignPool
            new CodeMatch(OpCodes.Ldloc_S),// stationPilerLevel
            new CodeMatch(OpCodes.Ldloc_S),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in GameLogic._station_output_parallel");
            return instructions;
        }

        // 移动到 stationPilerLevel 参数加载的位置
        matcher.Advance(2);
        // 替换为 GetOutputStack
        matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, (byte)7))// 加载 factory
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)19),// 加载 &local2
                new CodeInstruction(OpCodes.Ldind_Ref),// 解引用得到 StationComponent
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(StationManager), nameof(GetOutputStack)))// 调用方法
            );
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 实际处理时，物流交互站的集装上限使用强化上限
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick_OutputToBelt))]
    private static IEnumerable<CodeInstruction> PlanetTransport_GameTick_OutputToBelt_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_0),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_1),// entitySignPool
            new CodeMatch(OpCodes.Ldarg_1),// maxPilerCount
            new CodeMatch(OpCodes.Ldloc_2),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in PlanetTransport.GameTick_OutputToBelt");
            return instructions;
        }

        // 移动到 maxPilerCount 参数加载的位置
        matcher.Advance(2);
        // 替换为 GetOutputStack
        matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))// this (PlanetTransport)
            .Insert(
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetTransport), "factory")),// factory
                new CodeInstruction(OpCodes.Ldarg_0),// this (PlanetTransport) 再次加载
                new CodeInstruction(OpCodes.Ldfld,
                    AccessTools.Field(typeof(PlanetTransport), "stationPool")),// stationPool
                new CodeInstruction(OpCodes.Ldloc_3),// index
                new CodeInstruction(OpCodes.Ldelem_Ref),// stationPool[index]
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(StationManager), nameof(GetOutputStack)))// 调用方法
            );
        return matcher.InstructionEnumeration();
    }

    /// <summary>根据交互站类型返回正确的集装上限</summary>
    /// <param name="factory">行星工厂实例</param>
    /// <param name="station">目标站点组件</param>
    /// <returns>自定义交互站使用强化堆叠，否则使用历史等级</returns>
    private static int GetOutputStack(PlanetFactory factory, StationComponent station) {
        int buildingID = factory.entityPool[station.entityId].protoId;
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站
            ? LDB.items.Select(buildingID).MaxStack()
            : GameMain.history.stationPilerLevel;
    }

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
                        dict[slotIndex] = (ETransferMode)br.ReadInt32();
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
                        dict[slotIndex] = (ECapacityMode)br.ReadInt32();
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
