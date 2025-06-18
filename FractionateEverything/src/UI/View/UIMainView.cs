using System.Text;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.ViewModel.UIMainViewModel;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.BaseRecipe;

namespace FE.UI.View;

public static class UIMainView {
    public static void Init() {
        // I18NUtils.Register("key", "en", "cn");
        MyConfigWindow.OnUICreated += CreateUI;
        MyConfigWindow.OnUpdateUI += UpdateUI;
    }

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(1, 100) {
        public override float IndexToValue(int index) => index / 10.0f;
        public override int ValueToIndex(float value) => Mathf.RoundToInt(value * 10);

        // public override string FormatValue(string format, float value) {
        //     return value == 0 ? "max".Translate() : base.FormatValue(format, value);
        // }
    }

    private class OutputStackMapper() : MyWindow.RangeValueMapper<float>(1, 4) {
        public override float IndexToValue(int index) => index;
        public override int ValueToIndex(float value) => Mathf.RoundToInt(value);
    }

    private class AutoConfigDispenserChargePowerMapper() : MyWindow.RangeValueMapper<int>(3, 30) {
        public override string FormatValue(string format, int value) {
            var sb = new StringBuilder("         ");
            StringBuilderUtility.WriteKMG(sb, 8, value * 300000L, false);
            sb.Append('W');
            return sb.ToString().Trim();
        }
    }

    public static Text[] textRecipeInfo = new Text[20];
    public static Text[] textBuildingInfo = new Text[20];

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        Text txt;
        float x;
        float y;
        {
            wnd.AddTabGroup(trans, "配方&建筑", "tab-group-fe1");
            {
                var tab = wnd.AddTab(trans, "配方详情");
                x = 0f;
                y = 10f;
                wnd.AddComboBox(x, y, tab, "配方类型")
                    .WithItems("矿物复制", "量子复制", "点金", "分解", "转化")
                    .WithSize(150f, 0f)
                    .WithConfigEntry(RecipeTypeEntry);
                x = 250f;
                wnd.AddButton(x, y, 200, tab, "切换物品", 16, "button-change-item", OnButtonChangeItemClick);
                x = 0f;
                y += 36f;
                for (int i = 0; i < textRecipeInfo.Length; i++) {
                    textRecipeInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"textRecipeInfo{i}");
                    y += 36f;
                }
            }
            {
                var tab = wnd.AddTab(trans, "建筑加成");
                x = 0f;
                y = 10f;
                wnd.AddComboBox(x, y, tab, "建筑类型")
                    .WithItems("交互塔", "矿物复制塔", "点数聚集塔", "量子复制塔", "点金塔", "分解塔", "转化塔")
                    .WithSize(150f, 0f)
                    .WithConfigEntry(BuildingTypeEntry);
                y += 36f;
                txt = wnd.AddText2(x, y, tab, "产物输出堆叠", 15, "text-output-stack");
                wnd.AddSlider(x + txt.preferredWidth + 5f, y + 6f, tab,
                    OutputStackEntry, new OutputStackMapper(), "G", 160f);
                y += 36f;
                for (int i = 0; i < textBuildingInfo.Length; i++) {
                    textBuildingInfo[i] = wnd.AddText2(x, y, tab, "", 15, $"textBuildingInfo{i}");
                    y += 36f;
                }
            }
        }
        {
            wnd.AddTabGroup(trans, "抽卡", "tab-group-fe2");
            {
                var tab = wnd.AddTab(trans, "抽卡");
                x = 0f;
                y = 10f;
                wnd.AddComboBox(x, y, tab, "卡池")
                    .WithItems("矿物复制", "量子复制", "点金", "分解", "转化")
                    .WithSize(150f, 0f)
                    .WithConfigEntry(BuildingTypeEntry);
                y += 30f;

                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe1-1",
                    () => Raffle(ERecipe.MineralCopy, 1));
                wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-get-recipe1-10",
                    () => Raffle(ERecipe.MineralCopy, 10));
            }
        }
        {
            wnd.AddTabGroup(trans, "商店", "tab-group-fe3");
            {
                var tab = wnd.AddTab(trans, "配方");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "黑雾");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
        }
        {
            wnd.AddTabGroup(trans, "成就", "tab-group-fe4");
        }
        {
            wnd.AddTabGroup(trans, "彩蛋", "tab-group-fe5");
        }
        {
            wnd.AddTabGroup(trans, "其他", "tab-group-fe6");
            {
                var tab = wnd.AddTab(trans, "其他");
                x = 0f;
                y = 10f;
                var checkBox = wnd.AddCheckBox(x, y, tab, EnableGod, "启用上帝模式(未实装)");
                wnd.AddTipsButton2(checkBox.Width + 5f, y + 6f, tab, "启用上帝模式", "可以大幅提升经验获取速度。", "");
                y += 36f;
                wnd.AddButton(x, y, 200, tab, "解锁所有配方", 16, "button-unlock-all-recipes",
                    UnlockAll);
                y += 30f;
                txt = wnd.AddText2(x, y, tab, "处理倍率(未实装)", 15, "text-multi-rate");
                wnd.AddSlider(x + txt.preferredWidth + 5f, y + 6f, tab,
                    MultiRateEntry, new MultiRateMapper(), "G", 160f);
                y += 30f;

            }
        }

        //     wnd.AddText2(x + 2f, y, tab1, "Default profile name", 15, "text-default-profile-name");
        //     y += 24f;
        //     wnd.AddInputField(x + 2f, y, 200f, tab1, FractionateEverything.DefaultProfileName, 15, "input-profile-save-folder");
        //     y += 18f;

        // for (var i = 0; i < 10; i++)
        // {
        //     var id = i + 1;
        //     var btn = wnd.AddFlatButton(x, y, tab5, id.ToString(), 12, "dismantle-layer-" + id, () =>
        //         {
        //             var star = DysonSphereFunctions.CurrentStarForDysonSystem();
        //             UIMessageBox.Show("Dismantle selected layer".Translate(), "Dismantle selected layer Confirm".Translate(), "取消".Translate(), "确定".Translate(), 2, null,
        //                 () => { DysonSphereFunctions.InitCurrentDysonLayer(star, id); });
        //         }
        //     ).WithSize(40f, 20f);
        //     DysonLayerBtn[i] = btn.uiButton;
        //     if (i == 4)
        //     {
        //         x -= 160f;
        //         y += 20f;
        //     }
        //     else
        //     {
        //         x += 40f;
        //     }
        // }
    }

    private static void UpdateUI() {
        ERecipe recipeType = RecipeTypeToERecipe();
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, InputItem.ID);
        int line = 0;
        if (recipe != null) {
            textRecipeInfo[line].text =
                $"{RecipeTypeToStr()}-{InputItem.name} Lv{recipe.Level} ({recipe.Experience}/{recipe.NextLevelExperience})";
            textRecipeInfo[line].color = recipe.QualityColor;
            line++;
            textRecipeInfo[line].text = $"费用 1.00 {InputItem.name}";
            line++;
            if (recipeType == ERecipe.QuantumDuplicate) {
                textRecipeInfo[line].text = $"     0.01 复制精华";
                line++;
                textRecipeInfo[line].text = $"     0.01 点金精华";
                line++;
                textRecipeInfo[line].text = $"     0.01 分解精华";
                line++;
                textRecipeInfo[line].text = $"     0.01 转化精华";
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
            textRecipeInfo[line].text = $"突破加成：无";
            line++;
        } else {
            textRecipeInfo[line].text = $"配方不存在！";
            textRecipeInfo[line].color = QualityColors[5];
            line++;
        }
        for (; line < textRecipeInfo.Length; line++) {
            textRecipeInfo[line].text = "";
        }

        line = 0;
        textBuildingInfo[line].text = $"建筑加成：无";
        line++;
        for (; line < textBuildingInfo.Length; line++) {
            textBuildingInfo[line].text = "";
        }
    }
}
