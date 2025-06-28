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
    public Image checkImage;
    // public Text labelText;
    public event Action OnChecked;
    private bool _checked;

    private static GameObject _baseObject;

    private static readonly Color BoxColor = new(1f, 1f, 1f, 100f / 255f);
    private static readonly Color CheckColor = new(1f, 1f, 1f, 1f);
    private static readonly Color TextColor = new(178f / 255f, 178f / 255f, 178f / 255f, 168f / 255f);

    public static void InitBaseObject() {
        if (_baseObject) return;
        var go = Instantiate(UIRoot.instance.uiGame.buildMenu.uxFacilityCheck.gameObject);
        go.name = "my-image-button";
        go.SetActive(false);
        // var comp = go.transform.Find("text");
        // if (comp) {
        //     var txt = comp.GetComponent<Text>();
        //     if (txt) txt.text = "";
        //     var localizer = comp.GetComponent<Localizer>();
        //     if (localizer) DestroyImmediate(localizer);
        // }
        _baseObject = go;
    }

    protected void OnDestroy() {
        if (_config != null) _config.SettingChanged -= _configChanged;
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, int itemId,
        int fontSize = 15) {
        return CreateImageButton(x, y, parent, LDB.items.Select(itemId).iconSprite, fontSize);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, ItemProto proto,
        int fontSize = 15) {
        return CreateImageButton(x, y, parent, proto.iconSprite, fontSize);
    }

    public static MyImageButton CreateImageButton(float x, float y, RectTransform parent, Sprite sprite,
        int fontSize = 15) {
        var go = Instantiate(_baseObject);
        go.name = "my-image-button";
        go.SetActive(true);
        var cb = go.AddComponent<MyImageButton>();
        var rect = NormalizeRectWithTopLeft(cb, x, y, parent);

        cb.rectTrans = rect;
        cb.uiButton = go.GetComponent<UIButton>();
        cb.boxImage = go.transform.GetComponent<Image>();
        cb.checkImage = go.transform.Find("checked")?.GetComponent<Image>();
        cb.checkImage.sprite = sprite;
        NormalizeRectWithTopLeft(cb.checkImage, 0f, 0f);

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
            checkImage.enabled = value;
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
            if (checkImage) checkImage.color = CheckColor;
            // if (labelText) labelText.color = TextColor;
        } else {
            if (boxImage) boxImage.color = BoxColor.RGBMultiplied(0.5f);
            if (checkImage) checkImage.color = CheckColor.RGBMultiplied(0.5f);
            // if (labelText) labelText.color = TextColor.RGBMultiplied(0.5f);
        }
    }

    public void SetSprite(Sprite sprite) {
        checkImage.sprite = sprite;
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
        if (item == null) {
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

    // public MyImageButton WithSmallerBox(float boxSize = 20f) {
    //     var oldWidth = rectTrans.sizeDelta.x;
    //     rectTrans.sizeDelta = new(boxSize, boxSize);
    //     checkImage.rectTransform.sizeDelta = new(boxSize, boxSize);
    //     labelText.rectTransform.sizeDelta = new(labelText.rectTransform.sizeDelta.x, boxSize);
    //     labelText.rectTransform.localPosition =
    //         new(labelText.rectTransform.localPosition.x + boxSize - oldWidth,
    //             labelText.rectTransform.localPosition.y, labelText.rectTransform.localPosition.z);
    //     return this;
    // }

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
        checkImage.enabled = _checked;
        OnChecked?.Invoke();
    }

    // public float Width => rectTrans.sizeDelta.x + labelText.rectTransform.sizeDelta.x;
    // public float Height => Math.Max(rectTrans.sizeDelta.y, labelText.rectTransform.sizeDelta.y);
}
