using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Recipe;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public class GachaWindow : MyWindow {
    public static GachaWindow Instance { get; private set; }
    
    private RectTransform _leftPanel;
    private RectTransform _rightPanel;
    
    private Text _poolNameText;
    private Text _normalTicketText;
    private Text _premiumTicketText;
    private Text _annealEchoLevelText;
    private Text _annealConsumeText;
    
    private RectTransform[] _cardSlots = new RectTransform[10];
    private GachaCard[] _cards = new GachaCard[10];
    private GachaSSREffect _ssrEffect;
    private List<GachaResult> _pendingResults = [];
    private int _revealIndex = 0;
    private bool _isDrawing = false;
    private int _selectedPoolId = GachaPool.PoolIdPermanentRecipe;
    private readonly List<BaseRecipe> _annealRecipes = [];
    
    private UIButton _btnRevealAll;
    private UIButton _btnDraw1;
    private UIButton _btnDraw10;
    private UIButton _btnAnneal;
    private MyComboBox _annealRecipeComboBox;
    private BaseRecipe _selectedAnnealRecipe;
    
    public static void CreateInstance() {
        Instance = MyWindow.Create<GachaWindow>("gacha-window", "分馏抽卡");
    }

    public static void OpenForPool(int poolId) {
        if (Instance == null) {
            CreateInstance();
        }
        Instance?.SetSelectedPool(poolId);
        Instance?.Open();
    }

    public static void AddTranslations() {
        Register("分馏抽卡", "Fractionation Gacha");
        Register("常驻池", "Permanent Pool");
        Register("UP池", "UP Pool");
        Register("限定池", "Limited Pool");
        Register("全部翻开", "Reveal All");
        Register("保底进度", "Pity Progress");
        Register("退火", "Anneal");
        Register("回响等级", "Echo Level");
        Register("退火确认", "Anneal Confirmation");
        Register("退火后配方等级重置为0，获得永久回响加成。", "Recipe level resets to 0 and grants permanent echo bonus.");
        Register("消耗", "Consume");
        Register("对", "on");
        Register("进行退火？", "to anneal?");
        Register("无满级配方", "No Max-Level Recipes");
    }

    public override void _OnCreate() {
        MaxY = 700f;
        var trans = GetComponent<RectTransform>();
        trans.sizeDelta = new Vector2(1100f, 700f + TitleHeight + Margin);
        
        _leftPanel = new GameObject("left-panel").AddComponent<RectTransform>();
        _leftPanel.SetParent(trans, false);
        _leftPanel.anchorMin = new Vector2(0, 1);
        _leftPanel.anchorMax = new Vector2(0, 1);
        _leftPanel.pivot = new Vector2(0, 1);
        _leftPanel.anchoredPosition = new Vector2(Margin, -TitleHeight - Margin);
        _leftPanel.sizeDelta = new Vector2(220f, 700f);
        
        _rightPanel = new GameObject("right-panel").AddComponent<RectTransform>();
        _rightPanel.SetParent(trans, false);
        _rightPanel.anchorMin = new Vector2(0, 1);
        _rightPanel.anchorMax = new Vector2(0, 1);
        _rightPanel.pivot = new Vector2(0, 1);
        _rightPanel.anchoredPosition = new Vector2(Margin + 220f + Spacing, -TitleHeight - Margin);
        _rightPanel.sizeDelta = new Vector2(1100f - 220f - Spacing - Margin * 2, 700f);
        
        var topBar = new GameObject("top-bar").AddComponent<RectTransform>();
        topBar.SetParent(_rightPanel, false);
        topBar.anchorMin = new Vector2(0, 1);
        topBar.anchorMax = new Vector2(1, 1);
        topBar.pivot = new Vector2(0, 1);
        topBar.anchoredPosition = new Vector2(0, 0);
        topBar.sizeDelta = new Vector2(0, 40f);
        
        _poolNameText = AddText(0, -20f, topBar, "卡池名称", 16, "pool-name");
        _poolNameText.alignment = TextAnchor.MiddleLeft;
        _poolNameText.rectTransform.sizeDelta = new Vector2(300f, 40f);
        
        _normalTicketText = AddText(topBar.rect.width - 200f, -20f, topBar, "普通券×0", 14, "normal-ticket");
        _normalTicketText.alignment = TextAnchor.MiddleRight;
        _normalTicketText.rectTransform.anchorMin = new Vector2(1, 0.5f);
        _normalTicketText.rectTransform.anchorMax = new Vector2(1, 0.5f);
        _normalTicketText.rectTransform.anchoredPosition = new Vector2(-150f, 0);
        
        _premiumTicketText = AddText(topBar.rect.width - 100f, -20f, topBar, "精选券×0", 14, "premium-ticket");
        _premiumTicketText.alignment = TextAnchor.MiddleRight;
        _premiumTicketText.rectTransform.anchorMin = new Vector2(1, 0.5f);
        _premiumTicketText.rectTransform.anchorMax = new Vector2(1, 0.5f);
        _premiumTicketText.rectTransform.anchoredPosition = new Vector2(-20f, 0);
        
        var cardArea = new GameObject("card-area").AddComponent<RectTransform>();
        cardArea.SetParent(_rightPanel, false);
        cardArea.anchorMin = new Vector2(0, 1);
        cardArea.anchorMax = new Vector2(1, 1);
        cardArea.pivot = new Vector2(0, 1);
        cardArea.anchoredPosition = new Vector2(0, -40f);
        cardArea.sizeDelta = new Vector2(0, 400f);
        
        float cardWidth = 120f;
        float cardHeight = 160f;
        float startX = 50f;
        float startY = -20f;
        float gapX = 20f;
        float gapY = 20f;
        
        for (int i = 0; i < 10; i++) {
            int row = i / 5;
            int col = i % 5;
            
            var slot = new GameObject($"card-slot-{i}").AddComponent<RectTransform>();
            slot.SetParent(cardArea, false);
            slot.anchorMin = new Vector2(0, 1);
            slot.anchorMax = new Vector2(0, 1);
            slot.pivot = new Vector2(0, 1);
            slot.anchoredPosition = new Vector2(startX + col * (cardWidth + gapX), startY - row * (cardHeight + gapY));
            slot.sizeDelta = new Vector2(cardWidth, cardHeight);
            
            _cardSlots[i] = slot;
            _cards[i] = GachaCard.Create(slot, 0, 0, cardWidth, cardHeight);
            _cards[i].gameObject.SetActive(false);
        }
        
        _ssrEffect = GachaSSREffect.Create(trans);
        
        var bottomBar = new GameObject("bottom-bar").AddComponent<RectTransform>();
        bottomBar.SetParent(_rightPanel, false);
        bottomBar.anchorMin = new Vector2(0, 1);
        bottomBar.anchorMax = new Vector2(1, 1);
        bottomBar.pivot = new Vector2(0, 1);
        bottomBar.anchoredPosition = new Vector2(0, -440f);
        bottomBar.sizeDelta = new Vector2(0, 60f);
        
        _btnRevealAll = AddButton(50f, -30f, 120f, bottomBar, "全部翻开", 16, "btn-reveal-all");
        _btnDraw1 = AddButton(300f, -30f, 120f, bottomBar, "抽1次", 16, "btn-draw-1");
        _btnDraw10 = AddButton(500f, -30f, 120f, bottomBar, "抽10次", 16, "btn-draw-10");
        
        _btnRevealAll.onClick += _ => RevealAll();
        _btnDraw1.onClick += _ => OnDrawClick(1);
        _btnDraw10.onClick += _ => OnDrawClick(10);

        var annealPanel = new GameObject("anneal-panel").AddComponent<RectTransform>();
        annealPanel.SetParent(_rightPanel, false);
        annealPanel.anchorMin = new Vector2(0, 1);
        annealPanel.anchorMax = new Vector2(1, 1);
        annealPanel.pivot = new Vector2(0, 1);
        annealPanel.anchoredPosition = new Vector2(0, -505f);
        annealPanel.sizeDelta = new Vector2(0, 170f);

        AddText(20f, -18f, annealPanel, "退火", 16, "anneal-title");
        _annealRecipeComboBox = AddComboBox(20f, -52f, annealPanel, 14)
            .WithSize(350f, 0f)
            .WithOnSelChanged(OnAnnealSelectionChanged);
        _annealEchoLevelText = AddText(20f, -88f, annealPanel, "回响等级: 0", 14, "anneal-echo-level");
        _annealConsumeText = AddText(20f, -114f, annealPanel, "消耗 宇宙矩阵 ×1", 14, "anneal-consume");
        _btnAnneal = AddButton(20f, -146f, 120f, annealPanel, "退火", 16, "btn-anneal", OnAnnealClick);
    }

    private int GetTicketIdForPool(int poolId) {
        var pool = GachaService.GetPool(poolId);
        return pool != null && pool.RequiresPremiumTicket ? IFE精选抽卡券 : IFE普通抽卡券;
    }

    private void SetSelectedPool(int poolId) {
        if (GachaService.GetPool(poolId) == null) {
            return;
        }
        _selectedPoolId = poolId;
        RefreshPoolHeader();
    }

    private void RefreshPoolHeader() {
        if (_poolNameText == null) {
            return;
        }
        GachaPool pool = GachaService.GetPool(_selectedPoolId);
        _poolNameText.text = pool?.NameKey.Translate() ?? "常驻配方池".Translate();
    }

    private void OnDrawClick(int count) {
        if (_isDrawing) return;
        int poolId = _selectedPoolId;
        int ticketId = GetTicketIdForPool(poolId);
        var results = GachaService.Draw(poolId, ticketId, count);
        if (results.Count == 0) return;
        
        _isDrawing = true;
        _pendingResults = results;
        _revealIndex = 0;
        
        foreach (var card in _cards) {
            card.gameObject.SetActive(false);
        }
        for (int i = 0; i < results.Count && i < _cards.Length; i++) {
            _cards[i].gameObject.SetActive(true);
            _cards[i].SetResult(results[i]);
        }
        
        OnRevealNextCard();
    }

    private void OnRevealNextCard() {
        if (_revealIndex >= _pendingResults.Count) {
            _isDrawing = false;
            return;
        }
        var card = _cards[_revealIndex];
        card.OnRevealed = OnCardRevealed;
        card.Reveal();
    }

    private void OnCardRevealed(GachaCard card) {
        if (card.Result.Rarity == GachaRarity.S) {
            _ssrEffect.OnComplete = () => {
                _revealIndex++;
                OnRevealNextCard();
            };
            _ssrEffect.Play(card.Result);
        } else {
            _revealIndex++;
            OnRevealNextCard();
        }
    }

    private void RevealAll() {
        foreach (var card in _cards) {
            if (!card.IsRevealed && card.gameObject.activeSelf) {
                card.RevealImmediate();
            }
        }
        _isDrawing = false;
    }

    private void OnAnnealSelectionChanged(int index) {
        if (index < 0 || index >= _annealRecipes.Count) {
            _selectedAnnealRecipe = null;
        } else {
            _selectedAnnealRecipe = _annealRecipes[index];
        }
        UpdateAnnealSelectionUI();
    }

    private void UpdateAnnealSelectionUI() {
        string echoLabel = "回响等级".Translate();
        string consumeLabel = "消耗".Translate();
        if (_annealEchoLevelText != null) {
            int echoLevel = _selectedAnnealRecipe?.EchoLevel ?? 0;
            _annealEchoLevelText.text = $"{echoLabel}: {echoLevel}";
        }
        if (_annealConsumeText != null) {
            _annealConsumeText.text = $"{consumeLabel} {LDB.items.Select(I宇宙矩阵).name} ×1";
        }
        if (_btnAnneal != null) {
            _btnAnneal.button.interactable = _selectedAnnealRecipe != null && _selectedAnnealRecipe.IsMaxLevel;
        }
    }

    private void RefreshAnnealUI() {
        if (_annealRecipeComboBox == null) return;

        BaseRecipe previousSelection = _selectedAnnealRecipe;
        _annealRecipes.Clear();
        List<string> recipeNames = [];

        foreach (ERecipe recipeType in Enum.GetValues(typeof(ERecipe))) {
            foreach (BaseRecipe recipe in RecipeManager.GetRecipesByType(recipeType)) {
                if (!recipe.IsMaxLevel) continue;
                _annealRecipes.Add(recipe);
                recipeNames.Add(recipe.TypeName);
            }
        }

        int selectedIndex = 0;
        if (recipeNames.Count == 0) {
            recipeNames.Add("无满级配方".Translate());
            _selectedAnnealRecipe = null;
            _annealRecipeComboBox.SetItems(recipeNames.ToArray());
            _annealRecipeComboBox.SetIndex(0);
            UpdateAnnealSelectionUI();
            return;
        }

        if (previousSelection != null) {
            selectedIndex = _annealRecipes.IndexOf(previousSelection);
        }
        if (selectedIndex < 0) selectedIndex = 0;

        _selectedAnnealRecipe = _annealRecipes[selectedIndex];
        _annealRecipeComboBox.SetItems(recipeNames.ToArray());
        _annealRecipeComboBox.SetIndex(selectedIndex);
        UpdateAnnealSelectionUI();
    }

    private void OnAnnealClick() {
        if (_selectedAnnealRecipe == null) return;
        if (!_selectedAnnealRecipe.IsMaxLevel) return;

        string consumeLabel = "消耗".Translate();
        string onLabel = "对".Translate();
        string annealQuestion = "进行退火？".Translate();
        string annealTip = "退火后配方等级重置为0，获得永久回响加成。".Translate();

        UIMessageBox.Show("退火确认".Translate(),
            $"{consumeLabel} {LDB.items.Select(I宇宙矩阵).name} x1 {onLabel} {_selectedAnnealRecipe.TypeName} {annealQuestion}\n{annealTip}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(I宇宙矩阵, 1, out _)) return;
                _selectedAnnealRecipe.Anneal();
                RefreshAnnealUI();
            }, null);
    }

    public override void _OnOpen() {
        base._OnOpen();
        RefreshPoolHeader();
        RefreshAnnealUI();
    }

    public override void _OnUpdate() {
        if (_normalTicketText != null && GameMain.mainPlayer != null) {
            _normalTicketText.text = $"普通券×{GameMain.mainPlayer.package.GetItemCount(IFE普通抽卡券)}";
        }
        if (_premiumTicketText != null && GameMain.mainPlayer != null) {
            _premiumTicketText.text = $"精选券×{GameMain.mainPlayer.package.GetItemCount(IFE精选抽卡券)}";
        }
        RefreshPoolHeader();
    }
}
