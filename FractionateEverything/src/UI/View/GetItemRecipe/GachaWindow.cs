using System.Collections.Generic;
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
    
    private RectTransform[] _cardSlots = new RectTransform[10];
    
    private UIButton _btnRevealAll;
    private UIButton _btnDraw1;
    private UIButton _btnDraw10;
    
    private class PoolGroup {
        public string NameKey;
        public bool Expanded;
        public List<int> PoolIds;
        public GameObject HeaderBtn;
        public List<GameObject> ItemBtns;
    }
    
    private List<PoolGroup> _poolGroups = new();

    public static void CreateInstance() {
        Instance = MyWindow.Create<GachaWindow>("gacha-window", "分馏抽卡");
    }

    public static void AddTranslations() {
        Register("分馏抽卡", "Fractionation Gacha");
        Register("常驻池", "Permanent Pool");
        Register("UP池", "UP Pool");
        Register("限定池", "Limited Pool");
        Register("全部翻开", "Reveal All");
        Register("保底进度", "Pity Progress");
    }

    public override void _OnCreate() {
        MaxY = 550f;
        var trans = GetComponent<RectTransform>();
        trans.sizeDelta = new Vector2(1100f, 550f + TitleHeight + Margin);
        
        _leftPanel = new GameObject("left-panel").AddComponent<RectTransform>();
        _leftPanel.SetParent(trans, false);
        _leftPanel.anchorMin = new Vector2(0, 1);
        _leftPanel.anchorMax = new Vector2(0, 1);
        _leftPanel.pivot = new Vector2(0, 1);
        _leftPanel.anchoredPosition = new Vector2(Margin, -TitleHeight - Margin);
        _leftPanel.sizeDelta = new Vector2(220f, 550f);
        
        _rightPanel = new GameObject("right-panel").AddComponent<RectTransform>();
        _rightPanel.SetParent(trans, false);
        _rightPanel.anchorMin = new Vector2(0, 1);
        _rightPanel.anchorMax = new Vector2(0, 1);
        _rightPanel.pivot = new Vector2(0, 1);
        _rightPanel.anchoredPosition = new Vector2(Margin + 220f + Spacing, -TitleHeight - Margin);
        _rightPanel.sizeDelta = new Vector2(1100f - 220f - Spacing - Margin * 2, 550f);
        
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
            
            var img = slot.gameObject.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            _cardSlots[i] = slot;
        }
        
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
    }

    public override void _OnOpen() {
    }

    public override void _OnUpdate() {
        if (_normalTicketText != null && GameMain.mainPlayer != null) {
            _normalTicketText.text = $"普通券×{GameMain.mainPlayer.package.GetItemCount(8058)}";
        }
        if (_premiumTicketText != null && GameMain.mainPlayer != null) {
            _premiumTicketText.text = $"精选券×{GameMain.mainPlayer.package.GetItemCount(8059)}";
        }
    }
}
