using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketRaffle {
    public static long totalDraws;
    private static RectTransform tab;

    private static UIButton[] _poolButtons = new UIButton[4];
    private static int _selectedPoolId = GachaPool.PoolIdPermanentRecipe;

    private static Text _txtPoolName;
    private static Text _txtPoolDesc;
    private static Text _txtPityProgress;
    private static Text _txtUpRotationTime;
    private static Text _txtNormalTicket;
    private static Text _txtPremiumTicket;

    private static RectTransform _cardArea;
    private static GachaCard[] _cards = new GachaCard[10];
    private static GachaSSREffect _ssrEffect;

    private static List<GachaResult> _pendingResults = [];
    private static bool _isDrawing = false;

    private static UIButton _btnRevealAll;
    private static UIButton _btnDraw1Normal;
    private static UIButton _btnDraw10Normal;
    private static UIButton _btnDraw1Premium;
    private static UIButton _btnDraw10Premium;


    private const float LeftW = 185f;
    private const float RightX = 195f;
    private const float CardW = 108f;
    private const float CardH = 148f;
    private const float CardGapX = 10f;
    private const float CardGapY = 10f;
    private const float CardAreaY = 130f;

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");
        Register("配方奖池", "Recipe Pool");
        Register("配方奖池说明",
            "Draw fractionate recipes and Recipe Cores.\nHigher tier tickets can yield lower tier recipes.",
            "可以抽取各种分馏配方，以及分馏配方核心。\n高等级奖券也可以抽到低层次科技的相关配方。");
        Register("原胚奖池", "Proto Pool");
        Register("原胚奖池说明",
            "Draw fractionator prototypes and Amplifier Chips.",
            "可以抽取各种分馏塔原胚，以及分馏塔增幅芯片。");
        Register("UP池", "UP Pool");
        Register("UP池说明",
            "Current UP items have doubled drop rate. Big pity: guaranteed UP after 2 consecutive non-UP S draws.",
            "当期UP物品概率翻倍。大保底：连续2次S未出UP，第3次必出UP。");
        Register("限定池", "Limited Pool");
        Register("限定池说明",
            "Draw high-star Runes and special recipes. Requires Featured Ticket.",
            "可以抽取高星符文和特殊配方。需要精选抽卡券。");
        Register("限定池未解锁", "Limited pool is locked.", "限定池暂未解锁。需要先解锁宇宙矩阵。");
        Register("保底进度", "Pity");
        Register("全部翻开", "Reveal All");
        Register("抽1次(普通)", "Draw x1 (Normal)");
        Register("抽10次(普通)", "Draw x10 (Normal)");
        Register("抽1次(精选)", "Draw x1 (Featured)");
        Register("抽10次(精选)", "Draw x10 (Featured)");
        Register("消耗", "Consume");
        Register("抽奖结果", "Raffle results");
        Register("获得了以下物品", "Obtained the following items");
        Register("谢谢惠顾喵", "Thank you meow");
        Register("已解锁", "unlocked");
        Register("已转为同名回响提示",
            "has been converted to a homonym echo (currently holding {0} homonym echoes)",
            "已转为同名回响（当前持有 {0} 同名回响）");
        Register("所有奖励已存储至分馏数据中心。", "All rewards have been stored in the fractionation data centre.");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "奖券抽奖");
        BuildLeftPanel(wnd);
        BuildRightTop();
        BuildCardArea();
        BuildActionButtons(wnd);
        BuildSSREffect(wnd);
        RefreshPoolButtonTexts();
        SelectPool(GachaPool.PoolIdPermanentRecipe);
    }

    private static void RefreshPoolButtonTexts() {
        if (_poolButtons[0] != null) _poolButtons[0].SetText("配方奖池".Translate());
        if (_poolButtons[1] != null) _poolButtons[1].SetText("原胚奖池".Translate());
        if (_poolButtons[2] != null) _poolButtons[2].SetText("UP池".Translate());
        string limitedText = GachaService.LimitedPoolUnlocked ? "限定池".Translate() : $"{"限定池".Translate()} 🔒";
        if (_poolButtons[3] != null) _poolButtons[3].SetText(limitedText);
    }

    private static void BuildLeftPanel(MyConfigWindow wnd) {
        MyWindow.AddText(5f, 8f, tab, "卡池", 13);
        string[] poolNames = ["配方奖池", "原胚奖池", "UP池", "限定池"];
        int[] poolIds = [
            GachaPool.PoolIdPermanentRecipe,
            GachaPool.PoolIdPermanentBuilding,
            GachaPool.PoolIdUp,
            GachaPool.PoolIdLimited
        ];
        for (int i = 0; i < 4; i++) {
            int pid = poolIds[i];
            float btnY = 30f + i * 46f;
            _poolButtons[i] = wnd.AddButton(5f, btnY, LeftW - 10f, tab, poolNames[i].Translate(), 14,
                onClick: () => SelectPool(pid));
        }
        MyWindow.AddText(5f, 225f, tab, "──────────", 10);
        _txtNormalTicket = MyWindow.AddText(5f, 245f, tab, "普通券: 0", 12);
        _txtPremiumTicket = MyWindow.AddText(5f, 268f, tab, "精选券: 0", 12);
    }

    private static void BuildRightTop() {
        _txtPoolName = MyWindow.AddText(RightX, 8f, tab, "配方奖池", 18);
        _txtPityProgress = MyWindow.AddText(RightX + 700f, 8f, tab, "保底: 0/90", 13);
        _txtUpRotationTime = MyWindow.AddText(RightX + 700f, 28f, tab, "", 11);
        _txtPoolDesc = MyWindow.AddText(RightX, 38f, tab, "配方奖池说明", 13);
        if (_txtPoolDesc != null) {
            _txtPoolDesc.rectTransform.sizeDelta = new Vector2(960f, 80f);
        }
    }

    private static void BuildCardArea() {
        var areaGo = new GameObject("CardArea");
        _cardArea = areaGo.AddComponent<RectTransform>();
        _cardArea.SetParent(tab, false);
        _cardArea.anchorMin = new Vector2(0, 1);
        _cardArea.anchorMax = new Vector2(0, 1);
        _cardArea.pivot = new Vector2(0, 1);
        _cardArea.anchoredPosition = new Vector2(RightX, -CardAreaY);
        _cardArea.sizeDelta = new Vector2(960f, CardH * 2 + CardGapY);
        for (int i = 0; i < 10; i++) {
            int col = i % 5;
            int row = i / 5;
            float cx = col * (CardW + CardGapX);
            float cy = row * (CardH + CardGapY);
            _cards[i] = GachaCard.Create(_cardArea, cx, cy, CardW, CardH);
            _cards[i].gameObject.SetActive(false);
        }
    }

    private static void BuildActionButtons(MyConfigWindow wnd) {
        float btnY = CardAreaY + CardH * 2 + CardGapY + 18f;
        _btnRevealAll = wnd.AddButton(RightX, btnY, 130f, tab, "全部翻开".Translate(), 14, onClick: RevealAll);
        _btnDraw1Normal = wnd.AddButton(RightX + 140f, btnY, 150f, tab, "抽1次(普通)".Translate(), 14,
            onClick: () => StartDraw(IFE普通抽卡券, 1));
        _btnDraw10Normal = wnd.AddButton(RightX + 300f, btnY, 150f, tab, "抽10次(普通)".Translate(), 14,
            onClick: () => StartDraw(IFE普通抽卡券, 10));
        _btnDraw1Premium = wnd.AddButton(RightX + 460f, btnY, 150f, tab, "抽1次(精选)".Translate(), 14,
            onClick: () => StartDraw(IFE精选抽卡券, 1));
        _btnDraw10Premium = wnd.AddButton(RightX + 620f, btnY, 150f, tab, "抽10次(精选)".Translate(), 14,
            onClick: () => StartDraw(IFE精选抽卡券, 10));
    }

    private static void BuildSSREffect(MyConfigWindow wnd) {
        _ssrEffect = GachaSSREffect.Create(wnd.transform);
    }

    private static void SelectPool(int poolId) {
        if (poolId == GachaPool.PoolIdLimited && !GachaService.LimitedPoolUnlocked) {
            UIRealtimeTip.Popup("限定池未解锁".Translate(), true, 2);
            return;
        }
        _selectedPoolId = poolId;
        for (int i = 0; i < _poolButtons.Length; i++) {
            if (_poolButtons[i] != null)
                _poolButtons[i].highlighted = (i == poolId);
        }
        string poolName = poolId switch {
            GachaPool.PoolIdPermanentRecipe => "配方奖池".Translate(),
            GachaPool.PoolIdPermanentBuilding => "原胚奖池".Translate(),
            GachaPool.PoolIdUp => "UP池".Translate(),
            GachaPool.PoolIdLimited => "限定池".Translate(),
            _ => "配方奖池".Translate(),
        };
        string poolDescKey = poolId switch {
            GachaPool.PoolIdPermanentRecipe => "配方奖池说明",
            GachaPool.PoolIdPermanentBuilding => "原胚奖池说明",
            GachaPool.PoolIdUp => "UP池说明",
            GachaPool.PoolIdLimited => "限定池说明",
            _ => "配方奖池说明",
        };
        if (_txtPoolName != null) _txtPoolName.text = poolName;
        if (_txtPoolDesc != null) _txtPoolDesc.text = poolDescKey.Translate();
        RefreshPityText();
        ResetCards();
    }

    private static void RefreshPityText() {
        if (_txtPityProgress == null) return;
        int pity = GachaManager.PityCount[_selectedPoolId];
        _txtPityProgress.text = $"{"保底进度".Translate()}: {pity}/{GachaManager.HardPityThreshold}";
    }

    private static void StartDraw(int ticketId, int count) {
        if (_isDrawing) return;
        var results = GachaService.Draw(_selectedPoolId, ticketId, count);
        if (results == null || results.Count == 0) return;
        _isDrawing = true;
        _pendingResults = results;
        totalDraws += results.Count;
        ResetCards();
        int shown = System.Math.Min(results.Count, 10);
        for (int i = 0; i < shown; i++) {
            _cards[i].SetResult(results[i]);
            _cards[i].gameObject.SetActive(true);
            _cards[i].OnRevealed = OnCardRevealed;
        }
        SetDrawButtonsInteractable(false);
        if (_btnRevealAll != null) _btnRevealAll.button.interactable = true;
        RefreshPityText();
    }

    private static void OnCardRevealed(GachaCard card) {
        if (card.Result.Rarity == GachaRarity.S && _ssrEffect != null) {
            _ssrEffect.Play(card.Result, () => {
                _isDrawing = false;
                SetDrawButtonsInteractable(true);
            });
        } else {
            _isDrawing = false;
            SetDrawButtonsInteractable(true);
        }
    }

    private static void RevealAll() {
        if (_pendingResults == null || _pendingResults.Count == 0) return;
        int shown = System.Math.Min(_pendingResults.Count, 10);
        bool hasSsr = false;
        GachaResult ssrResult = default;
        for (int i = 0; i < shown; i++) {
            if (!_cards[i].IsRevealed) {
                _cards[i].RevealImmediate();
                if (_cards[i].Result.Rarity == GachaRarity.S && !hasSsr) {
                    hasSsr = true;
                    ssrResult = _cards[i].Result;
                }
            }
        }
        if (hasSsr && _ssrEffect != null) {
            _ssrEffect.Play(ssrResult, () => {
                _isDrawing = false;
                SetDrawButtonsInteractable(true);
            });
        } else {
            _isDrawing = false;
            SetDrawButtonsInteractable(true);
        }
        if (_btnRevealAll != null) _btnRevealAll.button.interactable = false;
    }

    private static void ResetCards() {
        for (int i = 0; i < 10; i++) {
            _cards[i]?.ResetToBack();
            if (_cards[i] != null) _cards[i].gameObject.SetActive(false);
        }
        _pendingResults = [];
        _isDrawing = false;
        if (_btnRevealAll != null) _btnRevealAll.button.interactable = false;
        SetDrawButtonsInteractable(true);
    }

    private static void SetDrawButtonsInteractable(bool on) {
        if (_btnDraw1Normal != null) _btnDraw1Normal.button.interactable = on;
        if (_btnDraw10Normal != null) _btnDraw10Normal.button.interactable = on;
        if (_btnDraw1Premium != null) _btnDraw1Premium.button.interactable = on;
        if (_btnDraw10Premium != null) _btnDraw10Premium.button.interactable = on;
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;
        GachaService.LimitedPoolUnlocked = GachaLimitedUnlocks.IsLimitedPoolUnlocked();
        RefreshPoolButtonTexts();
        int normalCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE普通抽卡券));
        int premiumCount = (int)System.Math.Min(int.MaxValue, GetItemTotalCount(IFE精选抽卡券));
        if (_txtNormalTicket != null) _txtNormalTicket.text = $"普通券: {normalCount}";
        if (_txtPremiumTicket != null) _txtPremiumTicket.text = $"精选券: {premiumCount}";
        RefreshPityText();
        RefreshUpRotationText();
    }

    private static void RefreshUpRotationText() {
        if (_txtUpRotationTime == null) return;
        if (_selectedPoolId != GachaPool.PoolIdUp) {
            _txtUpRotationTime.text = "";
            return;
        }
        long remaining = GachaManager.UpRotationNextTick - GameMain.gameTick;
        if (remaining <= 0) {
            _txtUpRotationTime.text = "";
            return;
        }
        long totalSec = remaining / 60;
        long h = totalSec / 3600;
        long m = totalSec % 3600 / 60;
        long s = totalSec % 60;
        _txtUpRotationTime.text = $"UP轮换: {h:D2}:{m:D2}:{s:D2}";
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { totalDraws = 0; }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameMain_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return;
        GachaService.LimitedPoolUnlocked = GachaLimitedUnlocks.IsLimitedPoolUnlocked();
        GachaManager.TickRotationIfNeeded();
    }
}
