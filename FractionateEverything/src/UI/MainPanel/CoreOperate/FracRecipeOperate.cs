using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.Components;
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

public static partial class FracRecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static Text txtCurrItem;
    private static MyImageButton btnSelectedItem;
    private static readonly UIButton[] recipeSandboxBtn = new UIButton[4];

    private static void OnButtonChangeItemClick(bool showLocked, float y) {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(SelectedRecipeType, item.ID);
            return recipe != null && (showLocked || RecipeGrowthQueries.IsUnlocked(recipe));
        });
    }

    private static ConfigEntry<int> RecipeTypeEntry;
    private static ERecipe SelectedRecipeType => RecipeTypes[RecipeTypeEntry.Value];
    private static BaseRecipe SelectedRecipe => GetRecipe<BaseRecipe>(SelectedRecipeType, SelectedItem.ID);

    // ==================== 布局常量 ====================

    private const int InfoLineCount = 28;// 左列文本行数
    private const int LevelLineCount = 13;// 右列: 标题 + Lv0 到 Lv5 + 预留空行
    private const float RightColX = 620f;// 右列X起始位置
    private const float IconSize = 24f;
    private const float TextOffsetWithIcon = 28f;// 图标宽度 + 间距
    private const float LineHeight = 22f;

    // 产物行布局（格式：概率 | 图标 | 数量）
    private const float ProductRatioX = 0f;// 左侧概率文本X
    private const float ProductIconX = 72f;// 物品图标X（概率文本右侧）
    private const float ProductTextX = 100f;// 名称×数目文本X（= ProductIconX + TextOffsetWithIcon）

    // ==================== UI 元素 ====================

    private static Text[] txtRecipeInfo = new Text[InfoLineCount];
    private static Text[] txtProductLeft = new Text[InfoLineCount];// 产物行左侧文本（概率/等效数量）
    private static MyImageButton[] btnRecipeInfoIcons = new MyImageButton[InfoLineCount];
    private static MySlider[] incSliders = new MySlider[InfoLineCount];
    private static ConfigEntry<int> selectedInc;

    // 右列：配方强化等级信息
    private static Text[] txtLevelInfo = new Text[LevelLineCount];
}
