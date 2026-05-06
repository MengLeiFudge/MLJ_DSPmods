using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using FE.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static FE.Utils.Utils;
using static FE.UI.Foundation.RectTransformUtils;
using FE.UI.Controls;

namespace FE.UI.Foundation.Window;

// MyWindow modified from LSTM: https://github.com/hetima/DSP_LSTM/blob/main/LSTM/MyWindowCtl.cs
// 大部分UI来源为UXAssist，感谢@soarqin的源码以供参考！

public class MyWindow : ManualBehaviour {
    private const int MinFontSize = 15;
    private float _maxX;
    protected float MaxY;

    // 固定窗口尺寸常量
    protected const float WindowWidth = 1366f;
    protected const float WindowHeight = 768f;

    // 布局常量
    protected const float TitleHeight = 48f;
    protected const float TabWidth = 130f;
    protected const float TabHeight = 27f;
    protected const float Margin = 30f;
    protected const float Spacing = 10f;
    protected const float LeftNavWidth = 130f;
    protected const float OuterMargin = 24f;
    protected const float SectionGap = 16f;
    protected const float RowGap = 8f;

    public event Action OnFree;
    private static GameObject _baseObject;

    public static void InitBaseObject() {
        if (_baseObject) return;
        var go = Instantiate(UIRoot.instance.uiGame.inserterWindow.gameObject);
        go.SetActive(false);
        go.name = "my-window";
        Destroy(go.GetComponent<UIInserterWindow>());
        for (var i = go.transform.childCount - 1; i >= 0; i--) {
            var child = go.transform.GetChild(i).gameObject;
            if (child.name != "panel-bg" && child.name != "shadow") {
                Destroy(child);
            }
        }

        _baseObject = go;
    }

    public static T Create<T>(string name, string title = "") where T : MyWindow {
        // var go = Instantiate(_baseObject, UIRoot.instance.uiGame.transform.parent);
        var go = Instantiate(_baseObject, UIRoot.instance.uiGame.inserterWindow.transform.parent);
        go.name = name;
        go.SetActive(false);
        MyWindow win = go.AddComponent<T>();
        if (!win) return null;

        var btn = go.transform.Find("panel-bg")?.gameObject.GetComponentInChildren<Button>();
        if (btn) btn.onClick.AddListener(win._Close);

        win.SetTitle(title);
        win._Create();
        if (MyWindowManager.Initialized) {
            win._Init(win.data);
        }
        return (T)win;
    }

    public override void _OnOpen() {
        ApplyFixedWindowSize();
    }

    public override void _OnFree() {
        OnFree?.Invoke();
    }

    public virtual void TryClose() {
        _Close();
    }

    public virtual bool IsWindowFunctional() {
        return true;
    }

    public void Open() {
        _Open();
        transform.SetAsLastSibling();
    }

    public void Close() => _Close();

    public void SetTitle(string title) {
        //todo: 英文会超出左边界
        var txt = gameObject.transform.Find("panel-bg/title-text")?.gameObject.GetComponent<Text>();
        if (txt) {
            txt.text = title.Translate();
        }
    }

    public void ApplyFixedWindowSize() {
        var trans = GetComponent<RectTransform>();
        trans.sizeDelta = new(WindowWidth, WindowHeight);
    }

    public void AutoFitWindowSize() {
        var trans = GetComponent<RectTransform>();
        trans.sizeDelta = new(_maxX + Margin + TabWidth + Spacing + Margin, MaxY + TitleHeight + Margin);
    }

    public static Text AddText(float x, float y, RectTransform parent, string label, int fontSize = 15,
        string objName = "label") {
        var src = UIRoot.instance.uiGame.assemblerWindow.stateText;
        var txt = Instantiate(src);
        txt.gameObject.name = objName;
        txt.text = label.Translate();
        txt.color = new(1f, 1f, 1f, 0.4f);
        txt.alignment = TextAnchor.MiddleLeft;
        txt.fontSize = Math.Max(MinFontSize, fontSize);
        txt.supportRichText = true;
        txt.rectTransform.sizeDelta = new(txt.preferredWidth + 8f, txt.preferredHeight + 8f);
        NormalizeRectWithMidLeft(txt.rectTransform, x, y, parent);
        return txt;
    }

    public Text AddText2(float x, float y, RectTransform parent, string label, int fontSize = 15,
        string objName = "label") {
        var text = AddText(x, y, parent, label, fontSize, objName);
        _maxX = Math.Max(_maxX, x + text.rectTransform.sizeDelta.x);
        MaxY = Math.Max(MaxY, y + text.rectTransform.sizeDelta.y);
        return text;
    }

    public static UIButton AddTipsButton(float x, float y, RectTransform parent, string tipTitle, string tipContent,
        string objName = "tips-button") {
        var src = UIRoot.instance.galaxySelect.sandboxToggle.gameObject.transform.parent.Find("tip-button");
        var dst = Instantiate(src);
        dst.gameObject.name = objName;
        var btn = dst.GetComponent<UIButton>();
        NormalizeRectWithMidLeft(btn, x, y, parent);
        btn.tips.topLevel = true;
        btn.tips.tipTitle = tipTitle;
        btn.tips.tipText = tipContent;
        btn.UpdateTip();
        return btn;
    }

    public UIButton AddTipsButton2(float x, float y, RectTransform parent, string tipTitle, string tipContent,
        string objName = "tips-button") {
        var tipsButton = AddTipsButton(x, y, parent, tipTitle, tipContent, objName);
        var rect = tipsButton.transform as RectTransform;
        if (rect != null) {
            _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
            MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        }

        return tipsButton;
    }

    public UIButton AddButton(float x, float y, RectTransform parent, string text = "", int fontSize = 16,
        string objName = "button", UnityAction onClick = null) {
        return AddButton(x, y, 150f, parent, text, fontSize, objName, onClick);
    }

    public UIButton AddButton(int xIdx, int xCount, float y, RectTransform parent, string text = "",
        int fontSize = 16,
        string objName = "button", UnityAction onClick = null) {
        (float, float) location = GetPosition(xIdx, xCount);
        return AddButton(location.Item1, y, location.Item2, parent, text, fontSize, objName, onClick);
    }

    public UIButton AddButton(float x, float y, float width, RectTransform parent, string text = "", int fontSize = 16,
        string objName = "button", UnityAction onClick = null) {
        var panel = UIRoot.instance.uiGame.statWindow.performancePanelUI;
        var btn = Instantiate(panel.cpuActiveButton);
        btn.gameObject.name = objName;
        var rect = NormalizeRectWithMidLeft(btn, x, y, parent);
        rect.sizeDelta = new(width, rect.sizeDelta.y);
        var l = btn.gameObject.transform.Find("button-text").GetComponent<Localizer>();
        var t = btn.gameObject.transform.Find("button-text").GetComponent<Text>();
        if (l != null) {
            l.stringKey = text;
            l.translation = text.Translate();
        }

        if (t != null) {
            t.text = text.Translate();
        }

        t.fontSize = Math.Max(MinFontSize, fontSize);
        btn.tip = null;
        btn.tips = new();
        btn.button.onClick.RemoveAllListeners();
        if (onClick != null) btn.button.onClick.AddListener(onClick);

        _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
        MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        return btn;
    }

    public MyImageButton AddImageButton(float x, float y, RectTransform parent, Proto proto = null,
        string objName = "image-button") {
        var btn = MyImageButton.CreateImageButton(x, y, parent, proto);
        btn.gameObject.name = objName;

        _maxX = Math.Max(_maxX, x + btn.Width);
        MaxY = Math.Max(MaxY, y + btn.Height);
        return btn;
    }

    public MyFlatButton AddFlatButton(float x, float y, RectTransform parent, string text = "", int fontSize = 15,
        string objName = "button", UnityAction onClick = null) {
        var btn = MyFlatButton.CreateFlatButton(x, y, parent, text, Math.Max(MinFontSize, fontSize), _ => onClick());
        btn.gameObject.name = objName;

        _maxX = Math.Max(_maxX, x + btn.Width);
        MaxY = Math.Max(MaxY, y + btn.Height);
        return btn;
    }

    public MyCheckBox AddCheckBox(float x, float y, RectTransform parent, ConfigEntry<bool> config, string label = "",
        int fontSize = 15) {
        var cb = MyCheckBox.CreateCheckBox(x, y, parent, config, label, Math.Max(MinFontSize, fontSize));
        _maxX = Math.Max(_maxX, x + cb.Width);
        MaxY = Math.Max(MaxY, y + cb.Height);
        return cb;
    }

    // public MyCheckBox AddCheckBox(float x, float y, RectTransform parent, bool check, string label = "",
    //     int fontSize = 15) {
    //     var cb = MyCheckBox.CreateCheckBox(x, y, parent, check, label, fontSize);
    //     _maxX = Math.Max(_maxX, x + cb.Width);
    //     MaxY = Math.Max(MaxY, y + cb.Height);
    //     return cb;
    // }

    public MyComboBox AddComboBox(float x, float y, RectTransform parent, int fontSize = 15) {
        var comboBox = MyComboBox.CreateComboBox(x, y, parent).WithFontSize(Math.Max(MinFontSize, fontSize));
        _maxX = Math.Max(_maxX, x + comboBox.Width);
        MaxY = Math.Max(MaxY, y + comboBox.Height);
        return comboBox;
    }

    public MyCornerComboBox AddCornerComboBox(float x, float y, RectTransform parent, int fontSize = 15) {
        var comboBox = MyCornerComboBox.CreateComboBox(x, y, parent).WithFontSize(Math.Max(MinFontSize, fontSize));
        _maxX = Math.Max(_maxX, x + comboBox.Width);
        MaxY = Math.Max(MaxY, y + comboBox.Height);
        return comboBox;
    }

    #region Slider

    public class ValueMapper<T> {
        public virtual int Min => 1;
        public virtual int Max => 100;

        public virtual int ValueToIndex(T value) =>
            (int)Convert.ChangeType(value, typeof(int), CultureInfo.InvariantCulture);

        public virtual T IndexToValue(int index) =>
            (T)Convert.ChangeType(index, typeof(T), CultureInfo.InvariantCulture);

        public virtual string FormatValue(string format, T value) {
            return string.Format($"{{0:{format}}}", value);
        }
    }

    /// <summary>
    /// 滑条位置是index，滑块显示数字是value
    /// </summary>
    public class RangeValueMapper<T>(int min, int max) : ValueMapper<T> {
        public override int Min => min;
        public override int Max => max;
    }

    /// <summary>
    /// 滑条位置是index，滑块显示数字是value
    /// </summary>
    private class ArrayMapper<T> : ValueMapper<T> {
        private readonly T[] _values;

        public ArrayMapper(T[] values) {
            Array.Sort(values);
            _values = values;
        }

        public override int Min => 0;
        public override int Max => _values.Length - 1;

        public override int ValueToIndex(T value) {
            return Array.BinarySearch(_values, value);
        }

        public override T IndexToValue(int index) {
            return _values[index >= 0 && index < _values.Length ? index : 0];
        }
    }

    public MySlider AddSlider(float x, float y, RectTransform parent, float value, float minValue, float maxValue,
        string format = "G", float width = 0f) {
        var slider = MySlider.CreateSlider(x, y, parent, value, minValue, maxValue, format, width);
        var rect = slider.rectTrans;
        if (rect != null) {
            _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
            MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        }

        return slider;
    }

    public MySideSlider AddSideSlider(float x, float y, RectTransform parent, float value, float minValue,
        float maxValue, string format = "G", float width = 0f, float textWidth = 0f) {
        var slider = MySideSlider.CreateSlider(x, y, parent, value, minValue, maxValue, format, width, textWidth);
        var rect = slider.rectTrans;
        if (rect != null) {
            _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
            MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        }

        return slider;
    }

    public MySlider AddSlider<T>(float x, float y, RectTransform parent, ConfigEntry<T> config,
        ValueMapper<T> valueMapper, string format = "G", float width = 0f) {
        var slider = MySlider.CreateSlider(x, y, parent, OnConfigValueChanged(config), valueMapper.Min, valueMapper.Max,
            format, width);
        slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        config.SettingChanged += SettingsChanged;
        OnFree += () => config.SettingChanged -= SettingsChanged;
        slider.OnValueChanged += () => {
            var index = Mathf.RoundToInt(slider.Value);
            config.Value = valueMapper.IndexToValue(index);
            slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        };

        var rect = slider.rectTrans;
        if (rect != null) {
            _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
            MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        }

        return slider;

        void SettingsChanged(object o, EventArgs a) {
            var index = OnConfigValueChanged(config);
            slider.Value = index;
            slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        }

        int OnConfigValueChanged(ConfigEntry<T> conf) {
            var index = valueMapper.ValueToIndex(conf.Value);
            if (index >= 0) return index;
            index = ~index;
            index = Math.Max(0, Math.Min(valueMapper.Max, index));
            conf.Value = valueMapper.IndexToValue(index);
            return index;
        }
    }

    public MySideSlider AddSideSlider<T>(float x, float y, RectTransform parent, ConfigEntry<T> config,
        ValueMapper<T> valueMapper, string format = "G", float width = 0f, float textWidth = 0f) {
        var slider = MySideSlider.CreateSlider(x, y, parent, OnConfigValueChanged(config), valueMapper.Min,
            valueMapper.Max, format, width, textWidth);
        slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        config.SettingChanged += SettingsChanged;
        OnFree += () => config.SettingChanged -= SettingsChanged;
        slider.OnValueChanged += () => {
            var index = Mathf.RoundToInt(slider.Value);
            config.Value = valueMapper.IndexToValue(index);
            slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        };

        var rect = slider.rectTrans;
        if (rect != null) {
            _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
            MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        }

        return slider;

        void SettingsChanged(object o, EventArgs a) {
            var index = OnConfigValueChanged(config);
            slider.Value = index;
            slider.SetLabelText(valueMapper.FormatValue(format, config.Value));
        }

        int OnConfigValueChanged(ConfigEntry<T> conf) {
            var index = valueMapper.ValueToIndex(conf.Value);
            if (index >= 0) return index;
            index = ~index;
            index = Math.Max(0, Math.Min(valueMapper.Max, index));
            conf.Value = valueMapper.IndexToValue(index);
            return index;
        }
    }

    public MySlider AddSlider<T>(float x, float y, RectTransform parent, ConfigEntry<T> config, T[] valueList,
        string format = "G", float width = 0f) {
        return AddSlider(x, y, parent, config, new ArrayMapper<T>(valueList), format, width);
    }

    public MySideSlider AddSideSlider<T>(float x, float y, RectTransform parent, ConfigEntry<T> config, T[] valueList,
        string format = "G", float width = 0f) {
        return AddSideSlider(x, y, parent, config, new ArrayMapper<T>(valueList), format, width);
    }

    #endregion

    public InputField AddInputField(float x, float y, RectTransform parent, string text = "", int fontSize = 16,
        string objName = "input", UnityAction<string> onChanged = null,
        UnityAction<string> onEditEnd = null) {
        var stationWindow = UIRoot.instance.uiGame.stationWindow;
        //public InputField nameInput;
        var inputField = Instantiate(stationWindow.nameInput);
        inputField.gameObject.name = objName;
        Destroy(inputField.GetComponent<UIButton>());
        inputField.GetComponent<Image>().color = new(1f, 1f, 1f, 0.05f);
        var rect = NormalizeRectWithMidLeft(inputField, x, y, parent);
        rect.sizeDelta = new(210, rect.sizeDelta.y);
        inputField.text = text;
        inputField.textComponent.fontSize = fontSize;

        inputField.onValueChanged.RemoveAllListeners();
        if (onChanged != null) inputField.onValueChanged.AddListener(onChanged);
        inputField.onEndEdit.RemoveAllListeners();
        if (onEditEnd != null) inputField.onEndEdit.AddListener(onEditEnd);

        _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
        MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        return inputField;
    }

    public InputField AddInputField(float x, float y, float width, RectTransform parent, ConfigEntry<string> config,
        int fontSize = 16, string objName = "input") {
        var stationWindow = UIRoot.instance.uiGame.stationWindow;
        //public InputField nameInput;
        var inputField = Instantiate(stationWindow.nameInput);
        inputField.gameObject.name = objName;
        Destroy(inputField.GetComponent<UIButton>());
        inputField.GetComponent<Image>().color = new(1f, 1f, 1f, 0.05f);
        var rect = NormalizeRectWithMidLeft(inputField, x, y, parent);
        rect.sizeDelta = new(width, rect.sizeDelta.y);
        inputField.text = config.Value;
        inputField.textComponent.fontSize = fontSize;

        inputField.onValueChanged.RemoveAllListeners();
        inputField.onEndEdit.RemoveAllListeners();
        inputField.onEndEdit.AddListener(value => config.Value = value);

        _maxX = Math.Max(_maxX, x + rect.sizeDelta.x);
        MaxY = Math.Max(MaxY, y + rect.sizeDelta.y);
        return inputField;
    }
}
