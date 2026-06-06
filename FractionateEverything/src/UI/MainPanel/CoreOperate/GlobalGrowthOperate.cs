using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Progression;
using FE.Logic.VanillaRecipes;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Setting;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using static FE.Logic.Items.ItemManager;
using static FE.UI.Layout.GridDsl;
using static FE.UI.Foundation.RectTransformUtils;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 全局成长页面。承载统一堆叠和原版配方全局时间上限这类跨系统升级。
/// </summary>
public static class GlobalGrowthOperate {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtStackTitle;
    private static Text txtStackStatus;
    private static Text txtStackEffect;
    private static Text txtStackCost;
    private static Text txtTimeTitle;
    private static Text txtTimeStatus;
    private static Text txtTimeEffect;
    private static Text txtTimeCost;
    private static UIButton btnStackUpgrade;
    private static UIButton btnTimeLimitUpgrade;

    public static void AddTranslations() {
        Register("全局成长", "Global Growth", "全局成长");
        Register("统一堆叠", "Unified Stacking", "统一堆叠");
        Register("统一堆叠说明",
            "Controls the maximum stacking level for fractionators, logistics stations, pilers, and stack inserters after Logistics Stacking System is unlocked.",
            "集装物流系统解锁后，统一控制分馏塔、物流站、集装机与集装分拣器的最大堆叠层数。");
        Register("当前堆叠", "Current stack", "当前堆叠");
        Register("下一堆叠", "Next stack", "下一堆叠");
        Register("升级堆叠", "Upgrade stack", "升级堆叠");
        Register("升级消耗", "Upgrade cost", "升级消耗");
        Register("全局成长页说明",
            "Upgrade global stacking and vanilla recipe time caps here.",
            "在这里升级统一堆叠与原版配方全局时间上限。");
        Register("影响范围", "Affected systems", "影响范围");
        Register("全局成长-堆叠影响",
            "Fractionators / logistics stations / pilers / stack inserters / related runtime caps",
            "分馏塔 / 物流站 / 集装机 / 集装分拣器 / 相关运行时上限");
        Register("全局成长-时间影响",
            "Raises the maximum time reduction allowed for vanilla recipes. Individual recipes still need fragment upgrades.",
            "提高原版配方允许达到的最大时间缩短上限；单条配方仍需消耗残片单独升级。");
        Register("无消耗", "No cost", "无消耗");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("全局成长", objectName: "global-growth-header", pos: (0, 0),
                        onBuilt: refs => header = refs),
                    Grid(pos: (1, 0),
                        cols: [1, 1],
                        columnGap: PageLayout.Gap,
                        children: [
                            ContentCard(pos: (0, 0), objectName: "global-growth-stack-card",
                                strong: true,
                                rows: [Px(26f), 1, 1, 1, Px(44f)],
                                rowGap: 6f,
                                children: [
                                    CardTitleNode("统一堆叠", onBuilt: text => txtStackTitle = text,
                                        pos: (0, 0), objectName: "global-growth-stack-title"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtStackStatus = text,
                                        pos: (1, 0), objectName: "global-growth-stack-status"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtStackEffect = text,
                                        pos: (2, 0), objectName: "global-growth-stack-effect"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtStackCost = text,
                                        pos: (3, 0), objectName: "global-growth-stack-cost"),
                                    ButtonNode("升级堆叠", onClick: UpgradeStack,
                                        onBuilt: btn => btnStackUpgrade = btn,
                                        pos: (4, 0), objectName: "global-growth-stack-upgrade"),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "global-growth-time-card",
                                strong: true,
                                rows: [Px(26f), 1, 1, 1, Px(44f)],
                                rowGap: 6f,
                                children: [
                                    CardTitleNode("全局时间上限", onBuilt: text => txtTimeTitle = text,
                                        pos: (0, 0), objectName: "global-growth-time-title"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtTimeStatus = text,
                                        pos: (1, 0), objectName: "global-growth-time-status"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtTimeEffect = text,
                                        pos: (2, 0), objectName: "global-growth-time-effect"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtTimeCost = text,
                                        pos: (3, 0), objectName: "global-growth-time-cost"),
                                    ButtonNode("提升上限", onClick: UpgradeGlobalTimeLimit,
                                        onBuilt: btn => btnTimeLimitUpgrade = btn,
                                        pos: (4, 0), objectName: "global-growth-time-upgrade"),
                                ]),
                        ]),
                ]));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "全局成长".Translate().WithColor(Orange);
        header.Summary.text = "全局成长页说明".Translate().WithColor(White);

        txtStackTitle.text = "统一堆叠".Translate().WithColor(Orange);
        txtStackStatus.text = BuildStackStatusText();
        txtStackEffect.text = $"{"影响范围".Translate()}：{"全局成长-堆叠影响".Translate()}";
        txtStackCost.text = BuildStackCostText();
        bool canUpgradeStack = StackingManager.CanUpgradeStack();
        btnStackUpgrade.button.interactable = canUpgradeStack;
        btnStackUpgrade.SetText(canUpgradeStack ? "升级堆叠".Translate() : GetStackBlockedText());

        txtTimeTitle.text = "全局时间上限".Translate().WithColor(Orange);
        txtTimeStatus.text = BuildTimeStatusText();
        txtTimeEffect.text = $"{"影响范围".Translate()}：{"全局成长-时间影响".Translate()}";
        txtTimeCost.text = BuildTimeCostText();
        bool canUpgradeTimeLimit = VanillaRecipeManager.CanUpgradeGlobalTimeLimit();
        btnTimeLimitUpgrade.button.interactable = canUpgradeTimeLimit;
        btnTimeLimitUpgrade.SetText(canUpgradeTimeLimit ? "提升上限".Translate() : GetTimeLimitBlockedText());
    }

    private static void UpgradeStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }

        if (!StackingManager.CanUpgradeStack()) {
            UIMessageBox.Show("提示".Translate(), GetStackBlockedText(), "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }

        if (GameMain.sandboxToolsEnabled) {
            StackingManager.UpgradeStack();
            return;
        }

        StackUpgradeCost cost = GetStackUpgradeCost();
        Miscellaneous.ShowQuestion("提示".Translate(),
            $"{ "升级消耗".Translate()}：{BuildCostText(cost)}\n{"来修改此项".Translate()}{"吗？".Translate()}",
            () => {
                if (!TryTakeCost(cost)) {
                    return;
                }

                StackingManager.UpgradeStack();
            });
    }

    private static void UpgradeGlobalTimeLimit() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }

        if (!VanillaRecipeManager.CanUpgradeGlobalTimeLimit()) {
            UIMessageBox.Show("提示".Translate(), GetTimeLimitBlockedText(), "确定".Translate(),
                UIMessageBox.WARNING, null);
            return;
        }

        VanillaRecipeManager.UpgradeGlobalTimeLimit();
    }

    private static string BuildStackStatusText() {
        if (GameMain.history == null || !StackingManager.IsUnlocked) {
            return "需要集装物流系统".Translate();
        }

        int current = StackingManager.CurrentMaxStack;
        int next = System.Math.Min(current + 1, StackingManager.AbsoluteMaxStack);
        return $"{ "当前堆叠".Translate()}：{current}/{StackingManager.AbsoluteMaxStack}\n"
               + $"{ "下一堆叠".Translate()}：{next}";
    }

    private static string BuildStackCostText() {
        if (GameMain.sandboxToolsEnabled) {
            return $"{ "升级消耗".Translate()}：{"无消耗".Translate()}";
        }

        return $"{ "升级消耗".Translate()}：{BuildCostText(GetStackUpgradeCost())}";
    }

    private static string BuildTimeStatusText() {
        if (GameMain.history == null || !StackingManager.IsUnlocked) {
            return "需要集装物流系统".Translate();
        }

        int level = VanillaRecipeManager.GlobalTimeLimitLevel;
        int maxByStack = VanillaRecipeManager.GetMaxTimeLimitLevelByCurrentStack();
        return $"{ "全局时间上限".Translate()}：{level}/{VanillaRecipeManager.MaxTimeLimitLevel}"
               + $"  {VanillaRecipeManager.GlobalTimeLimitRatio:P0}\n"
               + $"{ "当前堆叠".Translate()}：{StackingManager.CurrentMaxStack}  cap {maxByStack}";
    }

    private static string BuildTimeCostText() {
        return GameMain.sandboxToolsEnabled
            ? $"{ "升级消耗".Translate()}：{"无消耗".Translate()}"
            : $"{ "升级消耗".Translate()}：当前临时无消耗，后续接矩阵精华 + 源点";
    }

    private static string GetStackBlockedText() {
        if (GameMain.history == null || !StackingManager.IsUnlocked) {
            return "需要集装物流系统".Translate();
        }

        return "已达上限".Translate();
    }

    private static string GetTimeLimitBlockedText() {
        if (GameMain.history == null || !StackingManager.IsUnlocked) {
            return "需要集装物流系统".Translate();
        }

        if (VanillaRecipeManager.GlobalTimeLimitLevel >= VanillaRecipeManager.MaxTimeLimitLevel) {
            return "已达上限".Translate();
        }

        return "需要更高堆叠上限".Translate();
    }

    private static StackUpgradeCost GetStackUpgradeCost() {
        int nextStack = System.Math.Min(StackingManager.CurrentMaxStack + 1, StackingManager.AbsoluteMaxStack);
        int essenceIndex = System.Math.Min((nextStack - StackingManager.BaseUnlockedMaxStack) / 3,
            MainProgressMatrixIds.Length - 1);
        int essenceId = GetMatrixEssenceItemId(essenceIndex);
        int essenceCount = 200 + (nextStack - StackingManager.BaseUnlockedMaxStack) * 80;
        int memoryItemId = nextStack >= 14 ? IFE纯净源点 : IFE记忆源点;
        int memoryCount = nextStack >= 14 ? 1 + (nextStack - 14) / 2 : 1 + (nextStack - 5) / 3;
        return new(essenceId, essenceCount, memoryItemId, System.Math.Max(1, memoryCount));
    }

    private static string BuildCostText(StackUpgradeCost cost) {
        string essenceName = LDB.items.Select(cost.EssenceId)?.name ?? cost.EssenceId.ToString();
        string memoryName = LDB.items.Select(cost.MemoryItemId)?.name ?? cost.MemoryItemId.ToString();
        return $"{essenceName} x{cost.EssenceCount}，{memoryName} x{cost.MemoryCount}";
    }

    private static bool TryTakeCost(StackUpgradeCost cost) {
        if (GetItemTotalCount(cost.EssenceId) < cost.EssenceCount) {
            return TakeItemWithTip(cost.EssenceId, cost.EssenceCount, out _);
        }

        if (GetItemTotalCount(cost.MemoryItemId) < cost.MemoryCount) {
            return TakeItemWithTip(cost.MemoryItemId, cost.MemoryCount, out _);
        }

        return TakeItemWithTip(cost.EssenceId, cost.EssenceCount, out _)
               && TakeItemWithTip(cost.MemoryItemId, cost.MemoryCount, out _);
    }

    private readonly struct StackUpgradeCost(
        int essenceId,
        int essenceCount,
        int memoryItemId,
        int memoryCount) {
        public int EssenceId { get; } = essenceId;
        public int EssenceCount { get; } = essenceCount;
        public int MemoryItemId { get; } = memoryItemId;
        public int MemoryCount { get; } = memoryCount;
    }
}
