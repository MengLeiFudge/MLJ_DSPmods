# UI/Controls — Reusable Controls

本目录只放可复用的 Unity/DSP 控件包装，不放窗口框架、主面板壳或页面布局 DSL。

## Files

- `MyCheckButton.cs`
- `MyCheckBox.cs`
- `MyComboBox.cs`
- `MyCornerComboBox.cs`
- `MyFlatButton.cs`
- `MyImageButton.cs`
- `MyImageButtonGroup.cs`
- `MySideSlider.cs`
- `MySlider.cs`

## Rules

- 控件可以依赖 `UI/Foundation` 的 RectTransform 工具。
- 控件不得依赖 `UI/MainPanel` 页面或主面板 Shell。
- 新控件一类一文件；不要把多个控件塞回同一个文件。
