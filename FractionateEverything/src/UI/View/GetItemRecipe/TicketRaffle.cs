using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
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

    private static MyComboBox _annealComboBox;
    private static Text _txtEchoLevel;
    private static Text _txtAnnealCost;
    private static UIButton _btnAnneal;
    private static readonly List<BaseRecipe> _annealRecipes = [];
    private static BaseRecipe _selectedAnnealRecipe;

    private const float LeftW = 185f;
    private const float RightX = 195f;
    private const float CardW = 108f;
    private const float CardH = 148f;
    private const float CardGapX = 10f;
    private const float CardGapY = 10f;
    private const float CardAreaY = 130f;

    public static float[] RecipeValues => [
        (float)System.Math.Sqrt(itemValue[IFE电磁奖券] * 33.614f),
        (float)System.Math.Sqrt(itemValue[IFE能量奖券] * 48.02f),
        (float)System.Math.Sqrt(itemValue[IFE结构奖券] * 68.6f),
        (float)System.Math.Sqrt(itemValue[IFE信息奖券] * 98f),
        (float)System.Math.Sqrt(itemValue[IFE引力奖券] * 140f),
        (float)System.Math.Sqrt(itemValue[IFE宇宙奖券] * 200f),
        (float)System.Math.Sqrt(itemValue[IFE黑雾奖券] * 100f),
    ];

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
        Register("保底进度", "Pity");
        Register("全部翻开", "Reveal All");
        Register("抽1次(普通)", "Draw x1 (Normal)");
        Register("抽10次(普通)", "Draw x10 (Normal)");
        Register("抽1次(精选)", "Draw x1 (Featured)");
        Register("抽10次(精选)", "Draw x10 (Featured)");
        Register("退火", "Anneal");
        Register("回响等级", "Echo Lv");
        Register("退火确认", "Anneal Confirmation");
        Register("退火后配方等级重置为0，获得永久回响加成。", "Recipe level resets to 0 and grants permanent echo bonus.");
        Register("消耗", "Consume");
        Register("对", "on");
        Register("进行退火？", "to anneal?");
        Register("无满级配方", "No Max-Level Recipes");
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
        BuildAnnealSection(wnd);
        BuildSSREffect(wnd);
        SelectPool(GachaPool.PoolIdPermanentRecipe);
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

    private static void BuildAnnealSection(MyConfigWindow wnd) {
        float ay = CardAreaY + CardH * 2 + CardGapY + 70f;
        MyWindow.AddText(RightX, ay, tab, "退火".Translate(), 14);
        _annealComboBox = wnd.AddComboBox(RightX + 60f, ay, tab)
            .WithSize(340f, 0f)
            .WithOnSelChanged(idx => {
                _selectedAnnealRecipe = idx >= 0 && idx < _annealRecipes.Count ? _annealRecipes[idx] : null;
                RefreshAnnealInfo();
            });
        _txtEchoLevel = MyWindow.AddText(RightX + 415f, ay, tab, "", 13);
        _txtAnnealCost = MyWindow.AddText(RightX, ay + 36f, tab, "", 13);
        _btnAnneal = wnd.AddButton(RightX + 700f, ay, 100f, tab, "退火".Translate(), 14, onClick: OnAnnealClick);
    }

    private static void BuildSSREffect(MyConfigWindow wnd) {
        _ssrEffect = GachaSSREffect.Create(wnd.transform);
    }

    private static void SelectPool(int poolId) {
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

    private static void RefreshAnnealInfo() {
        if (_selectedAnnealRecipe == null) {
            if (_txtEchoLevel != null) _txtEchoLevel.text = "";
            if (_txtAnnealCost != null) _txtAnnealCost.text = "";
            if (_btnAnneal != null) _btnAnneal.button.interactable = false;
            return;
        }
        if (_txtEchoLevel != null)
            _txtEchoLevel.text = $"{"回响等级".Translate()}: {_selectedAnnealRecipe.EchoLevel}";
        bool canAnneal = _selectedAnnealRecipe.IsMaxLevel;
        if (_txtAnnealCost != null)
            _txtAnnealCost.text = canAnneal
                ? $"{"消耗".Translate()} {LDB.items.Select(I宇宙矩阵)?.name ?? "宇宙矩阵"} x1"
                : "";
        if (_btnAnneal != null) _btnAnneal.button.interactable = canAnneal;
    }

    private static void RefreshAnnealDropdown() {
        _annealRecipes.Clear();
        foreach (var r in RecipeManager.AllRecipes) {
            if (r.IsMaxLevel) _annealRecipes.Add(r);
        }
        if (_annealComboBox == null) return;
        if (_annealRecipes.Count == 0) {
            _annealComboBox.SetItems("无满级配方".Translate());
            _annealComboBox.SetIndex(0);
            _selectedAnnealRecipe = null;
        } else {
            string[] names = new string[_annealRecipes.Count];
            for (int i = 0; i < _annealRecipes.Count; i++)
                names[i] = _annealRecipes[i].TypeName;
            _annealComboBox.SetItems(names);
            _annealComboBox.SetIndex(0);
            _selectedAnnealRecipe = _annealRecipes[0];
        }
        RefreshAnnealInfo();
    }

    private static void OnAnnealClick() {
        if (_selectedAnnealRecipe == null || !_selectedAnnealRecipe.IsMaxLevel) return;
        string consumeLabel = "消耗".Translate();
        string onLabel = "对".Translate();
        string annealQuestion = "进行退火？".Translate();
        string annealTip = "退火后配方等级重置为0，获得永久回响加成。".Translate();
        UIMessageBox.Show("退火确认".Translate(),
            $"{consumeLabel} {LDB.items.Select(I宇宙矩阵)?.name ?? "宇宙矩阵"} x1 {onLabel} {_selectedAnnealRecipe.TypeName} {annealQuestion}\n{annealTip}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(I宇宙矩阵, 1, out _)) return;
                _selectedAnnealRecipe.Anneal();
                RefreshAnnealDropdown();
            }, null);
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;
        int normalCount = GameMain.mainPlayer?.package.GetItemCount(IFE普通抽卡券) ?? 0;
        int premiumCount = GameMain.mainPlayer?.package.GetItemCount(IFE精选抽卡券) ?? 0;
        if (_txtNormalTicket != null) _txtNormalTicket.text = $"普通券: {normalCount}";
        if (_txtPremiumTicket != null) _txtPremiumTicket.text = $"精选券: {premiumCount}";
        RefreshPityText();
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { totalDraws = 0; }
}
