using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

public class MyImageButton : MonoBehaviour {
    public RectTransform rectTrans;
    public UIButton uiButton;
    public Image boxImage;
    public Image itemImage;
    public event Action OnChecked;
    private bool _checked;

    private static GameObject _baseObject;

    private static readonly Color BoxColor = new(1f, 1f, 1f, 100f / 255f);
    private static readonly Color CheckColor = new(1f, 1f, 1f, 1f);

    public static void InitBaseObject() {
        if (_baseObject) return;
        var go = Instantiate(UIRoot.instance.uiGame.buildMenu.uxFacilityCheck.gameObject);
        go.name = "my-image-button";
        go.SetActive(false);
        var comp = go.transform.Find("text");
        if (comp) {
            var txt = comp.GetComponent<Text>();
            if (txt) DestroyImmediate(txt);
            var localizer = comp.GetComponent<Localizer>();
            if (localizer) DestroyImmediate(localizer);
        }
        _baseObject = go;
    }

    protected void OnDestroy() {
        if (_config != null) _config.SettingChanged -= _configChanged;
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, int itemId) {
        return CreateImageButton(x, y, parent, LDB.items.Select(itemId).iconSprite);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, ItemProto proto) {
        return CreateImageButton(x, y, parent, proto.iconSprite);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, Sprite sprite) {
        var go = Instantiate(_baseObject);
        go.name = "my-image-button";
        go.SetActive(true);
        var cb = go.AddComponent<MyImageButton>();
        var rect = NormalizeRectWithTopLeft(cb, x, y, parent);

        cb.rectTrans = rect;
        cb.uiButton = go.GetComponent<UIButton>();
        cb.boxImage = go.transform.GetComponent<Image>();
        cb.itemImage = go.transform.Find("checked")?.GetComponent<Image>();
        // cb.itemImage.sprite = sprite;
        cb.SetSprite(sprite);
        NormalizeRectWithTopLeft(cb.itemImage, 0f, 0f);

        // var child = go.transform.Find("text");
        // if (child != null) {
        //     cb.labelText = child.GetComponent<Text>();
        //     if (cb.labelText) {
        //         cb.labelText.text = "";
        //         cb.labelText.fontSize = fontSize;
        //         cb.UpdateLabelTextWidth();
        //     }
        // }

        cb.uiButton.onClick += cb.OnClick;
        return cb;
    }

    // private void UpdateLabelTextWidth() {
    //     if (labelText)
    //         labelText.rectTransform.sizeDelta =
    //             new(labelText.preferredWidth, labelText.rectTransform.sizeDelta.y);
    // }

    public bool Checked {
        get => _checked;
        set {
            _checked = value;
            // itemImage.enabled = value;
            boxImage.enabled = value;
        }
    }

    // public void SetLabelText(string val) {
    //     if (labelText != null) {
    //         labelText.text = val.Translate();
    //         UpdateLabelTextWidth();
    //     }
    // }

    public void SetEnable(bool on) {
        if (uiButton) uiButton.enabled = on;
        if (on) {
            if (boxImage) boxImage.color = BoxColor;
            if (itemImage) itemImage.color = CheckColor;
            // if (labelText) labelText.color = TextColor;
        } else {
            if (boxImage) boxImage.color = BoxColor.RGBMultiplied(0.5f);
            if (itemImage) itemImage.color = CheckColor.RGBMultiplied(0.5f);
            // if (labelText) labelText.color = TextColor.RGBMultiplied(0.5f);
        }
    }

    public void SetSprite(Sprite sprite) {
        itemImage.sprite = sprite;
        WithBox(sprite.rect.width, sprite.rect.height);
    }

    private EventHandler _configChanged;
    private Action _checkedChanged;
    private ConfigEntry<bool> _config;

    public void SetConfigEntry(ConfigEntry<bool> config) {
        if (_checkedChanged != null) OnChecked -= _checkedChanged;
        if (_configChanged != null) config.SettingChanged -= _configChanged;

        _config = config;
        _checkedChanged = () => config.Value = !config.Value;
        OnChecked += _checkedChanged;
        _configChanged = (_, _) => Checked = config.Value;
        config.SettingChanged += _configChanged;
    }

    public MyImageButton WithItem(int itemId) {
        return WithItem(LDB.items.Select(itemId));
    }

    public MyImageButton WithItem(ItemProto item) {
        if (item == null || item.iconSprite == null) {
            return this;
        }
        SetSprite(item.iconSprite);
        return this;
    }

    // public MyImageButton WithLabelText(string val) {
    //     SetLabelText(val);
    //     return this;
    // }

    public MyImageButton WithCheck(bool check) {
        Checked = check;
        return this;
    }

    public MyImageButton WithBox(float boxSize) {
        return WithBox(boxSize, boxSize);
    }

    public MyImageButton WithBox(float boxX, float boxY) {
        rectTrans.sizeDelta = new(boxX, boxY);
        itemImage.rectTransform.sizeDelta = new(boxX, boxY);
        return this;
    }

    public MyImageButton WithEnable(bool on) {
        SetEnable(on);
        return this;
    }

    public MyImageButton WithConfigEntry(ConfigEntry<bool> config) {
        SetConfigEntry(config);
        return this;
    }

    public void OnClick(int obj) {
        _checked = !_checked;
        // itemImage.enabled = _checked;
        boxImage.enabled = _checked;
        OnChecked?.Invoke();
    }

    public float Width => rectTrans.sizeDelta.x;
    public float Height => rectTrans.sizeDelta.y;
}
