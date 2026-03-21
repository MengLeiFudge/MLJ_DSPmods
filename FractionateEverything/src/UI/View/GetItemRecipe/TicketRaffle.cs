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
        public readonly Text[] TxtResultLines = new Text[10];
        public UIButton BtnClearResult;
        public UIButton BtnDraw1Normal;
        public UIButton BtnDraw10Normal;
        public UIButton BtnDraw1Premium;
        public UIButton BtnDraw10Premium;
    }

    public static long totalDraws;
    private static readonly List<RaffleTabUi> Uis = [];

    private const float ResultAreaY = 120f;

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
            "Mostly Fragments, with small chance for recipes. Hard pity grants 1 Vanilla Recipe Core.",
            "大部分产出为残片，少量概率产出配方。90抽保底获得1个原版配方核心。");
        Register("原胚奖池", "Proto Pool");
        Register("原胚奖池说明",
            "Only prototype items. Hard pity grants 1 Amplifier Chip.",
            "仅产出各种原胚物品。90抽保底获得1个分馏塔增幅芯片。");
        Register("UP池", "UP Pool");
        Register("UP池说明",
            "Only item rewards. UP group rotates every 1 hour.",
            "仅产出物品奖励。UP组每1小时轮换一次。");
        Register("限定池", "Limited Pool");
        Register("限定池说明",
            "Item-only pool. Hard pity grants 1 Targeted Prototype. Requires Featured Ticket.",
            "仅产出物品。90抽保底获得1个分馏塔定向原胚。需要精选抽卡券。");
        Register("限定池未解锁", "Limited pool is locked.", "限定池暂未解锁。需要先解锁宇宙矩阵。");
        Register("保底进度", "Pity");
        Register("清空结果", "Clear Results");
        Register("抽1次(普通)", "Draw x1 (Normal)");
        Register("抽10次(普通)", "Draw x10 (Normal)");
        Register("抽1次(精选)", "Draw x1 (Featured)");
        Register("抽10次(精选)", "Draw x10 (Featured)");
        Register("抽奖结果", "Raffle Results");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        SyncTotalDrawsFromSharedState();
        Uis.Clear();
        Uis.Add(CreateTab(wnd, trans, "配方抽奖", GachaPool.PoolIdPermanentRecipe));
        Uis.Add(CreateTab(wnd, trans, "原胚抽奖", GachaPool.PoolIdPermanentBuilding));
        Uis.Add(CreateTab(wnd, trans, "UP抽奖", GachaPool.PoolIdUp));
        Uis.Add(CreateTab(wnd, trans, "限定抽奖", GachaPool.PoolIdLimited));
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
            ui.TxtPoolDesc.rectTransform.sizeDelta = new Vector2(960f, 96f);
        }

        ui.TxtNormalTicket = MyWindow.AddText(5f, 86f, ui.Tab, "", 12);
        ui.TxtPremiumTicket = MyWindow.AddText(220f, 86f, ui.Tab, "", 12);

        ui.TxtResultTitle = MyWindow.AddText(5f, ResultAreaY, ui.Tab, "抽奖结果".Translate(), 14);
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.rectTransform.sizeDelta = new Vector2(420f, 24f);
        }
        float y = ResultAreaY + 30f;
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
            var upPool = GachaService.GetPool(GachaPool.PoolIdUp);
            if (upPool != null && upPool.UpItems.Count > 0) {
                int take = System.Math.Min(8, upPool.UpItems.Count);
                string[] names = new string[take];
                for (int i = 0; i < take; i++) {
                    int id = upPool.UpItems[i];
                    names[i] = LDB.items.Select(id)?.name ?? id.ToString();
                }
                return $"{"UP池说明".Translate()}\n当前UP：{string.Join("、", names)}";
            }
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

    private static void StartDraw(RaffleTabUi ui, int ticketId, int count) {
        if (ui.PoolId == GachaPool.PoolIdLimited && !GachaService.LimitedPoolUnlocked) {
            UIRealtimeTip.Popup("限定池未解锁".Translate(), true, 2);
            return;
        }

        var results = GachaService.Draw(ui.PoolId, ticketId, count);
        if (results == null || results.Count == 0) return;

        totalDraws += results.Count;
        SyncTotalDrawsToSharedState();
        RenderResults(ui, results);
        RefreshPityText(ui);
    }

    private static void ClearResults(RaffleTabUi ui) {
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.text = "抽奖结果".Translate();
        }
        for (int i = 0; i < ui.TxtResultLines.Length; i++) {
            if (ui.TxtResultLines[i] != null) ui.TxtResultLines[i].text = "";
        }
    }

    private static void RenderResults(RaffleTabUi ui, List<GachaResult> results) {
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.text = $"{"抽奖结果".Translate()} ({results.Count})";
        }
        int shown = System.Math.Min(results.Count, ui.TxtResultLines.Length);
        for (int i = 0; i < shown; i++) {
            var item = LDB.items.Select(results[i].ItemId);
            string itemName = item != null ? item.name : results[i].ItemId.ToString();
            string rarity = results[i].Rarity switch {
                GachaRarity.S => "S",
                GachaRarity.A => "A",
                GachaRarity.B => "B",
                _ => "C",
            };
            string kind = results[i].IsRecipe
                ? "配方"
                : (itemName.Contains("原胚") ? "建筑" : "物品");
            string upTag = results[i].IsUp ? "[UP] " : "";
            if (ui.TxtResultLines[i] != null) {
                ui.TxtResultLines[i].text = $"[{rarity}] {upTag}[{kind}] {itemName} x1";
            }
        }
        for (int i = shown; i < ui.TxtResultLines.Length; i++) {
            if (ui.TxtResultLines[i] != null) ui.TxtResultLines[i].text = "";
        }
    }

    private static void RefreshPityText(RaffleTabUi ui) {
        if (ui.TxtPityProgress == null) return;
        int pity = GachaManager.PityCount[ui.PoolId];
        ui.TxtPityProgress.text = $"{"保底进度".Translate()}: {pity}/{GachaManager.HardPityThreshold}";
    }

    private static void RefreshUpRotationText(RaffleTabUi ui) {
        if (ui.TxtUpRotationTime == null) return;
        if (ui.PoolId != GachaPool.PoolIdUp) {
            ui.TxtUpRotationTime.text = "";
            return;
        }
        long remaining = GachaManager.UpRotationNextTick - GameMain.gameTick;
        if (remaining <= 0) {
            ui.TxtUpRotationTime.text = "";
            return;
        }
        long totalSec = remaining / 60;
        long h = totalSec / 3600;
        long m = totalSec % 3600 / 60;
        long s = totalSec % 60;
        ui.TxtUpRotationTime.text = $"UP轮换: {h:D2}:{m:D2}:{s:D2}";
    }

    private static void RefreshTabState(RaffleTabUi ui) {
        if (ui.TxtPoolName != null) ui.TxtPoolName.text = GetPoolName(ui.PoolId);
        if (ui.TxtPoolDesc != null) ui.TxtPoolDesc.text = GetPoolDesc(ui.PoolId);

        int normalCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE普通抽卡券));
        int premiumCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE精选抽卡券));
        if (ui.TxtNormalTicket != null) ui.TxtNormalTicket.text = $"普通券: {normalCount}";
        if (ui.TxtPremiumTicket != null) ui.TxtPremiumTicket.text = $"精选券: {premiumCount}";

        bool limitedLocked = ui.PoolId == GachaPool.PoolIdLimited && !GachaService.LimitedPoolUnlocked;
        if (limitedLocked && ui.TxtPoolDesc != null) {
            ui.TxtPoolDesc.text = "限定池未解锁".Translate();
        }

        SetDrawButtonsInteractable(ui, !limitedLocked);
        RefreshPityText(ui);
        RefreshUpRotationText(ui);
    }

    private static void SetDrawButtonsInteractable(RaffleTabUi ui, bool on) {
        if (ui.BtnDraw1Normal?.button != null) ui.BtnDraw1Normal.button.interactable = on;
        if (ui.BtnDraw10Normal?.button != null) ui.BtnDraw10Normal.button.interactable = on;
        if (ui.BtnDraw1Premium?.button != null) ui.BtnDraw1Premium.button.interactable = on;
        if (ui.BtnDraw10Premium?.button != null) ui.BtnDraw10Premium.button.interactable = on;
    }

    public static void UpdateUI() {
        GachaService.LimitedPoolUnlocked = GachaLimitedUnlocks.IsLimitedPoolUnlocked();
        for (int i = 0; i < Uis.Count; i++) {
            var ui = Uis[i];
            if (ui?.Tab == null || !ui.Tab.gameObject.activeSelf) continue;
            RefreshTabState(ui);
        }
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
        SyncTotalDrawsFromSharedState();
    }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() {
        totalDraws = 0;
        SyncTotalDrawsToSharedState();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameMain_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return;
        GachaService.LimitedPoolUnlocked = GachaLimitedUnlocks.IsLimitedPoolUnlocked();
        GachaManager.TickRotationIfNeeded();
    }
}
