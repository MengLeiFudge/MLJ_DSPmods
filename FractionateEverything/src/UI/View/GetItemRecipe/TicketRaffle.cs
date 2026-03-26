using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketRaffle {
    private sealed class RaffleHistoryEntry {
        public int PoolId;
        public int DrawCount;
        public int CountS;
        public int CountA;
        public int CountB;
        public int CountC;
        public int PointsGained;
        public int PityBefore;
        public int PityAfter;
        public int MainUpHitCount;
        public bool HadHardPity;
        public long TotalDrawsAfter;
    }

    private sealed class RaffleTabUi {
        public int PoolId;
        public RectTransform Tab;
        public Text TxtPoolName;
        public Text TxtPoolDesc;
        public Text TxtPityProgress;
        public Text TxtUpRotationTime;
        public Text TxtNormalTicket;
        public Text TxtPremiumTicket;
        public Text TxtResultTitle;
        public Text TxtResultSummary;
        public readonly Text[] TxtResultLines = new Text[10];
        public UIButton BtnClearResult;
        public UIButton BtnDraw1Normal;
        public UIButton BtnDraw10Normal;
        public UIButton BtnDraw1Premium;
        public UIButton BtnDraw10Premium;
        public UIButton BtnGoToStore;
    }

    public static long totalDraws;
    private static readonly List<RaffleTabUi> ActiveUis = [];
    private static readonly List<RaffleHistoryEntry> RecentHistory = [];

    private const float ResultAreaY = 140f;
    private const int CurrentResultLineCount = 5;
    private const int MaxHistoryEntries = 16;

    private static void SyncTotalDrawsFromSharedState() {
        totalDraws = MainWindow.SharedPanelState?.TicketRaffleTotalDraws ?? 0;
    }

    private static void SyncTotalDrawsToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.TicketRaffleTotalDraws = totalDraws;
        }
    }

    public static void AddTranslations() {
        Register("配方抽奖", "Recipe Raffle");
        Register("原胚抽奖", "Proto Raffle");
        Register("UP抽奖", "UP Raffle");
        Register("限定抽奖", "Limited Raffle");
        Register("配方奖池", "Recipe Pool");
        Register("配方奖池说明",
            "C mainly gives fragments; B gives basic/mid recipes; A adds high recipes and FE cores; S gives high recipes or FE cores.",
            "C级主要产出残片；B级产出基础/中阶配方；A级追加高阶配方与分馏配方核心；S级产出高阶配方或分馏配方核心。");
        Register("原胚奖池", "Proto Pool");
        Register("原胚奖池说明",
            "Prototype-only pool. Each rarity maps to a distinct embryo tier and S grants the directional embryo.",
            "仅产出原胚。C/B/A/S 分别对应原胚品质层级，S级固定为分馏塔定向原胚。");
        Register("UP池", "UP Pool");
        Register("UP池说明",
            "C gives side-UP prototypes, B gives the main-UP prototype, A gives side-UP buildings, and S uses 1 Main + 3 Side targets (40%/20%/20%/20%). If Main misses this S roll, next S roll guarantees Main.",
            "C级产出副UP原胚；B级产出主UP原胚；A级产出副UP成品；S级按 1主+3副（40%/20%/20%/20%）结算。若本轮S未中主目标，则下一轮S保主。");
        Register("限定池", "Limited Pool");
        Register("限定池说明",
            "High-tier item pool. C starts with boost chips, B mixes chips and FE recipe cores, A mixes FE and Vanilla recipe cores, and S grants Vanilla Recipe Cores. Requires Featured Ticket.",
            "高阶物资池。C级从增幅芯片起步，B级混入分馏配方核心，A级混入原版配方核心，S级固定原版配方核心。需要精选抽卡券。");
        Register("限定池未解锁", "Limited pool is locked.", "限定池暂未解锁。需要先解锁宇宙矩阵。");
        Register("保底进度", "Pity");
        Register("清空结果", "Clear Results");
        Register("抽1次", "Draw x1");
        Register("抽10次", "Draw x10");
        Register("抽1次(普通)", "Draw x1 (Normal)");
        Register("抽10次(普通)", "Draw x10 (Normal)");
        Register("抽1次(精选)", "Draw x1 (Featured)");
        Register("抽10次(精选)", "Draw x10 (Featured)");
        Register("抽奖结果", "Raffle Results");
        Register("当前池积分", "Current Pool Points");
        Register("结果摘要", "Summary");
        Register("UP轮换已停用", "Single UP group active.", "当前仅有 1 组 UP 目标");
        Register("UP轮换说明", "UP targets rotate every 1 hour.", "UP组每1小时轮换一次。");
        Register("触发硬保底", "Hard Pity");
        Register("命中主UP", "Main UP");
        Register("最近历史", "Recent History");
        Register("当前S率", "Current S Rate");
        Register("距硬保底", "To Hard Pity");
        Register("主UP大保底", "Main UP Guarantee");
        Register("已激活", "Active");
        Register("未激活", "Inactive");
        Register("更多结果已折叠", "More results folded into summary.", "其余结果已折叠到摘要");
        Register("暂无历史", "No history yet.", "暂无历史");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "配方抽奖", GachaPool.PoolIdPermanentRecipe);
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "原胚抽奖", GachaPool.PoolIdPermanentBuilding);
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "UP抽奖", GachaPool.PoolIdUp);
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "限定抽奖", GachaPool.PoolIdLimited);

    private static void CreatePoolUI(MyConfigWindow wnd, RectTransform trans, string tabName, int poolId) {
        SyncTotalDrawsFromSharedState();
        var ui = CreateTab(wnd, trans, tabName, poolId);
        ActiveUis.Add(ui);
    }

    private static RaffleTabUi CreateTab(MyConfigWindow wnd, RectTransform trans, string tabName, int poolId) {
        var ui = new RaffleTabUi {
            PoolId = poolId,
            Tab = wnd.AddTab(trans, tabName)
        };

        ui.TxtPoolName = MyWindow.AddText(5f, 8f, ui.Tab, GetPoolName(poolId), 18);
        ui.TxtPityProgress = MyWindow.AddText(720f, 8f, ui.Tab, "", 13);
        ui.TxtUpRotationTime = MyWindow.AddText(720f, 28f, ui.Tab, "", 11);
        ui.TxtPoolDesc = MyWindow.AddText(5f, 38f, ui.Tab, GetPoolDesc(poolId), 13);
        if (ui.TxtPoolDesc != null) {
            ui.TxtPoolDesc.rectTransform.sizeDelta = new Vector2(960f, 84f);
        }

        ui.TxtNormalTicket = MyWindow.AddText(5f, 110f, ui.Tab, "", 12);
        ui.TxtPremiumTicket = MyWindow.AddText(220f, 110f, ui.Tab, "", 12);

        ui.BtnGoToStore = wnd.AddButton(860f, 8f, 100f, ui.Tab, "前往商店".Translate(), 13,
            onClick: () => {
                MainWindow.NavigateToPage(MainWindowPageRegistry.StoreCategoryName, poolId);
            });

        ui.TxtResultTitle = MyWindow.AddText(5f, ResultAreaY, ui.Tab, "抽奖结果".Translate(), 14);
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.rectTransform.sizeDelta = new Vector2(420f, 24f);
        }
        ui.TxtResultSummary = MyWindow.AddText(5f, ResultAreaY + 24f, ui.Tab, "", 12);
        if (ui.TxtResultSummary != null) {
            ui.TxtResultSummary.rectTransform.sizeDelta = new Vector2(960f, 24f);
        }
        float y = ResultAreaY + 50f;
        for (int i = 0; i < ui.TxtResultLines.Length; i++) {
            ui.TxtResultLines[i] = MyWindow.AddText(5f, y, ui.Tab, "动态刷新", 13);
            if (ui.TxtResultLines[i] != null) {
                ui.TxtResultLines[i].rectTransform.sizeDelta = new Vector2(900f, 22f);
                ui.TxtResultLines[i].text = "";
            }
            y += 24f;
        }

        float btnY = ResultAreaY + 300f;
        ui.BtnClearResult = wnd.AddButton(5f, btnY, 130f, ui.Tab, "清空结果".Translate(), 14,
            onClick: () => ClearResults(ui));

        ui.BtnDraw1Normal = wnd.AddButton(145f, btnY, 150f, ui.Tab, "抽1次(普通)".Translate(), 14,
            onClick: () => StartDraw(ui, IFE普通抽卡券, 1));
        ui.BtnDraw10Normal = wnd.AddButton(305f, btnY, 150f, ui.Tab, "抽10次(普通)".Translate(), 14,
            onClick: () => StartDraw(ui, IFE普通抽卡券, 10));
        ui.BtnDraw1Premium = wnd.AddButton(465f, btnY, 150f, ui.Tab, "抽1次(精选)".Translate(), 14,
            onClick: () => StartDraw(ui, IFE精选抽卡券, 1));
        ui.BtnDraw10Premium = wnd.AddButton(625f, btnY, 150f, ui.Tab, "抽10次(精选)".Translate(), 14,
            onClick: () => StartDraw(ui, IFE精选抽卡券, 10));

        RefreshTabState(ui);
        RenderHistoryOnly(ui);
        return ui;
    }

    private static string GetPoolName(int poolId) {
        return poolId switch {
            GachaPool.PoolIdPermanentRecipe => "配方奖池".Translate(),
            GachaPool.PoolIdPermanentBuilding => "原胚奖池".Translate(),
            GachaPool.PoolIdUp => "UP池".Translate(),
            GachaPool.PoolIdLimited => "限定池".Translate(),
            _ => "配方奖池".Translate(),
        };
    }

    private static string GetPoolDesc(int poolId) {
        if (poolId == GachaPool.PoolIdUp) {
            string mainName = GetItemName(GachaManager.UpMainItemId);
            string sub1Name = GetItemName(GachaManager.UpSubItemIds[0]);
            string sub2Name = GetItemName(GachaManager.UpSubItemIds[1]);
            string sub3Name = GetItemName(GachaManager.UpSubItemIds[2]);
            return $"C：{GetPoolItemNames(poolId, GachaRarity.C)}  B：{GetPoolItemNames(poolId, GachaRarity.B)}\nA：{sub1Name}、{sub2Name}、{sub3Name}\nS主目标(40%)：{mainName}\nS副目标(20%)：{sub1Name}、{sub2Name}、{sub3Name}";
        }
        if (poolId == GachaPool.PoolIdPermanentBuilding || poolId == GachaPool.PoolIdLimited) {
            return $"C：{GetPoolItemNames(poolId, GachaRarity.C)}\nB：{GetPoolItemNames(poolId, GachaRarity.B)}\nA：{GetPoolItemNames(poolId, GachaRarity.A)}\nS：{GetPoolItemNames(poolId, GachaRarity.S)}";
        }
        string key = poolId switch {
            GachaPool.PoolIdPermanentRecipe => "配方奖池说明",
            GachaPool.PoolIdPermanentBuilding => "原胚奖池说明",
            GachaPool.PoolIdUp => "UP池说明",
            GachaPool.PoolIdLimited => "限定池说明",
            _ => "配方奖池说明",
        };
        return key.Translate();
    }

    private static string GetPoolItemNames(int poolId, GachaRarity rarity) {
        GachaPool pool = GachaService.GetPool(poolId);
        if (pool == null) {
            return "-";
        }

        List<int> itemIds = rarity switch {
            GachaRarity.C => pool.PoolC,
            GachaRarity.B => pool.PoolB,
            GachaRarity.A => pool.PoolA,
            GachaRarity.S => pool.PoolS,
            _ => pool.PoolC,
        };

        var names = new List<string>(itemIds.Count);
        for (int i = 0; i < itemIds.Count; i++) {
            string itemName = GetItemName(itemIds[i]);
            if (!names.Contains(itemName)) {
                names.Add(itemName);
            }
        }

        return names.Count == 0 ? "-" : string.Join("、", names);
    }

    private static string GetItemName(int itemId) {
        if (itemId <= 0) {
            return "-";
        }
        return LDB.items.Select(itemId)?.name ?? itemId.ToString();
    }

    private static void StartDraw(RaffleTabUi ui, int ticketId, int count) {
        if (ui.PoolId == GachaPool.PoolIdLimited && !GachaService.IsLimitedPoolUnlocked()) {
            UIRealtimeTip.Popup("限定池未解锁".Translate(), true, 2);
            return;
        }

        int pityBefore = GachaManager.PityCount[ui.PoolId];
        int pointsBefore = GachaManager.GetPoolPoints(ui.PoolId);
        var results = GachaService.Draw(ui.PoolId, ticketId, count);
        if (results == null || results.Count == 0) return;

        totalDraws += results.Count;
        SyncTotalDrawsToSharedState();
        var historyEntry = BuildHistoryEntry(ui.PoolId, results, pityBefore, pointsBefore);
        AddHistoryEntry(historyEntry);
        RenderResults(ui, results, pityBefore, pointsBefore, historyEntry);
        RefreshTabState(ui);
    }

    private static void ClearResults(RaffleTabUi ui) {
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.text = "抽奖结果".Translate();
        }
        if (ui.TxtResultSummary != null) {
            ui.TxtResultSummary.text = "";
        }
        RenderHistoryOnly(ui);
    }

    private static void RenderResults(RaffleTabUi ui, List<GachaResult> results, int pityBefore, int pointsBefore, RaffleHistoryEntry currentEntry) {
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.text = $"{"抽奖结果".Translate()} ({results.Count})";
        }

        int sCount = 0;
        int aCount = 0;
        int bCount = 0;
        int cCount = 0;
        int mainUpHitCount = 0;
        bool hasHardPity = false;
        for (int i = 0; i < results.Count; i++) {
            var result = results[i];
            switch (result.Rarity) {
                case GachaRarity.S:
                    sCount++;
                    break;
                case GachaRarity.A:
                    aCount++;
                    break;
                case GachaRarity.B:
                    bCount++;
                    break;
                default:
                    cCount++;
                    break;
            }

            if (result.HitUpMainTarget) {
                mainUpHitCount++;
            }
            hasHardPity |= result.WasHardPity;
        }

        if (ui.TxtResultSummary != null) {
            int pityAfter = GachaManager.PityCount[ui.PoolId];
            int pointsAfter = GachaManager.GetPoolPoints(ui.PoolId);
            string raritySummary =
                $"{($"S×{sCount}").WithColor(Gold)} / {($"A×{aCount}").WithColor(Purple)} / {($"B×{bCount}").WithColor(Blue)} / {($"C×{cCount}").WithColor(White)}";
            string mainUpSummary = mainUpHitCount > 0 ? $"    主UP命中 x{mainUpHitCount}".WithColor(Orange) : "";
            ui.TxtResultSummary.text =
                $"{"结果摘要".Translate()}：{raritySummary}"
                + $"    积分 +{pointsAfter - pointsBefore}".WithColor(Green)
                + $"    保底 {pityBefore}->{pityAfter}".WithColor(Gray)
                + (hasHardPity ? $"    {"触发硬保底".Translate()}".WithColor(Gold) : "")
                + mainUpSummary;
        }

        var displayIndices = BuildPriorityResultIndices(results, CurrentResultLineCount - 1);
        int lineIndex = 0;
        for (int i = 0; i < displayIndices.Count && lineIndex < CurrentResultLineCount; i++, lineIndex++) {
            var res = results[displayIndices[i]];
            var item = LDB.items.Select(res.ItemId);
            string itemName = item != null ? item.name : res.ItemId.ToString();
            
            string rarityStr = res.Rarity switch {
                GachaRarity.S => "S".WithColor(Gold),
                GachaRarity.A => "A".WithColor(Purple),
                GachaRarity.B => "B".WithColor(Blue),
                _ => "C".WithColor(White),
            };
            
            string pityTag = res.WasHardPity ? "[保底] ".WithColor(Gold) : "";
            string mainUpTag = res.HitUpMainTarget ? "[主UP] ".WithColor(Orange) : "";
            string upTag = res.IsUp ? "[UP] ".WithColor(Orange) : "";
            
            string kind = res.IsRecipe ? "配方" : (item != null && item.CanBuild ? "建筑" : "物品");
            string kindStr = $"[{kind}]".WithColor(Gray);
            
            if (ui.TxtResultLines[lineIndex] != null) {
                ui.TxtResultLines[lineIndex].text = $"[{rarityStr}] {pityTag}{mainUpTag}{upTag}{kindStr} {itemName} x1";
            }
        }

        int foldedCount = results.Count - displayIndices.Count;
        if (foldedCount > 0 && lineIndex < CurrentResultLineCount) {
            ui.TxtResultLines[lineIndex].text = $"... {foldedCount} {"更多结果已折叠".Translate()}".WithColor(Gray);
            lineIndex++;
        }

        for (; lineIndex < CurrentResultLineCount; lineIndex++) {
            if (ui.TxtResultLines[lineIndex] != null) {
                ui.TxtResultLines[lineIndex].text = "";
            }
        }

        RenderRecentHistory(ui, currentEntry);
    }

    private static RaffleHistoryEntry BuildHistoryEntry(int poolId, List<GachaResult> results, int pityBefore, int pointsBefore) {
        var entry = new RaffleHistoryEntry {
            PoolId = poolId,
            DrawCount = results.Count,
            PityBefore = pityBefore,
            PityAfter = GachaManager.PityCount[poolId],
            PointsGained = GachaManager.GetPoolPoints(poolId) - pointsBefore,
            TotalDrawsAfter = totalDraws,
        };

        for (int i = 0; i < results.Count; i++) {
            switch (results[i].Rarity) {
                case GachaRarity.S:
                    entry.CountS++;
                    break;
                case GachaRarity.A:
                    entry.CountA++;
                    break;
                case GachaRarity.B:
                    entry.CountB++;
                    break;
                default:
                    entry.CountC++;
                    break;
            }

            if (results[i].HitUpMainTarget) {
                entry.MainUpHitCount++;
            }
            entry.HadHardPity |= results[i].WasHardPity;
        }

        return entry;
    }

    private static void AddHistoryEntry(RaffleHistoryEntry entry) {
        RecentHistory.Insert(0, entry);
        if (RecentHistory.Count > MaxHistoryEntries) {
            RecentHistory.RemoveRange(MaxHistoryEntries, RecentHistory.Count - MaxHistoryEntries);
        }
    }

    private static void RenderHistoryOnly(RaffleTabUi ui) {
        for (int i = 0; i < CurrentResultLineCount && i < ui.TxtResultLines.Length; i++) {
            if (ui.TxtResultLines[i] != null) {
                ui.TxtResultLines[i].text = "";
            }
        }
        RenderRecentHistory(ui, null);
    }

    private static List<int> BuildPriorityResultIndices(List<GachaResult> results, int maxCount) {
        var indices = new List<int>(maxCount);
        AddPriorityIndices(results, indices, maxCount, result => result.WasHardPity || result.HitUpMainTarget || result.Rarity == GachaRarity.S);
        AddPriorityIndices(results, indices, maxCount, result => result.Rarity == GachaRarity.A || result.IsUp);
        AddPriorityIndices(results, indices, maxCount, _ => true);
        return indices;
    }

    private static void AddPriorityIndices(List<GachaResult> results, List<int> indices, int maxCount, System.Predicate<GachaResult> predicate) {
        for (int i = 0; i < results.Count && indices.Count < maxCount; i++) {
            if (indices.Contains(i)) {
                continue;
            }
            if (predicate(results[i])) {
                indices.Add(i);
            }
        }
    }

    private static void RenderRecentHistory(RaffleTabUi ui, RaffleHistoryEntry currentEntry) {
        int lineIndex = CurrentResultLineCount;
        int historyOrdinal = 1;
        for (int i = 0; i < RecentHistory.Count && lineIndex < ui.TxtResultLines.Length; i++) {
            RaffleHistoryEntry entry = RecentHistory[i];
            if (ReferenceEquals(entry, currentEntry) || entry.PoolId != ui.PoolId) {
                continue;
            }

            string hardPityTag = entry.HadHardPity ? $" {"触发硬保底".Translate()}".WithColor(Gold) : "";
            string mainUpTag = entry.MainUpHitCount > 0 ? $" 主UP×{entry.MainUpHitCount}".WithColor(Orange) : "";
            ui.TxtResultLines[lineIndex].text =
                $"{"最近历史".Translate()} {historyOrdinal}: x{entry.DrawCount}  "
                + $"S{entry.CountS}/A{entry.CountA}/B{entry.CountB}/C{entry.CountC}  "
                + $"+{entry.PointsGained}pts  {entry.PityBefore}->{entry.PityAfter}"
                + hardPityTag
                + mainUpTag;
            lineIndex++;
            historyOrdinal++;
        }

        if (historyOrdinal == 1 && lineIndex < ui.TxtResultLines.Length) {
            ui.TxtResultLines[lineIndex].text = $"{"最近历史".Translate()}: {"暂无历史".Translate()}".WithColor(Gray);
            lineIndex++;
        }

        for (; lineIndex < ui.TxtResultLines.Length; lineIndex++) {
            if (ui.TxtResultLines[lineIndex] != null) {
                ui.TxtResultLines[lineIndex].text = "";
            }
        }
    }

    private static void RefreshPityText(RaffleTabUi ui) {
        if (ui.TxtPityProgress == null) return;
        int pity = GachaManager.PityCount[ui.PoolId];
        ui.TxtPityProgress.text = $"{"保底进度".Translate()}: {pity}/{GachaManager.HardPityThreshold - 1}";
    }

    private static void RefreshUpRotationText(RaffleTabUi ui) {
        if (ui.TxtUpRotationTime == null) return;
        GachaPool pool = GachaService.GetPool(ui.PoolId);
        if (pool == null) {
            ui.TxtUpRotationTime.text = "";
            return;
        }

        int pity = GachaManager.PityCount[ui.PoolId];
        int remainingToHardPity = GachaManager.HardPityThreshold - pity;
        float currentSRate = GachaManager.GetCurrentSRate(ui.PoolId, pool.RateS);
        string text =
            $"{ "当前S率".Translate() }: {currentSRate * 100f:F1}%"
            + $"    { "距硬保底".Translate() }: {remainingToHardPity}";

        if (ui.PoolId == GachaPool.PoolIdUp) {
            string guaranteeState = GachaManager.IsCurrentUpGuaranteeActive()
                ? "已激活".Translate().WithColor(Orange)
                : "未激活".Translate().WithColor(Gray);
            text += $"    { "主UP大保底".Translate() }: {guaranteeState}";

            if (GachaService.UpGroupCount > 1) {
                long remaining = GachaManager.UpRotationNextTick - GameMain.gameTick;
                if (remaining > 0) {
                    long totalSec = remaining / 60;
                    long h = totalSec / 3600;
                    long m = totalSec % 3600 / 60;
                    long s = totalSec % 60;
                    text += $"    UP轮换: {h:D2}:{m:D2}:{s:D2}";
                }
            }
        }

        ui.TxtUpRotationTime.text = text;
    }

    private static void RefreshTabState(RaffleTabUi ui) {
        if (ui.TxtPoolName != null) ui.TxtPoolName.text = GetPoolName(ui.PoolId);
        if (ui.TxtPoolDesc != null) ui.TxtPoolDesc.text = GetPoolDesc(ui.PoolId);

        bool isLimited = ui.PoolId == GachaPool.PoolIdLimited;
        int normalCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE普通抽卡券));
        int premiumCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE精选抽卡券));
        int poolPoints = GachaManager.GetPoolPoints(ui.PoolId);

        if (ui.TxtNormalTicket != null) {
            ui.TxtNormalTicket.text = $"普通券: {normalCount}";
        }
        if (ui.TxtPremiumTicket != null) {
            ui.TxtPremiumTicket.text = $"精选券: {premiumCount}    {"当前池积分".Translate()}: {poolPoints}";
        }

        bool limitedLocked = isLimited && !GachaService.IsLimitedPoolUnlocked();
        if (limitedLocked && ui.TxtPoolDesc != null) {
            ui.TxtPoolDesc.text = "限定池未解锁".Translate();
        }

        SetDrawButtonsInteractable(ui, !limitedLocked);
        RefreshPityText(ui);
        RefreshUpRotationText(ui);
    }

    private static void SetDrawButtonsInteractable(RaffleTabUi ui, bool on) {
        if (ui.BtnDraw1Normal?.button != null) ui.BtnDraw1Normal.button.interactable = on && GachaPool.CanUseTicket(ui.PoolId, IFE普通抽卡券);
        if (ui.BtnDraw10Normal?.button != null) ui.BtnDraw10Normal.button.interactable = on && GachaPool.CanUseTicket(ui.PoolId, IFE普通抽卡券);
        if (ui.BtnDraw1Premium?.button != null) ui.BtnDraw1Premium.button.interactable = on && GachaPool.CanUseTicket(ui.PoolId, IFE精选抽卡券);
        if (ui.BtnDraw10Premium?.button != null) ui.BtnDraw10Premium.button.interactable = on && GachaPool.CanUseTicket(ui.PoolId, IFE精选抽卡券);
    }

    public static void UpdateUI() {
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.None) return;
        
        for (int i = ActiveUis.Count - 1; i >= 0; i--) {
            var ui = ActiveUis[i];
            if (ui?.Tab == null) {
                ActiveUis.RemoveAt(i);
                continue;
            }
            if (!ui.Tab.gameObject.activeInHierarchy) continue;
            RefreshTabState(ui);
        }
    }

    public static void Import(BinaryReader r) {
        totalDraws = 0;
        RecentHistory.Clear();
        r.ReadBlocks(
            ("TotalDraws", br => totalDraws = br.ReadInt64()),
            ("RecentHistory", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    RecentHistory.Add(new RaffleHistoryEntry {
                        PoolId = br.ReadInt32(),
                        DrawCount = br.ReadInt32(),
                        CountS = br.ReadInt32(),
                        CountA = br.ReadInt32(),
                        CountB = br.ReadInt32(),
                        CountC = br.ReadInt32(),
                        PointsGained = br.ReadInt32(),
                        PityBefore = br.ReadInt32(),
                        PityAfter = br.ReadInt32(),
                        MainUpHitCount = br.ReadInt32(),
                        HadHardPity = br.ReadBoolean(),
                        TotalDrawsAfter = br.ReadInt64(),
                    });
                }
            })
        );
        SyncTotalDrawsToSharedState();
    }
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalDraws", bw => bw.Write(totalDraws)),
            ("RecentHistory", bw => {
                int count = System.Math.Min(RecentHistory.Count, MaxHistoryEntries);
                bw.Write(count);
                for (int i = 0; i < count; i++) {
                    RaffleHistoryEntry entry = RecentHistory[i];
                    bw.Write(entry.PoolId);
                    bw.Write(entry.DrawCount);
                    bw.Write(entry.CountS);
                    bw.Write(entry.CountA);
                    bw.Write(entry.CountB);
                    bw.Write(entry.CountC);
                    bw.Write(entry.PointsGained);
                    bw.Write(entry.PityBefore);
                    bw.Write(entry.PityAfter);
                    bw.Write(entry.MainUpHitCount);
                    bw.Write(entry.HadHardPity);
                    bw.Write(entry.TotalDrawsAfter);
                }
            })
        );
    }
    public static void IntoOtherSave() {
        totalDraws = 0;
        RecentHistory.Clear();
        SyncTotalDrawsToSharedState();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameMain_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return;
        GachaManager.TickRotationIfNeeded();
    }
}
