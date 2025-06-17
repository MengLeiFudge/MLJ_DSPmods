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
    private static RectTransform _windowTrans;
    private static RectTransform _dysonTab;
    private static UIButton _dysonInitBtn;
    private static readonly UIButton[] DysonLayerBtn = new UIButton[10];

    public static void Init() {
        // I18NUtils.Register("key", "en", "cn");
        MyConfigWindow.OnUICreated += CreateUI;
        MyConfigWindow.OnUpdateUI += UpdateUI;
    }

    private class MultiRateMapper : MyWindow.ValueMapper<float> {
        public override int Min => 1;//0.1
        public override int Max => 100;//10

        public override float IndexToValue(int index) => index / 10.0f;
        public override int ValueToIndex(float value) => Mathf.RoundToInt(value * 10);

        // public override string FormatValue(string format, float value) {
        //     return value == 0 ? "max".Translate() : base.FormatValue(format, value);
        // }
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
            wnd.AddTabGroup(trans, "配方", "tab-group-fe1");
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
                    .WithItems("交互塔", "矿物复制塔", "点数聚集塔", "量子复制塔", "点金塔", "分解塔", "转化塔")
                    .WithSize(150f, 0f)
                    .WithConfigEntry(BuildingTypeEntry);

                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe1-1",
                    () => Raffle(ERecipe.MineralCopy, 1));
                y += 30f;
                wnd.AddButton(x, y, 200, tab, "十连", 16, "button-get-recipe1-10",
                    () => Raffle(ERecipe.MineralCopy, 10));
            }
            {
                var tab = wnd.AddTab(trans, "量子复制");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe2-1",
                    () => Raffle(ERecipe.QuantumDuplicate, 1));
                y += 30f;
                wnd.AddButton(x, y, 200, tab, "十连", 16, "button-get-recipe2-10",
                    () => Raffle(ERecipe.QuantumDuplicate, 10));
            }
            {
                var tab = wnd.AddTab(trans, "点金");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe3-1",
                    () => Raffle(ERecipe.Alchemy, 1));
                y += 30f;
                wnd.AddButton(x, y, 200, tab, "十连", 16, "button-get-recipe3-10",
                    () => Raffle(ERecipe.Alchemy, 10));
            }
            {
                var tab = wnd.AddTab(trans, "分解");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe4-1",
                    () => Raffle(ERecipe.Deconstruction, 1));
                y += 30f;
                wnd.AddButton(x, y, 200, tab, "十连", 16, "button-get-recipe4-10",
                    () => Raffle(ERecipe.Deconstruction, 10));
            }
            {
                var tab = wnd.AddTab(trans, "转化");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe5-1",
                    () => Raffle(ERecipe.Conversion, 1));
                y += 30f;
                wnd.AddButton(x, y, 200, tab, "十连", 16, "button-get-recipe5-10",
                    () => Raffle(ERecipe.Conversion, 10));
            }
        }
        {
            wnd.AddTabGroup(trans, "商店", "tab-group-fe3");
            {
                var tab = wnd.AddTab(trans, "蓝糖");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "红糖");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "黄糖");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "紫糖");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "绿糖");
                x = 0f;
                y = 10f;
                wnd.AddButton(x, y, 300, tab, "——未知按钮——", 16, "button-unknown",
                    null);
            }
            {
                var tab = wnd.AddTab(trans, "白糖");
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
                var checkBox = wnd.AddCheckBox(x, y, tab, FractionateEverything.EnableGod, "启用上帝模式未实装");
                wnd.AddTipsButton2(checkBox.Width + 5f, y + 6f, tab, "启用上帝模式", "可以大幅提升经验获取速度。", "");
                y += 36f;
                wnd.AddButton(x, y, 200, tab, "解锁所有配方", 16, "button-unlock-all-recipes",
                    UnlockAll);
                y += 30f;
                txt = wnd.AddText2(x + 10f, y, tab, "处理倍率未实装", 15, "text-multi-rate");
                wnd.AddSlider(x + 10f + txt.preferredWidth + 5f, y + 6f, tab,
                    FractionateEverything.MultiRate, new MultiRateMapper(), "G", 160f);
                y += 30f;
            }
        }

        // wnd.AddTipsButton2(x, y, tab1, "Better auto-save mechanism", "Better auto-save mechanism tips", "auto-save-opt-tips");
        // x = 0f;
        // */
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab1, FractionateEverything.ConvertSavesFromPeaceEnabled, "Convert old saves to Combat Mode on loading");
        // MyCheckBox checkBoxForMeasureTextWidth;
        // if (WindowFunctions.ProfileName != null)
        // {
        //     y += 36f;
        //     checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab1, FractionateEverything.ProfileBasedSaveFolderEnabled, "Profile-based save folder");
        //     wnd.AddTipsButton2(checkBoxForMeasureTextWidth.Width + 5f, y + 6f, tab1, "Profile-based save folder", "Profile-based save folder tips", "btn-profile-based-save-folder-tips");
        //     y += 36f;
        //     checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab1, FractionateEverything.ProfileBasedOptionEnabled, "Profile-based option");
        //     wnd.AddTipsButton2(checkBoxForMeasureTextWidth.Width + 5f, y + 6f, tab1, "Profile-based option", "Profile-based option tips", "btn-profile-based-option-tips");
        //     y += 36f;
        //     wnd.AddText2(x + 2f, y, tab1, "Default profile name", 15, "text-default-profile-name");
        //     y += 24f;
        //     wnd.AddInputField(x + 2f, y, 200f, tab1, FractionateEverything.DefaultProfileName, 15, "input-profile-save-folder");
        //     y += 18f;
        // }
        // y += 36f;
        // wnd.AddComboBox(x + 2f, y, tab1, "Process priority").WithItems("High", "Above Normal", "Normal", "Below Normal", "Idle").WithSize(100f, 0f).WithConfigEntry(WindowFunctions.ProcessPriority);
        // var details = WindowFunctions.ProcessorDetails;
        // string[] affinities;
        // if (details.HybridArchitecture)
        // {
        //     affinities = new string[5];
        //     affinities[3] = "All P-Cores";
        //     affinities[4] = "All E-Cores";
        // }
        // else
        // {
        //     affinities = new string[3];
        // }
        // affinities[0] = "All CPUs";
        // affinities[1] = string.Format("First {0} CPUs".Translate(), details.ThreadCount / 2);
        // affinities[2] = details.ThreadCount > 16 ? "First 8 CPUs" : "First CPU only";
        // y += 36f;
        // wnd.AddComboBox(x + 2f, y, tab1, "Enabled CPUs").WithItems(affinities).WithSize(200f, 0f).WithConfigEntry(WindowFunctions.ProcessAffinity);
        // y += 36f;
        // ((RectTransform)wnd.AddButton(x, y, tab1, "CPU Info", 16, "button-show-cpu-info", WindowFunctions.ShowCPUInfo).transform).sizeDelta = new Vector2(100f, 25f);
        //
        // var tab2 = wnd.AddTab(trans, "Factory");
        // x = 0f;
        // y = 10f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.RemoveSomeConditionEnabled, "Remove some build conditions");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.RemoveBuildRangeLimitEnabled, "Remove build range limit");
        // y += 36f;
        // checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab2, FractionateEverything.NightLightEnabled, "Night Light");
        // x += checkBoxForMeasureTextWidth.Width + 5f + 10f;
        // txt = wnd.AddText2(x, y + 2f, tab2, "Angle X:", 13, "text-nightlight-angle-x");
        // x += txt.preferredWidth + 5f;
        // wnd.AddSlider(x, y + 7f, tab2, FractionateEverything.NightLightAngleX, new AngleMapper(), "0", 60f).WithSmallerHandle();
        // x += 70f;
        // txt = wnd.AddText2(x, y + 2f, tab2, "Y:", 13, "text-nightlight-angle-y");
        // wnd.AddSlider(x + txt.preferredWidth + 5f, y + 7f, tab2, FractionateEverything.NightLightAngleY, new AngleMapper(), "0", 60f).WithSmallerHandle();
        // x = 0;
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.LargerAreaForUpgradeAndDismantleEnabled, "Larger area for upgrade and dismantle");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.LargerAreaForTerraformEnabled, "Larger area for terraform");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.OffGridBuildingEnabled, "Off-grid building and stepped rotation");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.CutConveyorBeltEnabled, "Cut conveyor belt (with shortcut key)");
        // y += 36f;
        // checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab2, FractionateEverything.TreatStackingAsSingleEnabled, "Treat stack items as single in monitor components");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab2, FractionateEverything.QuickBuildAndDismantleLabsEnabled, "Quick build and dismantle stacking labs");
        //
        // {
        //     y += 36f;
        //     var cb = wnd.AddCheckBox(x, y, tab2, FractionateEverything.TankFastFillInAndTakeOutEnabled, "Fast fill in to and take out from tanks");
        //     x += cb.Width + 5f;
        //     txt = wnd.AddText2(x, y + 2f, tab2, "Speed Ratio", 13, "text-tank-fast-fill-speed-ratio");
        //     var tankSlider = wnd.AddSlider(x + txt.preferredWidth + 5f, y + 7f, tab2, FractionateEverything.TankFastFillInAndTakeOutMultiplier, [2, 5, 10, 20, 50, 100, 500, 1000], "G", 100f).WithSmallerHandle();
        //     FractionateEverything.TankFastFillInAndTakeOutEnabled.SettingChanged += TankSettingChanged;
        //     wnd.OnFree += () => { FractionateEverything.TankFastFillInAndTakeOutEnabled.SettingChanged -= TankSettingChanged; };
        //     TankSettingChanged(null, null);
        //
        //     void TankSettingChanged(object o, EventArgs e)
        //     {
        //         tankSlider.SetEnable(FactoryPatch.TankFastFillInAndTakeOutEnabled.Value);
        //     }
        // }
        //
        // x = 0;
        // y += 72f;
        // wnd.AddButton(x, y, 200, tab2, "Quick build Orbital Collectors", 16, "button-init-planet", PlanetFunctions.BuildOrbitalCollectors);
        // y += 30f;
        // txt = wnd.AddText2(x + 10f, y, tab2, "Maximum count to build", 15, "text-oc-build-count");
        // wnd.AddSlider(x + 10f + txt.preferredWidth + 5f, y + 6f, tab2, FractionateEverything.OrbitalCollectorMaxBuildCount, new TestValueMapper(), "G", 160f);
        //
        // y += 18f;
        //
        // {
        //     y += 36f;
        //     wnd.AddCheckBox(x, y, tab2, FractionateEverything.TweakBuildingBufferEnabled, "Tweak building buffers");
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Assembler buffer time multiplier(in seconds)", 13);
        //     var nx1 = txt.preferredWidth + 5f;
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Assembler buffer minimum multiplier", 13);
        //     var nx2 = txt.preferredWidth + 5f;
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Buffer count for assembling in labs", 13);
        //     var nx3 = txt.preferredWidth + 5f;
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Extra buffer count for Self-evolution Labs", 13);
        //     var nx4 = txt.preferredWidth + 5f;
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Buffer count for researching in labs", 13);
        //     var nx5 = txt.preferredWidth + 5f;
        //     y += 27f;
        //     txt = wnd.AddText2(x + 20f, y, tab2, "Ray Receiver Graviton Lens buffer count", 13);
        //     var nx6 = txt.preferredWidth + 5f;
        //     y -= 135f;
        //     var mx = Mathf.Max(nx1, nx2, nx3, nx4, nx5, nx6) + 20f;
        //     var assemblerBufferTimeMultiplierSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.AssemblerBufferTimeMultiplier, new MyWindow.RangeValueMapper<int>(2, 10), "0", 80f).WithSmallerHandle();
        //     y += 27f;
        //     var assemblerBufferMininumMultiplierSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.AssemblerBufferMininumMultiplier, new MyWindow.RangeValueMapper<int>(2, 10), "0", 80f).WithSmallerHandle();
        //     y += 27f;
        //     var labBufferMaxCountForAssembleSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.LabBufferMaxCountForAssemble, new MyWindow.RangeValueMapper<int>(2, 20), "0", 80f).WithSmallerHandle();
        //     y += 27f;
        //     var labBufferExtraCountForAdvancedAssembleSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.LabBufferExtraCountForAdvancedAssemble, new MyWindow.RangeValueMapper<int>(1, 10), "0", 80f).WithSmallerHandle();
        //     y += 27f;
        //     var labBufferMaxCountForResearchSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.LabBufferMaxCountForResearch, new MyWindow.RangeValueMapper<int>(2, 20), "0", 80f).WithSmallerHandle();
        //     y += 27f;
        //     var receiverBufferCountSlider = wnd.AddSlider(x + mx, y + 5f, tab2, FactoryPatch.ReceiverBufferCount, new MyWindow.RangeValueMapper<int>(1, 20), "0", 80f).WithSmallerHandle();
        //     FactoryPatch.TweakBuildingBufferEnabled.SettingChanged += TweakBuildingBufferChanged;
        //     wnd.OnFree += () => { FactoryPatch.TweakBuildingBufferEnabled.SettingChanged -= TweakBuildingBufferChanged; };
        //     TweakBuildingBufferChanged(null, null);
        //
        //     void TweakBuildingBufferChanged(object o, EventArgs e)
        //     {
        //         assemblerBufferTimeMultiplierSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //         assemblerBufferMininumMultiplierSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //         labBufferMaxCountForAssembleSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //         labBufferExtraCountForAdvancedAssembleSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //         labBufferMaxCountForResearchSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //         receiverBufferCountSlider.SetEnable(FactoryPatch.TweakBuildingBufferEnabled.Value);
        //     }
        // }
        //
        // var tab3 = wnd.AddTab(trans, "Logistics");
        // x = 0f;
        // y = 10f;
        //
        // checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab3, LogisticsPatch.LogisticsCapacityTweaksEnabled, "Enhance control for logistic storage capacities");
        // wnd.AddTipsButton2(x + checkBoxForMeasureTextWidth.Width + 5f, y + 6f, tab3, "Enhance control for logistic storage capacities", "Enhance control for logistic storage capacities tips", "enhanced-logistic-capacities-tips");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab3, LogisticsPatch.AllowOverflowInLogisticsEnabled, "Allow overflow for Logistic Stations and Advanced Mining Machines");
        // y += 36f;
        // checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab3, LogisticsPatch.LogisticsConstrolPanelImprovementEnabled, "Logistics Control Panel Improvement");
        // wnd.AddTipsButton2(x + checkBoxForMeasureTextWidth.Width + 5f, y + 6f, tab3, "Logistics Control Panel Improvement", "Logistics Control Panel Improvement tips", "lcp-improvement-tips");
        // {
        //     y += 36f;
        //     var realtimeLogisticsInfoPanelCheckBox = wnd.AddCheckBox(x, y, tab3, LogisticsPatch.RealtimeLogisticsInfoPanelEnabled, "Real-time logistic stations info panel");
        //     y += 27f;
        //     var realtimeLogisticsInfoPanelBarsCheckBox = wnd.AddCheckBox(x + 20f, y, tab3, LogisticsPatch.RealtimeLogisticsInfoPanelBarsEnabled, "Show status bars for storage items", 13);
        //     if (AuxilaryfunctionWrapper.ShowStationInfo != null)
        //     {
        //         AuxilaryfunctionWrapper.ShowStationInfo.SettingChanged += RealtimeLogisticsInfoPanelChanged;
        //         wnd.OnFree += () => { AuxilaryfunctionWrapper.ShowStationInfo.SettingChanged -= RealtimeLogisticsInfoPanelChanged; };
        //     }
        //     LogisticsPatch.RealtimeLogisticsInfoPanelEnabled.SettingChanged += RealtimeLogisticsInfoPanelChanged;
        //     wnd.OnFree += () => { LogisticsPatch.RealtimeLogisticsInfoPanelEnabled.SettingChanged -= RealtimeLogisticsInfoPanelChanged; };
        //     RealtimeLogisticsInfoPanelChanged(null, null);
        //
        //     void RealtimeLogisticsInfoPanelChanged(object o, EventArgs e)
        //     {
        //         if (AuxilaryfunctionWrapper.ShowStationInfo == null)
        //         {
        //             realtimeLogisticsInfoPanelCheckBox.SetEnable(true);
        //             realtimeLogisticsInfoPanelBarsCheckBox.SetEnable(LogisticsPatch.RealtimeLogisticsInfoPanelEnabled.Value);
        //             return;
        //         }
        //
        //         var on = !AuxilaryfunctionWrapper.ShowStationInfo.Value;
        //         realtimeLogisticsInfoPanelCheckBox.SetEnable(on);
        //         realtimeLogisticsInfoPanelBarsCheckBox.SetEnable(on & LogisticsPatch.RealtimeLogisticsInfoPanelEnabled.Value);
        //         if (!on)
        //         {
        //             LogisticsPatch.RealtimeLogisticsInfoPanelEnabled.Value = false;
        //         }
        //     }
        // }
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab3, LogisticsPatch.AutoConfigLogisticsEnabled, "Auto-config logistic stations");
        // y += 24f;
        // wnd.AddCheckBox(10f, y, tab3, LogisticsPatch.AutoConfigLimitAutoReplenishCount, "Limit auto-replenish count to values below", 13).WithSmallerBox();
        // y += 18f;
        // var maxWidth = 0f;
        // wnd.AddText2(10f, y, tab3, "Dispenser", 14, "text-dispenser");
        // y += 18f;
        // var oy = y;
        // x = 20f;
        // var textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Max. Charging Power", 13, "text-dispenser-max-charging-power");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Count of Bots filled", 13, "text-dispenser-count-of-bots-filled");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // wnd.AddText2(10f, y, tab3, "Battlefield Analysis Base", 14, "text-battlefield-analysis-base");
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Max. Charging Power", 13, "text-battlefield-analysis-base-max-charging-power");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // wnd.AddText2(10f, y, tab3, "PLS", 14, "text-pls");
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Max. Charging Power", 13, "text-pls-max-charging-power");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Drone transport range", 13, "text-pls-drone-transport-range");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Min. Load of Drones", 13, "text-pls-min-load-of-drones");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Outgoing integration count", 13, "text-pls-outgoing-integration-count");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Count of Drones filled", 13, "text-pls-count-of-drones-filled");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // wnd.AddText2(10f, y, tab3, "ILS", 14, "text-ils");
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Max. Charging Power", 13, "text-ils-max-charging-power");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Drone transport range", 13, "text-ils-drone-transport-range");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Vessel transport range", 13, "text-ils-vessel-transport-range");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // wnd.AddCheckBox(x + 360f, y + 6f, tab3, LogisticsPatch.AutoConfigILSIncludeOrbitCollector, "Include Orbital Collector", 13).WithSmallerBox();
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Warp distance", 13, "text-ils-warp-distance");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // wnd.AddCheckBox(x + 360f, y + 6f, tab3, LogisticsPatch.AutoConfigILSWarperNecessary, "Warpers required", 13).WithSmallerBox();
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Min. Load of Drones", 13, "text-ils-min-load-of-drones");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Min. Load of Vessels", 13, "text-ils-min-load-of-vessels");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Outgoing integration count", 13, "text-ils-outgoing-integration-count");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Count of Drones filled", 13, "text-ils-count-of-drones-filled");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Count of Vessels filled", 13, "text-ils-count-of-vessels-filled");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // wnd.AddText2(10f, y, tab3, "Advanced Mining Machine", 14, "text-amm");
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Collecting Speed", 13, "text-amm-collecting-speed");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y += 18f;
        // textForMeasureTextWidth = wnd.AddText2(x, y, tab3, "Min. Piler Value", 13, "text-amm-min-piler-value");
        // maxWidth = Mathf.Max(maxWidth, textForMeasureTextWidth.preferredWidth);
        // y = oy + 1;
        // var nx = x + maxWidth + 5f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigDispenserChargePower, new AutoConfigDispenserChargePowerMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigDispenserCourierCount, new MyWindow.RangeValueMapper<int>(0, 10), "G", 150f, -100f).WithFontSize(13);
        // y += 36f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigBattleBaseChargePower, new AutoConfigBattleBaseChargePowerMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 36f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigPLSChargePower, new AutoConfigPLSChargePowerMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigPLSMaxTripDrone, new MyWindow.RangeValueMapper<int>(1, 180), "0°", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigPLSDroneMinDeliver, new AutoConfigCarrierMinDeliverMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigPLSMinPilerValue, new AutoConfigMinPilerValueMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigPLSDroneCount, new MyWindow.RangeValueMapper<int>(0, 50), "G", 150f, -100f).WithFontSize(13);
        // y += 36f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSChargePower, new AutoConfigILSChargePowerMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSMaxTripDrone, new MyWindow.RangeValueMapper<int>(1, 180), "0°", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSMaxTripShip, new AutoConfigILSMaxTripShipMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSWarperDistance, new AutoConfigILSWarperDistanceMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSDroneMinDeliver, new AutoConfigCarrierMinDeliverMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSShipMinDeliver, new AutoConfigCarrierMinDeliverMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSMinPilerValue, new AutoConfigMinPilerValueMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSDroneCount, new MyWindow.RangeValueMapper<int>(0, 100), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigILSShipCount, new MyWindow.RangeValueMapper<int>(0, 10), "G", 150f, -100f).WithFontSize(13);
        // y += 36f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigVeinCollectorHarvestSpeed, new AutoConfigVeinCollectorHarvestSpeedMapper(), "G", 150f, -100f).WithFontSize(13);
        // y += 18f;
        // wnd.AddSideSlider(nx, y, tab3, LogisticsPatch.AutoConfigVeinCollectorMinPilerValue, new AutoConfigMinPilerValueMapper(), "G", 150f, -100f).WithFontSize(13);
        // x = 0f;
        //
        // var tab4 = wnd.AddTab(trans, "Player/Mecha");
        // x = 0f;
        // y = 10f;
        // wnd.AddCheckBox(x, y, tab4, FactoryPatch.UnlimitInteractiveEnabled, "Unlimited interactive range");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab4, PlanetPatch.PlayerActionsInGlobeViewEnabled, "Enable player actions in globe view");
        // y += 36f;
        // wnd.AddCheckBox(x, y, tab4, PlayerPatch.HideTipsForSandsChangesEnabled, "Hide tips for soil piles changes");
        // y += 36f;
        // checkBoxForMeasureTextWidth = wnd.AddCheckBox(x, y, tab4, PlayerPatch.EnhancedMechaForgeCountControlEnabled, "Enhanced count control for hand-make");
        // wnd.AddTipsButton2(x + checkBoxForMeasureTextWidth.Width + 5f, y + 6f, tab4, "Enhanced count control for hand-make", "Enhanced count control for hand-make tips", "enhanced-count-control-tips");
        // y += 36f;

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
