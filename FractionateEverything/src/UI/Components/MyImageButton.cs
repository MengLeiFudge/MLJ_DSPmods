using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

/// <summary>
/// 带有边框和图片的按钮，默认大小为80x80，可以调整大小
/// </summary>
public class MyImageButton : MonoBehaviour {
    private static GameObject _baseObject;
    public RectTransform rectTrans;
    public UIButton uiButton;
    public Image borderImage;
    public Image spriteImage;
    //按钮状态
    private static Color normalColor;
    private static Color mouseOverColor;
    private static Color pressColor;
    private static Color selectedColor = new(1f, 0.9f, 0.2f, 0.6f);
    //选中与按钮组
    private MyImageButtonGroup _buttonGroup = null;
    private bool _selected = false;
    public bool Selected {
        get => _selected;
        set {
            _selected = value;
            borderImage.color = _selected ? selectedColor : normalColor;
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
            if (_proto is ItemProto item) {
                _proto = value;
                Sprite sprite = item.iconSprite;
                spriteImage.sprite = sprite;
                spriteImage.gameObject.SetActive(true);
            } else if (_proto is RecipeProto recipe) {
                _proto = value;
                Sprite sprite = recipe.iconSprite;
                spriteImage.sprite = sprite;
                spriteImage.gameObject.SetActive(true);
            } else if (_proto is TechProto tech) {
                _proto = value;
                Sprite sprite = tech.iconSprite;
                spriteImage.sprite = sprite;
                spriteImage.gameObject.SetActive(true);
            } else {
                _proto = null;
                spriteImage.sprite = null;
                spriteImage.gameObject.SetActive(false);
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
        normalColor = tankWindow.closeNormalColor;

        // 创建一个全新的按钮对象，而不是使用现有的
        _baseObject = new GameObject("my-image-button-base");

        // 添加必要的组件
        _baseObject.AddComponent<RectTransform>();
        var image = _baseObject.AddComponent<Image>();
        image.color = normalColor;

        // 添加Button组件
        _baseObject.AddComponent<Button>();

        // 添加UIButton组件
        var uiButton = _baseObject.AddComponent<UIButton>();
        uiButton.button = _baseObject.GetComponent<Button>();

        // 创建一个子对象用于显示图标
        var spriteObj = new GameObject("sprite");
        spriteObj.transform.SetParent(_baseObject.transform, false);
        var spriteRect = spriteObj.AddComponent<RectTransform>();
        spriteRect.anchorMin = new Vector2(0, 0);
        spriteRect.anchorMax = new Vector2(1, 1);
        spriteRect.offsetMin = new Vector2(5, 5);// 内边距
        spriteRect.offsetMax = new Vector2(-5, -5);// 内边距
        var spriteImage = spriteObj.AddComponent<Image>();
        spriteImage.color = new(1f, 1f, 1f, 1f);

        // 设置过渡效果
        uiButton.transitions = new UIButton.Transition[1];
        uiButton.transitions[0] = new UIButton.Transition();
        uiButton.transitions[0].target = image;
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
        ibtn.borderImage = go.GetComponent<Image>();
        ibtn.spriteImage = go.transform.Find("sprite").GetComponent<Image>();

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
        if (uiButton.button != null) {
            uiButton.button.onClick.RemoveAllListeners();
            if (onLeftClick != null) uiButton.button.onClick.AddListener(onLeftClick);
            if (onRightClick != null) {
                EventTrigger eventTrigger = gameObject.GetComponent<EventTrigger>();
                if (eventTrigger == null) {
                    eventTrigger = gameObject.AddComponent<EventTrigger>();
                }
                if (eventTrigger.triggers == null) {
                    eventTrigger.triggers = [];
                }
                eventTrigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerClick);
                EventTrigger.Entry entry = new EventTrigger.Entry {
                    eventID = EventTriggerType.PointerClick,
                };
                entry.callback.AddListener(data => {
                    PointerEventData pointerData = (PointerEventData)data;
                    if (pointerData.button == PointerEventData.InputButton.Right) {
                        onRightClick.Invoke();
                    }
                });
                eventTrigger.triggers.Add(entry);
            }
        }
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

    public float Width => rectTrans.sizeDelta.x;
    public float Height => rectTrans.sizeDelta.y;
}
