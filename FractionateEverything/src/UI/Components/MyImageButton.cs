using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

/// <summary>
/// 带图片的按钮，默认大小为80x80
/// </summary>
public class MyImageButton : MonoBehaviour {
    private const float ProtoTipDelay = 0.4f;
    private const int ProtoTipAnchor = 7;
    private static readonly Vector2 ProtoTipOffset = new(15f, -50f);
    private static readonly StringBuilder countSb = new("                ", 16);
    private static GameObject _baseObject;
    public RectTransform rectTrans;
    public UIButton uiButton;
    public Image backgroundImage;
    public Image spriteImage;
    public Text countText;
    //背景图在不同状态下的颜色
    private static Color normalColor = Color.clear;
    private static Color mouseOverColor;
    private static Color pressColor;
    private static Color selectedColor = new(1f, 0.9f, 0.2f, 0.2f);
    //是否选中与按钮组
    private MyImageButtonGroup _buttonGroup = null;
    private bool _selected = false;
    private bool _deselectOnHover = false;
    private UnityAction _onDeselectCallback = null;
    public bool Selected {
        get => _selected;
        set {
            _selected = value;
            backgroundImage.color = _selected ? selectedColor : normalColor;
            // 通知按钮组
            if (_buttonGroup != null && _selected) {
                _buttonGroup.OnButtonSelected(this);
            }
        }
    }
    //配方与图片
    private Proto _proto = null;
    public Proto Proto {
        get => _proto;
        set {
            if (value is ItemProto item) {
                _proto = value;
                Sprite sprite = item.iconSprite;
                spriteImage.sprite = sprite;
            } else if (value is RecipeProto recipe) {
                _proto = value;
                Sprite sprite = recipe.iconSprite;
                spriteImage.sprite = sprite;
            } else if (value is TechProto tech) {
                _proto = value;
                Sprite sprite = tech.iconSprite;
                spriteImage.sprite = sprite;
            } else {
                _proto = null;
                spriteImage.sprite = null;
            }
            ApplyProtoTip();
        }
    }
    public int ProtoID => _proto?.ID ?? 0;

    public static void InitBaseObject() {
        if (_baseObject) return;

        // 使用tankWindow中的颜色作为鼠标悬停效果的颜色
        var tankWindow = UIRoot.instance.uiGame.tankWindow;
        mouseOverColor = tankWindow.closeMouseOverColor;
        pressColor = tankWindow.closePressColor;
        // normalColor = tankWindow.closeNormalColor;

        // 创建一个新的GameObject
        _baseObject = new("my-image-button-base");
        _baseObject.AddComponent<RectTransform>();

        // 添加UIButton用于添加点击事件
        var uiButton = _baseObject.AddComponent<UIButton>();
        _baseObject.AddComponent<Button>();
        uiButton.Init();// 初始化以绑定uiButton和button

        // 创建背景图片子对象
        var backgroundObj = new GameObject("backgroundImage");
        backgroundObj.transform.SetParent(_baseObject.transform, false);
        var backgroundRect = backgroundObj.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.sizeDelta = Vector2.zero;
        backgroundRect.anchoredPosition = Vector2.zero;
        var backgroundImage = backgroundObj.AddComponent<Image>();
        backgroundImage.color = normalColor;
        backgroundImage.raycastTarget = false;// 确保背景不拦截射线检测

        // 创建精灵图片子对象
        var spriteObj = new GameObject("spriteImage");
        spriteObj.transform.SetParent(_baseObject.transform, false);
        var spriteRect = spriteObj.AddComponent<RectTransform>();
        spriteRect.anchorMin = Vector2.zero;
        spriteRect.anchorMax = Vector2.one;
        spriteRect.sizeDelta = Vector2.zero;
        spriteRect.anchoredPosition = Vector2.zero;
        var spriteImage = spriteObj.AddComponent<Image>();
        spriteImage.color = Color.white;

        // 创建右下角数字子对象（默认隐藏）
        var countObj = new GameObject("countText");
        countObj.transform.SetParent(_baseObject.transform, false);
        var countRect = countObj.AddComponent<RectTransform>();
        countRect.anchorMin = new Vector2(1f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.anchoredPosition = new Vector2(-2f, 2f);
        countRect.sizeDelta = new Vector2(36f, 16f);
        var countText = countObj.AddComponent<Text>();
        countText.raycastTarget = false;
        countText.alignment = TextAnchor.LowerRight;
        countText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countText.verticalOverflow = VerticalWrapMode.Overflow;
        countText.resizeTextForBestFit = false;
        countText.fontSize = 12;
        countText.color = Color.white;
        countText.text = string.Empty;
        var fontSource = UIRoot.instance.uiGame.buildMenu.uxFacilityCheck.transform.Find("text")?.GetComponent<Text>();
        if (fontSource != null) {
            countText.font = fontSource.font;
            countText.fontStyle = fontSource.fontStyle;
        }
        var outline = countObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        countObj.SetActive(false);

        // 设置过渡效果
        uiButton.transitions = new UIButton.Transition[1];
        uiButton.transitions[0] = new UIButton.Transition();
        uiButton.transitions[0].target = spriteImage;
        uiButton.transitions[0].normalColor = normalColor;
        uiButton.transitions[0].mouseoverColor = mouseOverColor;
        uiButton.transitions[0].pressedColor = pressColor;
        // 设置过渡效果的大小变化（避免大小变化导致的问题）
        uiButton.transitions[0].highlightSizeMultiplier = 1.0f;
        uiButton.transitions[0].mouseoverSize = 1.0f;
        uiButton.transitions[0].pressedSize = 1.0f;

        _baseObject.SetActive(false);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent,
        Proto proto, float width = 40f, float height = 40f,
        string tipTitle = "", string tipContent = "") {
        var go = Instantiate(_baseObject);
        go.name = "my-image-button";
        go.SetActive(true);

        var ibtn = go.AddComponent<MyImageButton>();
        var rect = NormalizeRectWithMidLeft(ibtn, x, y, parent, height);

        ibtn.rectTrans = rect;
        ibtn.uiButton = go.GetComponent<UIButton>();
        ibtn.backgroundImage = go.transform.Find("backgroundImage").GetComponent<Image>();
        ibtn.spriteImage = go.transform.Find("spriteImage").GetComponent<Image>();
        ibtn.countText = go.transform.Find("countText").GetComponent<Text>();

        // 添加EventTrigger监听鼠标进入事件
        var eventTrigger = go.AddComponent<EventTrigger>();
        var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener(_ => {
            if (ibtn._deselectOnHover && ibtn.Selected) {
                ibtn.Selected = false;
                ibtn._onDeselectCallback?.Invoke();
            }
        });
        eventTrigger.triggers.Add(pointerEnter);

        rect.sizeDelta = new(width, height);
        ibtn.Proto = proto;

        //添加按钮悬浮提示
        bool isItemOrRecipeProto = proto is ItemProto or RecipeProto;
        if (!isItemOrRecipeProto && (!string.IsNullOrEmpty(tipTitle) || !string.IsNullOrEmpty(tipContent))) {
            ibtn.uiButton.tips.type = UIButton.ItemTipType.Other;
            ibtn.uiButton.tips.itemId = 0;
            ibtn.uiButton.tips.topLevel = true;
            ibtn.uiButton.tips.delay = 1f;
            ibtn.uiButton.tips.corner = 2;
            ibtn.uiButton.tips.tipTitle = tipTitle;
            ibtn.uiButton.tips.tipText = tipContent;
            ibtn.uiButton.UpdateTip();
        }

        return ibtn;
    }

    public MyImageButton WithTakeItemClickEvent() {
        WithClickEvent(() => ClickToMoveModDataItem(ProtoID, true), () => ClickToMoveModDataItem(ProtoID, false));
        return this;
    }

    public MyImageButton WithClickEvent(UnityAction onLeftClick, UnityAction onRightClick) {
        uiButton.onClick += _ => onLeftClick?.Invoke();
        uiButton.onRightClick += _ => onRightClick?.Invoke();
        return this;
    }

    public MyImageButton WithSize(float width, float height) {
        rectTrans.sizeDelta = new(width, height);
        // spriteImage.rectTransform.sizeDelta = new(width, height);
        return this;
    }

    public MyImageButton WithTip(string tip, float delay = 1f) {
        if (uiButton == null) return this;
        uiButton.tips.type = UIButton.ItemTipType.Other;
        uiButton.tips.topLevel = true;
        uiButton.tips.tipTitle = tip;
        uiButton.tips.tipText = null;
        uiButton.tips.delay = delay;
        uiButton.tips.corner = 2;
        uiButton.UpdateTip();
        return this;
    }

    public void SetCountText(string text, bool show = true) {
        if (countText == null) {
            return;
        }

        bool visible = show && !string.IsNullOrEmpty(text);
        countText.text = visible ? text : string.Empty;
        countText.gameObject.SetActive(visible);
    }

    public void SetCount(long count, bool show = true, bool formatKmg = true) {
        SetCountText(FormatCountText(count, formatKmg), show);
    }

    public void ClearCountText() {
        SetCountText(string.Empty, false);
    }

    public MyImageButton WithCountText(string text, bool show = true) {
        SetCountText(text, show);
        return this;
    }

    public MyImageButton WithCount(long count, bool show = true, bool formatKmg = true) {
        SetCount(count, show, formatKmg);
        return this;
    }

    private static string FormatCountText(long count, bool formatKmg) {
        if (!formatKmg || (count <= 10000 && count >= -10000)) {
            return count.ToString();
        }

        countSb.Length = 16;
        for (int i = 0; i < 16; i++) {
            countSb[i] = ' ';
        }
        StringBuilderUtility.WriteKMG(countSb, 8, count, blank: true);
        return countSb.ToString().Trim();
    }

    private void ApplyProtoTip() {
        if (uiButton == null) {
            return;
        }

        if (_proto is ItemProto itemProto) {
            uiButton.tips.type = UIButton.ItemTipType.Item;
            uiButton.tips.itemId = itemProto.ID;
            uiButton.tips.itemCount = 0;
            uiButton.tips.itemInc = 0;
            uiButton.tips.topLevel = true;
            uiButton.tips.delay = ProtoTipDelay;
            uiButton.tips.corner = ProtoTipAnchor;
            uiButton.tips.offset = ProtoTipOffset;
            uiButton.tips.tipTitle = null;
            uiButton.tips.tipText = null;
            uiButton.UpdateTip();
            return;
        }

        if (_proto is RecipeProto recipeProto) {
            uiButton.tips.type = UIButton.ItemTipType.Recipe;
            uiButton.tips.itemId = -recipeProto.ID;
            uiButton.tips.itemCount = 0;
            uiButton.tips.itemInc = 0;
            uiButton.tips.topLevel = true;
            uiButton.tips.delay = ProtoTipDelay;
            uiButton.tips.corner = ProtoTipAnchor;
            uiButton.tips.offset = ProtoTipOffset;
            uiButton.tips.tipTitle = null;
            uiButton.tips.tipText = null;
            uiButton.UpdateTip();
            return;
        }

        uiButton.tips.type = UIButton.ItemTipType.None;
        uiButton.tips.itemId = 0;
        uiButton.tips.offset = Vector2.zero;
        uiButton.tips.tipTitle = null;
        uiButton.tips.tipText = null;
        uiButton.UpdateTip();
    }

    public MyImageButton WithSelected(bool selected) {
        Selected = selected;
        return this;
    }

    public MyImageButton WithGroup(MyImageButtonGroup group) {
        _buttonGroup = group;
        group?.AddButton(this);
        return this;
    }

    public MyImageButton WithDeselectOnHover(bool enable = true, UnityAction onDeselect = null) {
        _deselectOnHover = enable;
        _onDeselectCallback = onDeselect;
        return this;
    }

    public float Width => rectTrans.sizeDelta.x;
    public float Height => rectTrans.sizeDelta.y;
}
