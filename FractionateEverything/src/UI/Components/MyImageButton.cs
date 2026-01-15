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
    private static GameObject _baseObject;
    public RectTransform rectTrans;
    public UIButton uiButton;
    public Image backgroundImage;
    public Image spriteImage;
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

        // 添加EventTrigger监听鼠标进入事件
        var eventTrigger = go.AddComponent<EventTrigger>();
        var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener(_ => {
            LogWarning($"_deselectOnHover = {ibtn._deselectOnHover}; Selected = {ibtn.Selected}");
            if (ibtn._deselectOnHover && ibtn.Selected) {
                ibtn.Selected = false;
                ibtn._onDeselectCallback?.Invoke();
            }
        });
        eventTrigger.triggers.Add(pointerEnter);

        rect.sizeDelta = new(width, height);
        ibtn.Proto = proto;

        //添加按钮悬浮提示
        ibtn.uiButton.tips.topLevel = true;
        ibtn.uiButton.tips.tipTitle = tipTitle;
        ibtn.uiButton.tips.tipText = tipContent;
        ibtn.uiButton.UpdateTip();

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
