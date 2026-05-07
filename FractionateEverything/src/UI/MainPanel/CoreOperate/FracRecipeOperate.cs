using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Buildings;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Gacha;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.Setting;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Logic.Fractionation.FracRecipes.ERecipeExtension;
using static FE.Utils.Utils;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 分馏配方等级、经验和解锁操作页面。
/// </summary>
public static class FracRecipeOperate {
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

    // ==================== 翻译注册 ====================

    public static void AddTranslations() {
        Register("分馏配方", "Fractionate Recipe");

        Register("当前物品", "Current item");
        Register("分馏配方提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("成功率", "Success Ratio");
        Register("损毁率", "Destroy Ratio");
        Register("产出", "Output");
        Register("随机", "Random");
        Register("单锁", "Single Lock");

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
        Register("因果追踪", "Causal Tracing");
        Register("虚空喷射", "Void Spray");
        Register("双倍点数", "Double Points");
        Register("最大增产等级", "Max Inc Level");

        // 右列：等级信息
        Register("当前配方强化等级", "Current Recipe Enhancement Level");
        Register("当前配方等级", "Current Recipe Level");
        Register("配方未解锁", "Recipe Locked");
        Register("无通用加成", "No General Bonus");
        Register("不消耗原料", "No Consume");
        Register("翻倍产出", "Double Output");
        Register("解锁方式", "Unlock Method");
        Register("升级方式", "Upgrade Method");
        Register("成长进度", "Growth Progress");
        Register("通过开线抽取获取", "Obtain from Opening Pool");
        Register("通过开线抽取获取；部分前期配方也会随科技保底解锁",
            "Obtain from Opening Pool; some early recipes are also unlocked by tech baseline");
        Register("通过原胚闭环或成长规划获得；相关科技也会保底解锁",
            "Obtain from Proto Loop or Growth Planning; related tech also provides baseline unlock");
        Register("通过成长规划或固定入口获得；解锁后直接满级",
            "Obtain from Growth Planning or fixed entry; unlocking grants max level directly");
        Register("通过黑雾支线成长规划报价获得",
            "Obtain from Dark Fog branch Growth Planning offers");
        Register("通过科技保底解锁", "Unlocked by tech baseline");
        Register("首次获得对应黑雾物品后解锁", "Unlock after obtaining the related Dark Fog item once");
        Register("重复抽到该配方即可升级", "Upgrade by drawing the same recipe again");
        Register("处理对应原胚获取经验，重复获得时也会直接提升",
            "Gain EXP by processing matching proto; duplicate rewards also level it up");
        Register("处理对应原胚获取经验", "Gain EXP by processing the matching proto");
        Register("处理对应黑雾物品获取经验，也可通过成长规划补差",
            "Gain EXP by processing the matching Dark Fog item, or catch up through Growth Planning");
        Register("处理对应矩阵获取保底进度", "Build pity progress by processing matching matrices");
        Register("解锁后", "After unlocking");
        Register("直接满级", "be granted at max level immediately");
        Register("已完全升级，无需继续成长", "Fully upgraded; no further growth needed");
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

    // ==================== UI 创建 ====================

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("分馏配方", objectName: "frac-recipe-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "查看配方成功率、损毁率、产物结构与强化等级信息".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "frac-recipe-content-card",
                        strong: true,
                        onBuilt: root => tab = root,
                        rows: BuildContentRows(),
                        cols: [Px(600f), Fr(1)],
                        rowGap: 0f,
                        columnGap: 20f,
                        children: [
                            Grid(pos: (0, 0), span: (1, 2),
                                cols: [Px(72f), Px(44f), Px(28f), Px(90f), Px(220f), Fr(1)],
                                columnGap: 8f,
                                children: [
                                    TextNode("当前物品", 15, onBuilt: text => txtCurrItem = text,
                                        pos: (0, 0), objectName: "textCurrItem"),
                                    ImageButtonNode(SelectedItem, 40f,
                                        onBuilt: btn => btnSelectedItem = btn.WithClickEvent(
                                            () => { OnButtonChangeItemClick(false, 22f); },
                                            () => { OnButtonChangeItemClick(true, 22f); }),
                                        pos: (0, 1), objectName: "button-change-item"),
                                    TipsButtonNode("提示", "分馏配方提示按钮说明1",
                                        pos: (0, 2), objectName: "frac-recipe-tip"),
                                    TextNode("配方类型", 15, pos: (0, 3), objectName: "frac-recipe-type-label"),
                                    ComboBoxNode(onBuilt: combo => combo.WithItems(RecipeTypeShortNames)
                                            .WithSize(200, 0).WithConfigEntry(RecipeTypeEntry),
                                        pos: (0, 4), objectName: "frac-recipe-type-combo"),
                                ]),
                            Grid(pos: (1, 0), span: (1, 2), cols: [1, 1, 1, 1], columnGap: 12f,
                                children: BuildSandboxButtonNodes()),
                            ..BuildInfoLineNodes(),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildContentRows() {
        var rows = new List<LayoutTrack> { Px(44f), Px(36f) };
        for (int i = 0; i < InfoLineCount; i++) {
            rows.Add(Px(LineHeight));
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildSandboxButtonNodes() {
        return [
            ButtonNode("重置等级", onClick: () => {
                if (SelectedRecipe != null) {
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, 0,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[0] = btn, pos: (0, 0), objectName: "frac-recipe-reset-level"),
            ButtonNode("等级-1", onClick: () => {
                if (SelectedRecipe != null) {
                    int level = RecipeGrowthQueries.GetLevel(SelectedRecipe);
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, level - 1,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[1] = btn, pos: (0, 1), objectName: "frac-recipe-level-down"),
            ButtonNode("等级+1", onClick: () => {
                if (SelectedRecipe != null) {
                    int level = RecipeGrowthQueries.GetLevel(SelectedRecipe);
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, level + 1,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[2] = btn, pos: (0, 2), objectName: "frac-recipe-level-up"),
            ButtonNode("等级升满", onClick: () => {
                if (SelectedRecipe != null) {
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, 5,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[3] = btn, pos: (0, 3), objectName: "frac-recipe-level-max"),
        ];
    }

    private static IReadOnlyList<LayoutNode> BuildInfoLineNodes() {
        int[] rang = !GenesisBook.Enable ? [0, 1, 2, 4, 10] : [0, 4, 10];
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < InfoLineCount; i++) {
            int index = i;
            int row = i + 2;
            nodes.Add(Grid(pos: (row, 0), cols: [Px(72f), Px(24f), Px(24f), Fr(1)], children: [
                TextNode("", 15, onBuilt: text => {
                        txtRecipeInfo[index] = text;
                        text.rectTransform.sizeDelta = new Vector2(560f, LineHeight);
                    },
                    pos: (0, 0), span: (1, 4), objectName: $"frac-recipe-info-{index}"),
                TextNode("", 15, onBuilt: text => {
                        txtProductLeft[index] = text;
                        text.gameObject.SetActive(false);
                    },
                    pos: (0, 0), objectName: $"frac-recipe-product-ratio-{index}"),
                ImageButtonNode(size: IconSize, onBuilt: btn => {
                        btn.gameObject.SetActive(false);
                        btnRecipeInfoIcons[index] = btn;
                    },
                    pos: (0, 1), objectName: $"frac-recipe-product-icon-{index}"),
                SliderNode(selectedInc, rang, null, 200f, onBuilt: slider => {
                        slider.gameObject.SetActive(false);
                        incSliders[index] = slider;
                    },
                    pos: (0, 3), objectName: $"frac-recipe-inc-slider-{index}"),
            ]));
            if (i < LevelLineCount) {
                nodes.Add(TextNode("", 13, onBuilt: text => txtLevelInfo[index] = text,
                    pos: (row, 1), objectName: $"frac-recipe-level-info-{index}"));
            }
        }

        return nodes;
    }

    // ==================== UI 更新 ====================

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        btnSelectedItem.Proto = SelectedItem;
        ERecipe recipeType = RecipeTypes[RecipeTypeEntry.Value];
        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, SelectedItem.ID);
        RecipeDisplaySnapshot snapshot = recipe == null ? default : RecipeGrowthQueries.GetSnapshot(recipe);
        ItemProto building = LDB.items.Select(recipeType.GetSpriteItemId());
        int line = 0;
        foreach (MySlider slider in incSliders) {
            slider?.gameObject.SetActive(false);
        }
        RefreshSandboxButtons(recipe, snapshot);

        if (recipe == null) {
            ShowTextLine(line++, "配方不存在！".Translate().WithColor(Red));
        } else if (!snapshot.IsUnlocked) {
            string headerLocked = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            int recipeColor = recipe.MatrixID - I电磁矩阵;
            ShowTextLine(line++, $"{headerLocked.WithColor(recipeColor)} {"分馏配方未解锁".Translate().WithColor(Red)}");
        } else {
            // ---- 左列内容 ----

            // 第1行：配方类型-原料名称（剥离强化等级）
            string headerName = $"{recipeType.GetShortName()}-{LDB.items.Select(recipe.InputID).name}";
            ShowTextLine(line++, headerName.WithColor(recipe.MatrixID - I电磁矩阵));
            ShowTextLine(line++, "");// 空行

            if (recipe is RectificationRecipe) {
                ShowTextLine(line++,
                    $"{"成功率".Translate()} {1.0f:P3}（稳定压缩）".WithColor(Orange));
                ShowTextLine(line++,
                    "精馏塔不参与成功率判定，献祭/成就成功率加成不会改变残片结算。".WithColor(Gray));
                ShowTextLine(line++,
                    $"{"损毁率".Translate()} {0.0f:P3}（稳定压缩）".WithColor(Green));
            } else {
                float sacrificeBoost = building?.SuccessBoost() ?? 0f;
                float progressBoost = Achievements.GetSuccessRateBonus();
                float actualSuccessRatio = Mathf.Clamp01(recipe.SuccessRatio
                                                         * (1f + sacrificeBoost)
                                                         * (1f + progressBoost));
                ShowTextLine(line++,
                    $"{"成功率".Translate()} {recipe.SuccessRatio:P3} × {(1f + sacrificeBoost):F3} × {(1f + progressBoost):F3} = {actualSuccessRatio:P3}"
                        .WithColor(Orange));
                ShowTextLine(line++,
                    $"(献祭 +{sacrificeBoost:P2} / 成就 +{progressBoost:P2})"
                        .WithColor(Gray));

                // 损毁率
                float baseDestroyRatio = snapshot.DestroyRatio
                                         + GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
                float destroyReduction = GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
                string destroyText = $"{"损毁率".Translate()} {baseDestroyRatio:P3}";
                if (destroyReduction > 0f) {
                    destroyText += $"（成就 -{destroyReduction:P3}，实际 {recipe.DestroyRatio:P3}）";
                }
                ShowTextLine(line++, destroyText.WithColor(Red));
            }
            ShowTextLine(line++, "");// 空行

            // 主产物：标签独占一行，下方竖向列表
            if (recipe.OutputMain.Count > 0) {
                ShowTextLine(line, "产出".Translate().WithColor(Orange));
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputMain) {
                if (recipe is RectificationRecipe rectificationRecipe) {
                    ShowRectificationProductLine(line++, rectificationRecipe, info);
                } else {
                    if (recipe is ConversionRecipe conversionRecipe) {
                        ShowConversionProductLine(line++, conversionRecipe, LDB.items.Select(info.OutputID), info);
                    } else {
                        ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
                    }
                }
            }

            // 副产物：标签独占一行，下方竖向列表
            if (recipe.OutputAppend.Count > 0) {
                ShowTextLine(line, "其他".Translate().WithColor(Orange));
                line++;// 标签独占一行
            }
            foreach (OutputInfo info in recipe.OutputAppend) {
                if (recipe is ConversionRecipe conversionRecipe) {
                    ShowConversionProductLine(line++, conversionRecipe, LDB.items.Select(info.OutputID), info);
                } else {
                    ShowProductLine(line++, LDB.items.Select(info.OutputID), info);
                }
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
        UpdateLevelColumn(recipe, snapshot);
    }

    /// <summary>
    /// 沙盒页顶部四个按钮统一按当前配方的真实等级边界刷新状态。
    /// </summary>
    private static void RefreshSandboxButtons(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        if (!GameMain.sandboxToolsEnabled) {
            foreach (UIButton button in recipeSandboxBtn) {
                button.gameObject.SetActive(false);
            }
            return;
        }

        foreach (UIButton button in recipeSandboxBtn) {
            button.gameObject.SetActive(true);
        }

        bool hasRecipe = recipe != null;
        int recipeLevel = hasRecipe ? snapshot.Level : 0;
        bool unlocked = hasRecipe && snapshot.IsUnlocked;
        int maxLevel = hasRecipe ? snapshot.MaxLevel : 0;

        recipeSandboxBtn[0].button.interactable = unlocked && recipeLevel > 0;
        recipeSandboxBtn[1].button.interactable = hasRecipe && recipeLevel > 0;
        recipeSandboxBtn[2].button.interactable = hasRecipe && recipeLevel < maxLevel;
        recipeSandboxBtn[3].button.interactable = hasRecipe && recipeLevel < maxLevel;
    }

    // ==================== 右列：强化等级表 ====================

    private static void UpdateLevelColumn(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        int currentLevel = recipe == null ? 0 : snapshot.Level;

        // 标题行
        string headerText;
        if (recipe == null) {
            headerText = "";
            foreach (Text text in txtLevelInfo) {
                text.text = "";
            }
            return;
        } else if (!snapshot.IsUnlocked) {
            headerText = "配方未解锁".Translate();
        } else {
            headerText = $"{"当前配方等级".Translate()} Lv{currentLevel}";
        }
        txtLevelInfo[0].text = headerText.WithColor(snapshot.IsUnlocked ? Orange : Red);

        int maxLevel = snapshot.MaxLevel;
        for (int lvl = 0; lvl <= maxLevel; lvl++) {
            int lineIdx = lvl + 1;
            string lvlText = snapshot.LevelDescriptions[lvl];

            string coloredText;
            if (!snapshot.IsUnlocked) {
                coloredText = lvlText.WithColor(Gray);// 未解锁：全灰
            } else if (lvl == currentLevel) {
                coloredText = lvlText.WithColor(Orange);// 当前等级：橙色高亮
            } else if (lvl < currentLevel) {
                coloredText = lvlText.WithColor(Green);// 已达到：绿色
            } else {
                coloredText = lvlText;// 未达到：默认白色
            }

            txtLevelInfo[lineIdx].text = coloredText;
        }

        for (int lineIdx = maxLevel + 2; lineIdx < LevelLineCount; lineIdx++) {
            txtLevelInfo[lineIdx].text = "";
        }

        int infoLineIdx = maxLevel + 2;
        if (!snapshot.IsUnlocked) {
            SetRightInfoLine(infoLineIdx++, $"{"解锁方式".Translate()}：{BuildUnlockHint(recipe)}".WithColor(Blue));
        }

        if (!snapshot.IsMaxed) {
            string upgradeHint = BuildUpgradeHint(recipe, snapshot);
            if (!string.IsNullOrEmpty(upgradeHint)) {
                SetRightInfoLine(infoLineIdx++, $"{"升级方式".Translate()}：{upgradeHint}"
                    .WithColor(snapshot.IsUnlocked ? White : Gray));
            }

            string progressHint = BuildUpgradeProgressHint(recipe, snapshot);
            if (!string.IsNullOrEmpty(progressHint)) {
                SetRightInfoLine(infoLineIdx++, $"{"成长进度".Translate()}：{progressHint}".WithColor(Gray));
            }
        } else {
            SetRightInfoLine(infoLineIdx++, $"{"升级方式".Translate()}：{"已完全升级，无需继续成长".Translate()}".WithColor(Green));
        }

        for (; infoLineIdx < LevelLineCount; infoLineIdx++) {
            txtLevelInfo[infoLineIdx].text = "";
        }
    }

    /// <summary>
    /// 右侧等级栏下方的辅助说明统一走这里，避免后续继续分散手写定位。
    /// </summary>
    private static void SetRightInfoLine(int lineIdx, string text) {
        if (lineIdx < 0 || lineIdx >= LevelLineCount) {
            return;
        }

        txtLevelInfo[lineIdx].text = text;
    }

    /// <summary>
    /// 按当前配方家族生成解锁提示，优先输出玩家在当前版本里真正能执行的入口。
    /// </summary>
    private static string BuildUnlockHint(BaseRecipe recipe) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        return rule.Family switch {
            RecipeFamily.MineralCopyNormal when rule.TechBaselineLevel > 0
                => "通过开线抽取获取；部分前期配方也会随科技保底解锁".Translate(),
            RecipeFamily.MineralCopyNormal or RecipeFamily.ConversionMaterialNormal
                => "通过开线抽取获取".Translate(),
            RecipeFamily.BuildingTrainForward or RecipeFamily.BuildingTrainReverse
                => "通过原胚闭环或成长规划获得；相关科技也会保底解锁".Translate(),
            RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog
                => "首次获得对应黑雾物品后解锁".Translate(),
            _ when recipe.RecipeType == ERecipe.Conversion && recipe.MatrixID == I黑雾矩阵
                => "通过黑雾支线成长规划报价获得".Translate(),
            RecipeFamily.ConversionBuilding or RecipeFamily.PointAggregate
                => "通过成长规划或固定入口获得；解锁后直接满级".Translate(),
            RecipeFamily.Rectification
                => "通过科技保底解锁".Translate(),
            _ => "通过开线抽取获取".Translate(),
        };
    }

    /// <summary>
    /// 按成长模式给出升级方式说明；若当前还未解锁，会自动补上“解锁后”前缀。
    /// </summary>
    private static string BuildUpgradeHint(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        string prefix = snapshot.IsUnlocked ? string.Empty : $"{"解锁后".Translate()}";
        return rule.Family switch {
            RecipeFamily.MineralCopyNormal or RecipeFamily.ConversionMaterialNormal
                => prefix + "重复抽到该配方即可升级".Translate(),
            RecipeFamily.BuildingTrainForward
                => prefix + "处理对应原胚获取经验，重复获得时也会直接提升".Translate(),
            RecipeFamily.BuildingTrainReverse
                => prefix + "处理对应原胚获取经验".Translate(),
            RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog
                => prefix + "处理对应黑雾物品获取经验，也可通过成长规划补差".Translate(),
            RecipeFamily.Rectification
                => prefix + "处理对应矩阵获取保底进度".Translate(),
            RecipeFamily.ConversionBuilding or RecipeFamily.PointAggregate
                => snapshot.IsUnlocked
                    ? "已完全升级，无需继续成长".Translate()
                    : prefix + "直接满级".Translate(),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// 只有当前规则存在明确阈值时，才显示经验/保底进度，避免给出虚假的进度条。
    /// </summary>
    private static string BuildUpgradeProgressHint(BaseRecipe recipe, RecipeDisplaySnapshot snapshot) {
        if (!snapshot.IsUnlocked || snapshot.IsMaxed) {
            return string.Empty;
        }

        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int threshold = RecipeGrowthRules.GetUpgradeThreshold(rule, snapshot.Level);
        if (threshold == int.MaxValue) {
            return string.Empty;
        }

        if (rule.UsesPity) {
            return $"{snapshot.PityProgress}/{threshold}";
        }

        if (rule.UsesGrowthExp) {
            return $"{snapshot.GrowthExp}/{threshold}";
        }

        return string.Empty;
    }

    private static float GetBaseDestroyRatio(BaseRecipe recipe, int? level = null) => 0.04f;

    // ==================== 产物显示（格式：概率 | 图标 | 数量） ====================

    /// <summary>
    /// 显示单个产物行：左侧概率文本，中间物品图标，右侧数量。
    /// </summary>
    private static void ShowProductLine(int line, ItemProto itemProto, OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        string count = forceShow || info.ShowOutputCount ? info.OutputCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? info.SuccessRatio.ToString("P3") : "???";

        // 左侧：概率文本
        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        // 中间：物品图标
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        // 右侧：数量
        txtRecipeInfo[line].text = $"×{count}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    private static void ShowConversionProductLine(int line, ConversionRecipe recipe, ItemProto itemProto,
        OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        bool showCount = forceShow || info.ShowOutputCount;
        string randomCount = showCount ? info.OutputCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? info.SuccessRatio.ToString("P3") : "???";
        string lockedCount = recipe.TryGetLockedOutputPlan(info.OutputID,
            out ConversionRecipe.LockedOutputPlan lockedPlan)
            ? (showCount ? lockedPlan.OutputCount.ToString("F3") : "???")
            : "???";

        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        txtRecipeInfo[line].text = $"{"随机".Translate()}×{randomCount}  {"单锁".Translate()}×{lockedCount}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    private static void ShowRectificationProductLine(int line, RectificationRecipe recipe, OutputInfo info) {
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        int fragmentCount = GetRectificationDisplayFragmentCount(recipe.InputID, selectedInc.Value);
        string count = forceShow || info.ShowOutputCount ? fragmentCount.ToString("F3") : "???";
        string ratio = forceShow || info.ShowSuccessRatio ? 1.0f.ToString("P3") : "???";

        txtProductLeft[line].text = ratio;
        txtProductLeft[line].SetPosition(ProductRatioX, 0f);
        txtProductLeft[line].gameObject.SetActive(true);

        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = LDB.items.Select(info.OutputID);
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

        txtRecipeInfo[line].text = $"×{count}";
        txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
    }

    // ==================== 等效处理（滑条 + 竖向输出列表） ====================

    private static int ShowEqProcessingSection(int line, BaseRecipe recipe, ItemProto building) {
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = "增产点数".Translate();
        txtRecipeInfo[line].SetPosition(0f, 0f);
        incSliders[line].gameObject.SetActive(true);
        line++;

        ShowTextLine(line++, "每个原料平均产出：".Translate());

        if (recipe is RectificationRecipe rectificationRecipe) {
            // 精馏配方是稳定压缩：不参与成功率/损毁/双倍/返料公式，直接显示当前条件下的真实残片数。
            int fragmentCount = GetRectificationDisplayFragmentCount(rectificationRecipe.InputID, selectedInc.Value);
            btnRecipeInfoIcons[line].gameObject.SetActive(true);
            btnRecipeInfoIcons[line].Proto = LDB.items.Select(IFE残片);
            NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);
            txtRecipeInfo[line].text = $"×{fragmentCount:F3}";
            txtRecipeInfo[line].SetPosition(ProductTextX, 0f);
            txtProductLeft[line].gameObject.SetActive(false);
            return line + 1;
        }

        // E = fracRatio / (1 - fracRatio*r)，其中 fracRatio=(1-d)*s，r=remainInputRatio
        float plrRatio = building?.PlrRatio() ?? 1.0f;
        float pointsBonus = (float)ProcessManager.MaxTableMilli(selectedInc.Value) * plrRatio;
        float successBoost = (building?.SuccessBoost() ?? 0f) + Achievements.GetSuccessRateBonus();
        float successRatio = Mathf.Clamp01(recipe.SuccessRatio * (1 + pointsBonus) * (1 + successBoost));
        float fracRatio = (1 - recipe.DestroyRatio) * successRatio;
        float remainInputRatio = recipe.RemainInputRatio;
        float repeatRatio = fracRatio * remainInputRatio;
        float repeatMultiplier = repeatRatio >= 0.9999f ? 10000.0f : 1.0f / (1.0f - repeatRatio);
        float mainOutputBonus = 1.0f + recipe.DoubleOutputRatio;

        ConversionRecipe conversionRecipe = recipe as ConversionRecipe;
        List<(int id, float cnt, float lockedCnt, bool showCount)> outputs = [];
        Dictionary<int, int> outputIndex = [];

        foreach (var info in recipe.OutputMain) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * mainOutputBonus * repeatMultiplier;
            float lockedCnt = GetLockedEquivalentCount(conversionRecipe, id, fracRatio, mainOutputBonus,
                repeatMultiplier);
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, elc, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, elc >= 0f ? elc : lockedCnt, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, lockedCnt, info.ShowSuccessRatio));
            }
        }
        foreach (var info in recipe.OutputAppend) {
            int id = info.OutputID;
            float cnt = fracRatio * info.SuccessRatio * info.OutputCount * repeatMultiplier;
            float lockedCnt = GetLockedEquivalentCount(conversionRecipe, id, fracRatio, mainOutputBonus,
                repeatMultiplier);
            if (outputIndex.TryGetValue(id, out int idx)) {
                var (eid, ec, elc, ecu) = outputs[idx];
                outputs[idx] = (eid, ec + cnt, elc >= 0f ? elc : lockedCnt, ecu);
            } else {
                outputIndex[id] = outputs.Count;
                outputs.Add((id, cnt, lockedCnt, info.ShowSuccessRatio));
            }
        }

        bool showDetails = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;

        foreach (var (id, cnt, lockedCnt, showCount) in outputs) {
            ItemProto outItem = LDB.items.Select(id);
            string outCount = showDetails || showCount ? cnt.ToString("F3") : "???";
            string lockedOutCount = showDetails || showCount ? lockedCnt.ToString("F3") : "???";

            txtProductLeft[line].gameObject.SetActive(false);

            btnRecipeInfoIcons[line].gameObject.SetActive(true);
            btnRecipeInfoIcons[line].Proto = outItem;
            NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], ProductIconX, 0f);

            txtRecipeInfo[line].text = conversionRecipe != null && lockedCnt >= 0f
                ? $"{"随机".Translate()}×{outCount}  {"单锁".Translate()}×{lockedOutCount}"
                : $"×{outCount}";
            txtRecipeInfo[line].SetPosition(ProductTextX, 0f);

            line++;
        }

        return line;
    }

    private static float GetLockedEquivalentCount(ConversionRecipe recipe, int outputId, float fracRatio,
        float mainOutputBonus, float repeatMultiplier) {
        if (recipe == null
            || !recipe.TryGetLockedOutputPlan(outputId,
                out ConversionRecipe.LockedOutputPlan lockedPlan)) {
            return -1f;
        }

        return fracRatio * lockedPlan.OutputCount * mainOutputBonus * repeatMultiplier;
    }

    private static int GetRectificationDisplayFragmentCount(int inputId, int inputInc) {
        int fragmentCount = GetRectificationFragmentYield(inputId, RectificationTower.PlrRatio);
        if (RectificationTower.EnableAfterglowExtraction && inputInc >= 4) {
            fragmentCount += 1;
        }
        if (RectificationTower.EnableHyperphaseCompression
            && (inputId == GetCurrentProgressMatrixId() || inputId == I黑雾矩阵)) {
            fragmentCount += 1;
        }
        return fragmentCount;
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
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        btnRecipeInfoIcons[line].gameObject.SetActive(true);
        btnRecipeInfoIcons[line].Proto = itemProto;
        NormalizeRectWithMidLeft(btnRecipeInfoIcons[line], 0f, 0f);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(TextOffsetWithIcon, 0f);
    }

    private static void ShowTextLine(int line, string text) {
        HideIconOnLine(line);
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        txtRecipeInfo[line].text = text;
        txtRecipeInfo[line].SetPosition(0f, 0f);
    }

    private static void HideIconOnLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
    }

    private static void HideAllLine(int line) {
        btnRecipeInfoIcons[line].gameObject.SetActive(false);
        txtProductLeft[line].gameObject.SetActive(false);
        incSliders[line].gameObject.SetActive(false);
        txtProductLeft[line].text = "";
        txtRecipeInfo[line].text = "";
        txtRecipeInfo[line].SetPosition(0f, 0f);
    }

    private static string FeatureStatus(bool enabled) =>
        enabled ? "已启用".Translate().WithColor(Green) : "未启用".Translate().WithColor(Gray);
}
