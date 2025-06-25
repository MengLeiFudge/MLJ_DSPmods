using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

namespace FE.UI.View;

public static class TabRecipeAndBuilding {
    public static RectTransform _windowTrans;

    #region 配方详情

    public static ConfigEntry<int> RecipeTypeEntry;
    public static string[] RecipeTypeNames = ["建筑培养", "矿物复制", "量子复制", "点金", "分解", "转化"];
    public static ERecipe[] RecipeTypes = [
        ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.QuantumDuplicate,
        ERecipe.Alchemy, ERecipe.Deconstruction, ERecipe.Conversion,
    ];

    public static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);

    public static void OnButtonChangeItemClick() {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = _windowTrans.anchoredPosition.x + _windowTrans.rect.width / 2 + 5;
        float y = _windowTrans.anchoredPosition.y + _windowTrans.rect.height / 2 + 5;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, false, item => GetRecipe<BaseRecipe>(RecipeTypes[RecipeTypeEntry.Value], item.ID) != null);
    }

    private static Text[] textRecipeInfo = new Text[30];

    #endregion

    #region 建筑加成

    public static ConfigEntry<int> BuildingTypeEntry;
    public static string[] BuildingTypeNames = ["交互塔", "矿物复制塔", "点数聚集塔", "量子复制塔", "点金塔", "分解塔", "转化塔"];

    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;
    public static ConfigEntry<bool>[] EnableFluidOutputStackEntryArr = new ConfigEntry<bool>[7];
    public static ConfigEntry<int>[] MaxProductOutputStackEntryArr = new ConfigEntry<int>[7];
    public static ConfigEntry<bool>[] EnableFracForeverEntryArr = new ConfigEntry<bool>[7];

    private static Text[] textBuildingInfo = new Text[5];
    private static MyCheckBox CheckBoxEnableFluidOutputStack;
    private static MySlider SliderEnableFluidOutputStack;
    private static MyCheckBox CheckBoxEnableFracForever;

    #endregion

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypeNames.Length) {
            RecipeTypeEntry.Value = 0;
        }
        BuildingTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Building Type", 0, "想要查看的建筑类型。");
        if (BuildingTypeEntry.Value < 0 || BuildingTypeEntry.Value >= BuildingTypeNames.Length) {
            BuildingTypeEntry.Value = 0;
        }
        EnableFluidOutputStackEntry = configFile.Bind("TabRecipeAndBuilding", "EnableFluidOutputStack", false);
        MaxProductOutputStackEntry = configFile.Bind("TabRecipeAndBuilding", "MaxProductOutputStack", 1);
        EnableFracForeverEntry = configFile.Bind("TabRecipeAndBuilding", "EnableFracForever", false);

        EnableFluidOutputStackEntryArr[0] = InteractionTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[1] = MineralCopyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[2] = PointAggregateTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[3] = QuantumCopyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[4] = AlchemyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[5] = DeconstructionTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[6] = ConversionTower.EnableFluidOutputStackEntry;

        MaxProductOutputStackEntryArr[0] = InteractionTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[1] = MineralCopyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[2] = PointAggregateTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[3] = QuantumCopyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[4] = AlchemyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[5] = DeconstructionTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[6] = ConversionTower.MaxProductOutputStackEntry;

        EnableFracForeverEntryArr[0] = InteractionTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[1] = MineralCopyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[2] = PointAggregateTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[3] = QuantumCopyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[4] = AlchemyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[5] = DeconstructionTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[6] = ConversionTower.EnableFracForeverEntry;
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "配方&建筑", "tab-group-fe1");
        {
            var tab = wnd.AddTab(trans, "配方详情");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "配方类型").WithItems(RecipeTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(RecipeTypeEntry);
            x = 250f;
            wnd.AddButton(x, y, 200, tab, "切换物品", 16, "button-change-item", OnButtonChangeItemClick);
            x = 0f;
            y += 36f;
            for (int i = 0; i < textRecipeInfo.Length; i++) {
                textRecipeInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"textRecipeInfo{i}");
                y += 20f;
            }
        }
        {
            var tab = wnd.AddTab(trans, "建筑加成");
            x = 0f;
            y = 10f;
            var cbx = wnd.AddComboBox(x, y, tab, "建筑类型").WithItems(BuildingTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(BuildingTypeEntry);
            y += 36f;
            CheckBoxEnableFluidOutputStack =
                wnd.AddCheckBox(x, y, tab, EnableFluidOutputStackEntry, "启用流动输出堆叠");
            wnd.AddTipsButton2(CheckBoxEnableFluidOutputStack.Width + 5f, y + 6f, tab, "启用流动输出堆叠",
                "流动输出尽可能以堆叠形式输出。", "");
            y += 36f;
            var txt = wnd.AddText2(x, y, tab, "产物输出最大堆叠", 15, "text-output-stack");
            SliderEnableFluidOutputStack = wnd.AddSlider(x + txt.preferredWidth + 5f, y + 6f, tab,
                MaxProductOutputStackEntry, new MyWindow.RangeValueMapper<int>(1, 4), "G", 160f);
            y += 36f;
            CheckBoxEnableFracForever = wnd.AddCheckBox(x, y, tab, EnableFracForeverEntry, "启用分馏永动");
            wnd.AddTipsButton2(CheckBoxEnableFracForever.Width + 5f, y + 6f, tab, "启用分馏永动",
                "分馏塔产物输出已满时，原料仍可流动输出，从而让后面的分馏塔继续分馏。",
                "");
            y += 36f;
            for (int i = 0; i < textBuildingInfo.Length; i++) {
                textBuildingInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"textBuildingInfo{i}");
                y += 20f;
            }
        }
    }

    public static void UpdateUI() {
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        string recipeTypeName = RecipeTypeNames[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        int line = 0;
        if (recipe != null) {
            textRecipeInfo[line].text =
                $"{recipeTypeName}-{SelectedItem.name} Lv{recipe.Level} ({recipe.Exp}/{recipe.LevelUpExp})";
            textRecipeInfo[line].color = recipe.QualityColor;
            line++;
            textRecipeInfo[line].text = $"费用 1.00 {SelectedItem.name}";
            line++;
            if (recipeType == ERecipe.QuantumDuplicate) {
                textRecipeInfo[line].text = "     0.01 复制精华";
                line++;
                textRecipeInfo[line].text = "     0.01 点金精华";
                line++;
                textRecipeInfo[line].text = "     0.01 分解精华";
                line++;
                textRecipeInfo[line].text = "     0.01 转化精华";
                line++;
            }
            textRecipeInfo[line].text = $"成功率 {recipe.BaseSuccessRate:P3}    损毁率 {recipe.DestroyRate:P3}";
            line++;
            bool isFirst = true;
            foreach (OutputInfo info in recipe.OutputMain) {
                textRecipeInfo[line].text = $"{(isFirst ? "产出" : "    ")} {info}";
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            isFirst = true;
            foreach (OutputInfo info in recipe.OutputAppend) {
                textRecipeInfo[line].text = $"{(isFirst ? "其他" : "    ")} {info}";
                line++;
                if (isFirst) {
                    isFirst = false;
                }
            }
            textRecipeInfo[line].text = $"配方回响数目：{recipe.MemoryCount}";
            line++;
            if (recipe.Quality >= 7) {
                textRecipeInfo[line].text = "当前配方已到最高品质，无法突破！";
                line++;
            } else {
                textRecipeInfo[line].text = "突破条件：";
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.CanBreakthrough2 ? "√" : "x")}] 等级达到 {recipe.Level} / {3 + recipe.Quality}";
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.CanBreakthrough3 ? "√" : "x")}] 经验达到 {recipe.Exp} / {recipe.MaxLevelUpExp}";
                line++;
                textRecipeInfo[line].text =
                    $"[{(recipe.CanBreakthrough4 ? "√" : "x")}] 拥有 {recipe.MemoryCount} / {recipe.NextQuality} 个对应回响";
                line++;
            }
            textRecipeInfo[line].text = "特殊突破加成：无";
            line++;
        } else {
            textRecipeInfo[line].text = "配方不存在！";
            textRecipeInfo[line].color = BaseRecipe.QualityColors[5];
            line++;
        }
        for (; line < textRecipeInfo.Length; line++) {
            textRecipeInfo[line].text = "";
        }

        line = 0;
        //20%配方达到蓝色（2叠） ---  80%配方达到红色（4叠）
        textBuildingInfo[line].text = "建筑加成：无";
        line++;
        for (; line < textBuildingInfo.Length; line++) {
            textBuildingInfo[line].text = "";
        }
        CheckBoxEnableFluidOutputStack.Checked = EnableFluidOutputStackEntryArr[BuildingTypeEntry.Value].Value;
        EnableFluidOutputStackEntry.Value = EnableFluidOutputStackEntryArr[BuildingTypeEntry.Value].Value;
        SliderEnableFluidOutputStack.Value = MaxProductOutputStackEntryArr[BuildingTypeEntry.Value].Value;
        MaxProductOutputStackEntry.Value = MaxProductOutputStackEntryArr[BuildingTypeEntry.Value].Value;
        CheckBoxEnableFracForever.Checked = EnableFracForeverEntryArr[BuildingTypeEntry.Value].Value;
        EnableFracForeverEntry.Value = EnableFracForeverEntryArr[BuildingTypeEntry.Value].Value;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
