using System.Collections.Generic;

namespace FE.UI.Components;

/// <summary>
/// 按钮组，用于管理按钮的选中状态
/// </summary>
public class MyImageButtonGroup(bool singleSelection = true) {
    private readonly List<MyImageButton> _buttons = new();
    // true: 单选组，false: 多选组

    public void AddButton(MyImageButton button) {
        if (!_buttons.Contains(button)) {
            _buttons.Add(button);
        }
    }

    public void RemoveButton(MyImageButton button) {
        _buttons.Remove(button);
    }

    public void OnButtonSelected(MyImageButton selectedButton) {
        if (singleSelection) {
            // 单选组：取消其他按钮的选中状态
            foreach (var button in _buttons) {
                if (button != selectedButton && button.Selected) {
                    button.Selected = false;
                }
            }
        }
    }

    public void ToggleButton(MyImageButton button) {
        if (singleSelection) {
            if (button.Selected) {
                // 单选组中，如果当前按钮已选中，不允许取消选中（保证至少有一个选中）
                return;
            } else {
                button.Selected = true;
            }
        } else {
            // 多选组：直接切换状态
            button.Selected = !button.Selected;
        }
    }

    public List<MyImageButton> GetSelectedButtons() {
        return _buttons.FindAll(b => b.Selected);
    }

    public MyImageButton GetSelectedButton() {
        return _buttons.Find(b => b.Selected);
    }

    public void ClearSelection() {
        foreach (var button in _buttons) {
            button.Selected = false;
        }
    }

    public void SelectButton(MyImageButton button) {
        if (_buttons.Contains(button)) {
            button.Selected = true;
        }
    }
}
