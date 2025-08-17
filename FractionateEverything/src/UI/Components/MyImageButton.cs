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
    public RectTransform rectTrans;
    public UIButton uiButton;
    public Image borderImage;
    public Image spriteImage;

    private static GameObject _baseObject;

    private static Color mouseOverColor;
    private static Color pressColor;
    private static Color normalColor;

    // 修改为半透明背景色，而不是边框色
    private static readonly Color BackgroundColor = new(0.4f, 0.4f, 0.4f, 0.3f);
    private static readonly Color SpriteColor = new(1f, 1f, 1f, 1f);

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
        image.color = BackgroundColor;

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
        spriteImage.color = SpriteColor;

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
        int itemID, UnityAction onLeftClick = null, UnityAction onRightClick = null,
        string tipTitle = "", string tipContent = "") {
        return CreateImageButton(x, y, parent, 40, 40, LDB.items.Select(itemID)?.iconSprite,
            onLeftClick, onRightClick, tipTitle, tipContent);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent,
        float width, float height, Sprite sprite = null,
        UnityAction onLeftClick = null, UnityAction onRightClick = null,
        string tipTitle = "", string tipContent = "") {
        var go = Instantiate(_baseObject);
        go.name = "my-image-button";
        go.SetActive(true);

        var cb = go.AddComponent<MyImageButton>();
        var rect = NormalizeRectWithMidLeft(cb, x, y, parent, height);

        cb.rectTrans = rect;
        cb.uiButton = go.GetComponent<UIButton>();
        cb.borderImage = go.GetComponent<Image>();
        cb.spriteImage = go.transform.Find("sprite").GetComponent<Image>();

        // 设置大小
        rect.sizeDelta = new(width, height);

        // 设置初始精灵
        if (sprite != null) {
            cb.SetSprite(sprite);
        }

        // 添加点击事件
        if (cb.uiButton.button != null) {
            cb.uiButton.button.onClick.RemoveAllListeners();
            if (onLeftClick != null) cb.uiButton.button.onClick.AddListener(onLeftClick);
            if (onRightClick != null) {
                EventTrigger eventTrigger = cb.gameObject.GetComponent<EventTrigger>();
                if (eventTrigger == null) {
                    eventTrigger = cb.gameObject.AddComponent<EventTrigger>();
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
        } else {
            LogError("Button component is null in MyImageButton");
        }

        //添加按钮悬浮提示
        cb.uiButton.tips.topLevel = true;
        cb.uiButton.tips.tipTitle = tipTitle;
        cb.uiButton.tips.tipText = tipContent;
        cb.uiButton.UpdateTip();

        return cb;
    }

    public void SetEnable(bool on) {
        if (uiButton) uiButton.enabled = on;
        if (on) {
            if (borderImage) borderImage.color = BackgroundColor;
            if (spriteImage) spriteImage.color = SpriteColor;
        } else {
            if (borderImage)
                borderImage.color = new Color(BackgroundColor.r, BackgroundColor.g, BackgroundColor.b,
                    BackgroundColor.a * 0.5f);
            if (spriteImage)
                spriteImage.color = new Color(SpriteColor.r, SpriteColor.g, SpriteColor.b, SpriteColor.a * 0.5f);
        }
    }

    public void SetSprite(Sprite sprite) {
        spriteImage.sprite = sprite;
        spriteImage.gameObject.SetActive(sprite != null);
    }

    public MyImageButton WithSize(float width, float height) {
        rectTrans.sizeDelta = new(width, height);
        // spriteImage.rectTransform.sizeDelta = new(width, height);
        return this;
    }

    public MyImageButton WithSprite(Sprite sprite) {
        SetSprite(sprite);
        return this;
    }

    public MyImageButton WithItemSprite(int itemId) {
        SetSprite(LDB.items.Select(itemId).iconSprite);
        return this;
    }

    public MyImageButton WithEnable(bool on) {
        SetEnable(on);
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

    public float Width => rectTrans.sizeDelta.x;
    public float Height => rectTrans.sizeDelta.y;
}
