using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.ProgressSystem;
using FE.UI.View.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class FracRecipeOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static Text txtCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick(bool showLocked, float y) {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(SelectedRecipeType, item.ID);
            return recipe != null && (showLocked || recipe.Unlocked);
        });
    }

    private static ConfigEntry<int> RecipeTypeEntry;
    private static ERecipe SelectedRecipeType => RecipeTypes[RecipeTypeEntry.Value];
    private static BaseRecipe SelectedRecipe => GetRecipe<BaseRecipe>(SelectedRecipeType, SelectedItem.ID);

    // ==================== 布局常量 ====================

    private const int InfoLineCount = 28;// 左列文本行数
    private const int LevelLineCount = 13;// 右列: 标题 + +0 到 +10 + 空行
    private const float RightColX = 620f;// 右列X起始位置
    private const float IconSize = 24f;
    private const float TextOffsetWithIcon = 28f;// 图标宽度 + 间距
    private const float LineHeight = 22f;

    // 产物行布局（格式：概率 | 图标 | 名称×数目）
    private const float ProductRatioX = 0f;// 左侧概率文本X
    private const float ProductIconX = 72f;// 物品图标X（概率文本右侧）
    private const float ProductTextX = 100f;// 名称×数目文本X（= ProductIconX + TextOffsetWithIcon）

    // ==================== UI 元素 ====================

    private static Text[] txtRecipeInfo = new Text[InfoLineCount];
    private static Text[] txtProductLeft = new Text[InfoLineCount];// 产物行左侧文本（概率/等效数量）
    private static MyImageButton[] btnRecipeInfoIcons = new MyImageButton[InfoLineCount];
    private static float txtRecipeInfoBaseY;
    private static MySlider incSlider;
    private static ConfigEntry<int> selectedInc;

    // 产物分节标签（动态定位）
    private static Text txtMainLabel;// "产出" 标签
    private static Text txtAppendLabel;// "其他" 标签

    // 右列：配方强化等级信息
    private static Text[] txtLevelInfo = new Text[LevelLineCount];
    private static Text txtAnnealEcho;
    private static Text txtAnnealCost;
    private static UIButton btnAnneal;

    // ==================== 翻译注册 ====================

    public static void AddTranslations() {
        Register("分馏配方", "Fractionate Recipe");

        Register("当前物品", "Current item");
        Register("分馏配方提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("解锁配方", "Unlock recipe");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("成功率", "Success Ratio");
        Register("损毁率", "Destroy Ratio");
        Register("产出", "Output");
        //Register("增产点数", "Proliferator Points"); // 原版已翻译
        //Register("其他", "Others"); // 原版已翻译

        Register("配方已完全升级！", "Recipe has been completely upgraded!");
        Register("每个原料平均产出：", "Average output per raw material:");

        Register("建筑强化加成", "Building Enhancement Bonuses");
        Register("等级", "Level");
        Register("堆叠", "Stack");
        Register("能耗比", "Energy Ratio");
        Register("增产效率", "Proliferator Efficiency");
        Register("流体增强", "Fluid Enhancement");
        Register("成功率加成", "Success Boost");
        Register("已启用", "Enabled");
        Register("未启用", "Disabled");
        Register("牺牲特性", "Sacrifice Trait");
        Register("维度共鸣", "Dimensional Resonance");
        Register("质能裂变", "Mass-Energy Fission");
        Register("零压循环", "Zero Pressure Cycle");
        Register("因果追踪", "Causal Tracing");
        Register("单锁", "Single Lock");
        Register("虚空喷射", "Void Spray");
        Register("双倍点数", "Double Points");
        Register("最大增产等级", "Max Inc Level");

        // 右列：等级信息
        Register("当前配方强化等级", "Current Recipe Enhancement Level");
        Register("配方未解锁", "Recipe Locked");
        Register("无通用加成", "No General Bonus");
        Register("不消耗原料", "No Consume");
        Register("翻倍产出", "Double Output");
        Register("损毁", "Destroy");
        Register("退火", "Anneal");
        Register("回响等级", "Echo Lv");
        Register("退火确认", "Anneal Confirmation");
        Register("退火后配方等级重置为0，获得永久回响加成。", "Recipe level resets to 0 and grants permanent echo bonus.");
        Register("对", "on");
        Register("进行退火？", "to anneal?");
        Register("退火需配方满级", "Anneal requires max-level recipe", "退火需配方满级（+10）");
    }

    // ==================== 配置加载 ====================

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "想要查看的配方类型。");
        if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
            RecipeTypeEntry.Value = 0;
        }
        selectedInc = configFile.Bind("Recipe Operate", "Selected Inc", 0, "想要查看的最终输出的增产点数");
        if (selectedInc.Value is < 0 or > 10) {
            selectedInc.Value = 0;
        }
    }

    // ==================== UI 创建 ====================

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "分馏配方");
        float x = 0f;
        float y = 18f + 7f;

        // 顶部：物品选择器 + 配方类型（移除核心按钮）
        txtCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        float popupY = y + (36f + 7f) / 2;
        btnSelectedItem = wnd.AddImageButton(x + txtCurrItem.preferredWidth + 5, y, tab,
            SelectedItem, "button-change-item").WithClickEvent(
            () => { OnButtonChangeItemClick(false, popupY); },
            () => { OnButtonChangeItemClick(true, popupY); });
        wnd.AddTipsButton2(x + txtCurrItem.preferredWidth + 5 + btnSelectedItem.Width + 5, y, tab,
            "提示", "分馏配方提示按钮说明1");
        var txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "配方类型");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(RecipeTypeShortNames).WithSize(200, 0).WithConfigEntry(RecipeTypeEntry);

        y += 36f + 7f;

        txtAnnealEcho = wnd.AddText2(0f, y, tab, "", 14);
        txtAnnealCost = wnd.AddText2(320f, y, tab, "", 14);
        btnAnneal = wnd.AddButton(620f, y, 160f, tab, "退火".Translate(), 14,
            onClick: OnAnnealClick);

        y += 36f + 7f;

        // 沙盒模式调试按钮
        if (GameMain.sandboxToolsEnabled) {
            wnd.AddButton(0, 4, y, tab, "重置等级",
                onClick: () => { SelectedRecipe?.ChangeLevelTo(0); });
            wnd.AddButton(1, 4, y, tab, "等级-1",
                onClick: () => { SelectedRecipe?.ChangeLevelTo((SelectedRecipe?.Level ?? 0) - 1); });
            wnd.AddButton(2, 4, y, tab, "等级+1",
                onClick: () => { SelectedRecipe?.ChangeLevelTo((SelectedRecipe?.Level ?? 0) + 1); });
            wnd.AddButton(3, 4, y, tab, "等级升满",
                onClick: () => { SelectedRecipe?.ChangeLevelTo(10); });
            y += 36f;
        }

        // 增产点数滑条（动态定位，初始隐藏）
        int[] rang;
        if (!GenesisBook.Enable) {
            rang = [0, 1, 2, 4, 10];
        } else {
            rang = [0, 4, 10];
        }
        incSlider = wnd.AddSlider(0f, 0f, tab, selectedInc, rang, null, 200f);

        txtRecipeInfoBaseY = y;

        // 左列：动态文本行（主文本）
        for (int i = 0; i < InfoLineCount; i++) {
            txtRecipeInfo[i] = wnd.AddText2(x, y, tab, "");
        }
        // 左列：图标按钮
        for (int i = 0; i < InfoLineCount; i++) {
            var btn = MyImageButton.CreateImageButton(0, 0, tab, null);
            btn.WithSize(IconSize, IconSize);
            btn.gameObject.SetActive(false);
            btnRecipeInfoIcons[i] = btn;
        }
        // 左列：产物行左侧文本（概率/等效数量）
        for (int i = 0; i < InfoLineCount; i++) {
            txtProductLeft[i] = MyWindow.AddText(0, 0, tab, "", 15);
            txtProductLeft[i].gameObject.SetActive(false);
        }

        // 产物分节标签（"产出" / "其他"）
        txtMainLabel = MyWindow.AddText(0, 0, tab, "产出", 15);
        txtMainLabel.gameObject.SetActive(false);
        txtAppendLabel = MyWindow.AddText(0, 0, tab, "其他", 15);
        txtAppendLabel.gameObject.SetActive(false);

        // 右列：配方强化等级信息（用较长的初始文本来撑开窗口宽度）
        for (int i = 0; i < LevelLineCount; i++) {
            txtLevelInfo[i] = wnd.AddText2(RightColX, 0f, tab, "", 14);
        }
    }

    // ==================== UI 更新 ====================

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        btnSelectedItem.Proto = SelectedItem;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        ItemProto building = LDB.items.Select(recipeType.GetSpriteItemId());
        RefreshAnnealUI(recipe);

        int line = 0;
        incSlider.gameObject.SetActive(false);

        // 隐藏分节标签
        txtMainLabel.gameObject.SetActive(false);
        txtAppendLabel.gameObject.SetActive(false);

        if (recipe == null) {
            ShowTextLine(line++, "配方不存在！".Translate().WithColor(Red));
        } else if (recipe.Locked) {
            string headerLocked = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            int recipeColor = recipe.MatrixID - I电磁矩阵;
            ShowTextLine(line++, $"{headerLocked.WithColor(recipeColor)} {"分馏配方未解锁".Translate().WithColor(Red)}");
        } else {
            // ---- 左列内容 ----

            // 第1行：配方类型-原料名称（剥离强化等级）
            string headerName = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            ShowTextLine(line++, headerName.WithColor(recipe.MatrixID - I电磁矩阵));
            ShowTextLine(line++, "");// 空行

            float sacrificeBoost = building?.SuccessBoost() ?? 0f;
            float achievementBoost = Achievements.GetSuccessRateBonus();
            float echoBoost = recipe.EchoBonus;
            float galleryBoost = GachaGalleryBonusManager.GetSuccessBonus(recipe.RecipeType);
            float actualSuccessRatio = recipe.SuccessRatio
                                     * (1f + sacrificeBoost)
                                     * (1f + achievementBoost)
                                     * (1f + echoBoost)
                                     * (1f + galleryBoost);
            ShowTextLine(line++,
                $"{"成功率".Translate()} {recipe.SuccessRatio:P3} × {(1f + sacrificeBoost):F3} × {(1f + achievementBoost):F3} × {(1f + echoBoost):F3} × {(1f + galleryBoost):F3} = {actualSuccessRatio:P3}"
                    .WithColor(Orange));
            ShowTextLine(line++,
                $"(献祭 +{sacrificeBoost:P2} / 成就 +{achievementBoost:P2} / 回响 +{echoBoost:P2} / 图鉴 +{galleryBoost:P2})"
                    .WithColor(Gray));

            // 损毁率
            ShowTextLine(line++,
                $"{"损毁率".Translate()} {recipe.DestroyRatio:P3}".WithColor(Red));
            ShowTextLine(line++, "");// 空行

            // 主产物：标签独占一行，下方竖向列表
            if (recipe.OutputMain.Count > 0) {
                float labelY = txtRecipeInfoBaseY + LineHeight * line;
                NormalizeRectWithMidLeft(txtMainLabel, 0, labelY);
                txtMainLabel.gameObject.SetActive(true);
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputMain) {
                ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
            }

            // 副产物：标签独占一行，下方竖向列表
            if (recipe.OutputAppend.Count > 0) {
                float labelY = txtRecipeInfoBaseY + LineHeight * line;
                NormalizeRectWithMidLeft(txtAppendLabel, 0, labelY);
                txtAppendLabel.gameObject.SetActive(true);
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputAppend) {
                ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
            }

            ShowTextLine(line++, "");// 空行

            // 等效处理：增产点数滑条 + 竖向输出列表
            line = ShowEqProcessingSection(line, recipe, building);

            ShowTextLine(line++, "");// 空行

            // 建筑强化效果
            if (building != null) {
                ShowIconLine(line++, building,
                    $"{"建筑强化加成".Translate()} {building.name}  {"等级".Translate()} +{building.Level()}");

                ShowTextLine(line++,
                    $"{"堆叠".Translate()} x{building.MaxStack()}  "
                    + $"{"能耗比".Translate()} {building.EnergyRatio():P0}  "
                    + $"{"增产效率".Translate()} x{building.PlrRatio():F1}");

                float sBoost = building.SuccessBoost();
                ShowTextLine(line++,
                    $"{"成功率加成".Translate()} +{sBoost:P1}"
                        .WithColor(sBoost > 0 ? Orange : Gray));

                bool fluidEnh = building.EnableFluidEnhancement();
                ShowTextLine(line++,
                    $"{"流体增强".Translate()}："
                    + (fluidEnh
                        ? "已启用".Translate().WithColor(Green)
                        : "未启用".Translate().WithColor(Gray)));

                line = ShowBuildingFeatures(line, building);
            }
        }

        // 隐藏剩余左列行
        for (; line < InfoLineCount; line++) {
            HideAllLine(line);
        }

        // 更新右列：配方强化等级表
        UpdateLevelColumn(recipe);
    }

    // ==================== 右列：强化等级表 ====================

    private static void UpdateLevelColumn(BaseRecipe recipe) {
        int currentLevel = recipe == null ? -2 : recipe.Locked ? -1 : recipe.Level;// -2=null, -1=locked, >=0=有效等级

        // 标题行（放在 txtRecipeInfoBaseY）
        string headerText;
        if (recipe == null) {
            headerText = "";
            foreach (Text text in txtLevelInfo) {
                text.text = "";
            }
            return;
        } else if (recipe.Locked) {
            headerText = "配方未解锁".Translate();
        } else {
            headerText = $"{"当前配方强化等级".Translate()} +{currentLevel}";
        }
        txtLevelInfo[0].text = headerText.WithColor(currentLevel >= 0 ? Orange : Red);
        NormalizeRectWithMidLeft(txtLevelInfo[0], RightColX, txtRecipeInfoBaseY);

        // 每个等级（+0 到 +10），从标题下一行开始（避免与标题行重叠）
        for (int lvl = 0; lvl <= 10; lvl++) {
            int lineIdx = lvl + 1;
            string lvlText = GetLevelDescription(lvl);

            string coloredText;
            if (currentLevel < 0) {
                coloredText = lvlText.WithColor(Gray);// 未解锁：全灰
            } else if (lvl == currentLevel) {
                coloredText = lvlText.WithColor(Orange);// 当前等级：橙色高亮
            } else if (lvl < currentLevel) {
                coloredText = lvlText.WithColor(Green);// 已达到：绿色
            } else {
                coloredText = lvlText;// 未达到：默认白色
            }

            txtLevelInfo[lineIdx].text = coloredText;
            // lvl+1 使第一个等级行（+0）位于标题行下方
            float levelY = txtRecipeInfoBaseY + LineHeight * (lvl + 1);
            NormalizeRectWithMidLeft(txtLevelInfo[lineIdx], RightColX, levelY);
        }

        // 末尾空行
        if (LevelLineCount > 12) {
            txtLevelInfo[12].text = "";
        }
    }

    private static string GetLevelDescription(int level) {
        int remainPct = level * 8;
        int doublePct = level * 5;
        string destroyStr = level switch {
            < 7 => "4%",
            7 => "3%",
            8 => "2%",
            9 => "1%",
            _ => "0%"
        };
        if (level == 0) {
            return $"+0  {"无通用加成".Translate()}  {"损毁".Translate()}{destroyStr}";
        }
        string specialNote = level >= 7 ? $"  {"损毁".Translate()}{destroyStr}" : "";
        return $"+{level}  {"不消耗原料".Translate()}{remainPct}%  {"翻倍产出".Translate()}{doublePct}%{specialNote}";
    }

    // ==================== 产物显示（格式：概率 | 图标 | 名称×数目） ====================

    /// <summary>
    /// 显示单个产物行：左侧概率文本，中间物品图标，右侧名称×数目。
    /// </summary>
    private static void ShowProductLine(int line, ItemProto itemProto, OutputInfo info) {
        float lineY = txtRecipeInfoBaseY + LineHeight * line;

        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        string count = forceShow || info.ShowOutputCount ? info.OutputCount.ToString("F3") : "???";
        string name = forceShow || info.ShowOutputName ? itemProto?.name ?? "???" : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? info.SuccessRatio.ToString("P3") : "???";

        // 左侧：概率文本
        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, lineY);
        txtProductLeft[line].gameObject.SetActive(true);

        // 中间：物品图标
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, lineY);

        // 右侧：名称×数目
        txtRecipeInfo[line].text = $"{name}×{count}";
        txtRecipeInfo[line].SetPosition(ProductTextX, lineY);
    }

    // ==================== 等效处理（滑条 + 竖向输出列表） ====================

    private static int ShowEqProcessingSection(int line, BaseRecipe recipe, ItemProto building) {
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = "增产点数".Translate();
        txtRecipeInfo[line].SetPosition(0, txtRecipeInfoBaseY + LineHeight * line);
        incSlider.SetPosition(120, txtRecipeInfoBaseY + LineHeight * line);
        incSlider.gameObject.SetActive(true);
        line++;

        ShowTextLine(line++, "每个原料平均产出：".Translate());

        // E = fracRatio / (1 - fracRatio*r)，其中 fracRatio=(1-d)*s，r=remainInputRatio
        float plrRatio = building?.PlrRatio() ?? 1.0f;
        float pointsBonus = (float)ProcessManager.MaxTableMilli(selectedInc.Value) * plrRatio;
        float successBoost = (building?.SuccessBoost() ?? 0f)
                             + Achievements.GetSuccessRateBonus()
                             + recipe.EchoBonus
                             + GachaGalleryBonusManager.GetSuccessBonus(recipe.RecipeType);
        float successRatio = recipe.SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        float fracRatio = (1 - recipe.DestroyRatio) * successRatio;
        float remainInputRatio = recipe.RemainInputRatio;
        float repeatRatio = fracRatio * remainInputRatio;
        float repeatMultiplier = repeatRatio >= 0.9999f ? 10000.0f : 1.0f / (1.0f - repeatRatio);
        float mainOutputBonus = 1.0f + recipe.DoubleOutputRatio;

        List<(int id, float cnt, bool showName, bool showCount)> outputs = [];
        Dictionary<int, int> outputIndex = [];

        foreach (var info in recipe.OutputMain) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * mainOutputBonus * repeatMultiplier;
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, en, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, en, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, info.ShowOutputName, info.ShowSuccessRatio));
            }
        }
        foreach (var info in recipe.OutputAppend) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * repeatMultiplier;
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, en, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, en, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, info.ShowOutputName, info.ShowSuccessRatio));
            }
        }

        bool showDetails = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;

        foreach (var (id, cnt, showName, showCount) in outputs) {
            float lineY = txtRecipeInfoBaseY + LineHeight * line;
            ItemProto outItem = LDB.items.Select(id);
            string outName = showDetails || showName ? outItem?.name ?? "???" : "???";
            string outCount = showDetails || showCount ? cnt.ToString("F3") : "???";

            txtProductLeft[line].gameObject.SetActive(false);

            btnRecipeInfoIcons[line].gameObject.SetActive(true);
            btnRecipeInfoIcons[line].Proto = outItem;
            NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, lineY);

            txtRecipeInfo[line].text = $"{outName}×{outCount}";
            txtRecipeInfo[line].SetPosition(ProductTextX, lineY);

            line++;
        }

        return line;
    }

    // ==================== 建筑特殊特质 ====================

    private static int ShowBuildingFeatures(int line, ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                ShowTextLine(line++,
                    $"{"牺牲特性".Translate()}：{FeatureStatus(InteractionTower.EnableSacrificeTrait)}  "
                    + $"{"维度共鸣".Translate()}：{FeatureStatus(InteractionTower.EnableDimensionalResonance)}");
                break;
            case IFE矿物复制塔:
                ShowTextLine(line++,
                    $"{"质能裂变".Translate()}：{FeatureStatus(MineralReplicationTower.EnableMassEnergyFission)}  "
                    + $"{"零压循环".Translate()}：{FeatureStatus(MineralReplicationTower.EnableZeroPressureCycle)}");
                break;
            case IFE转化塔:
                ShowTextLine(line++,
                    $"{"因果追踪".Translate()}：{FeatureStatus(ConversionTower.EnableCausalTracing)}  "
                    + $"{"单锁".Translate()}：{FeatureStatus(ConversionTower.EnableSingleLock)}");
                break;
            case IFE点数聚集塔:
                ShowTextLine(line++,
                    $"{"虚空喷射".Translate()}：{FeatureStatus(PointAggregateTower.EnableVoidSpray)}  "
                    + $"{"双倍点数".Translate()}：{FeatureStatus(PointAggregateTower.EnableDoublePoints)}  "
                    + $"{"最大增产等级".Translate()} {PointAggregateTower.MaxInc}");
                break;
        }
        return line;
    }

    // ==================== 辅助显示方法 ====================

    private static void ShowIconLine(int line, ItemProto itemProto, string text) {
        float lineY = txtRecipeInfoBaseY + LineHeight * line;
        txtProductLeft[line].gameObject.SetActive(false);
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], 0, lineY);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(TextOffsetWithIcon, lineY);
    }

    private static void ShowTextLine(int line, string text) {
        float lineY = txtRecipeInfoBaseY + LineHeight * line;
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(0, lineY);
    }

    private static void HideIconOnLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
    }

    private static void HideAllLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
        txtProductLeft[line].gameObject.SetActive(false);
        txtProductLeft[line].text = "";
        txtRecipeInfo[line].text = "";
        txtRecipeInfo[line].SetPosition(0, 0);
    }

    private static string FeatureStatus(bool enabled) =>
        enabled ? "已启用".Translate().WithColor(Green) : "未启用".Translate().WithColor(Gray);

    private static void RefreshAnnealUI(BaseRecipe recipe) {
        if (txtAnnealEcho != null) {
            txtAnnealEcho.text = recipe == null
                ? ""
                : $"{"回响等级".Translate()}: {recipe.EchoLevel}";
        }
        if (txtAnnealCost != null) {
            string costText = "";
            if (recipe != null) {
                var (itemId, itemCount) = recipe.GetAnnealCost();
                string itemName = LDB.items.Select(itemId)?.name ?? itemId.ToString();
                costText = $"{"消耗".Translate()} {itemName} x{itemCount}";
            }
            txtAnnealCost.text = recipe != null && recipe.IsMaxLevel
                ? costText
                : "退火需配方满级".Translate();
        }
        if (btnAnneal != null && btnAnneal.button != null) {
            btnAnneal.button.interactable = recipe != null && recipe.IsMaxLevel;
        }
    }

    private static void OnAnnealClick() {
        BaseRecipe recipe = SelectedRecipe;
        if (recipe == null || !recipe.IsMaxLevel) return;
        var (costItemId, costItemCount) = recipe.GetAnnealCost();
        string costItemName = LDB.items.Select(costItemId)?.name ?? costItemId.ToString();
        string consumeLabel = "消耗".Translate();
        string onLabel = "对".Translate();
        string annealQuestion = "进行退火？".Translate();
        string annealTip = "退火后配方等级重置为0，获得永久回响加成。".Translate();
        UIMessageBox.Show("退火确认".Translate(),
            $"{consumeLabel} {costItemName} x{costItemCount} {onLabel} {recipe.TypeName} {annealQuestion}\n{annealTip}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(costItemId, costItemCount, out _)) return;
                recipe.Anneal();
                RefreshAnnealUI(recipe);
            }, null);
    }

    #region IModCanSave

    // ==================== 存档 ====================

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
