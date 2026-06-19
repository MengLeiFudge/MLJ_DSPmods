using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Buildings;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Progression;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Setting;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// FE 建筑等级、经验和特性操作页面。
/// </summary>
public static class BuildingOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<int> BuildingTypeEntry;
    private static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[SelectedBuildingIndex]);
    private static int SelectedBuildingIndex => BuildingTypeEntry.Value >= 0 && BuildingTypeEntry.Value < BuildingIds.Length
        ? BuildingTypeEntry.Value
        : 0;
    private static readonly int[] BuildingIds = [
        IFE交互塔, IFE矿物复制塔, IFE转化塔, IFE精馏塔, IFE行星内物流交互站
    ];
    private static readonly string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "转化塔".Translate(), "精馏塔".Translate(),
        "物流交互站".Translate()
    ];
    private static MyImageButton btnFragmentIcon;
    private static Text txtFragmentCount;
    private static MyImageButton btnEssenceIcon;
    private static Text txtEssenceCount;

    private static Text txtBuildingInfo5;
    private static UIButton btnTip5;
    private static Text txtTrait1;
    private static UIButton btnTrait1Tip;
    private static Text txtTrait2;
    private static UIButton btnTrait2Tip;
    private static UIButton btnReinforcement;
    private static UIButton[] reinforcementSandboxBtn = new UIButton[4];
    private static Text[] txtReinforcementBonus = new Text[10];

    private const int LevelLineCount = 15;
    private static Text[] txtLevelInfo = new Text[LevelLineCount];

    public static void AddTranslations() {
        Register("建筑操作", "Building Operate");

        Register("建筑类型", "Building type");

        Register("建筑加成：", "Building bonuses:");

        Register("已启用流动输出集装", "Enable flow output integration");
        Register("未启用流动输出集装", "Not enable flow output integration");
        Register("流动输出集装", "Flow output integration");
        Register("流动输出集装说明",
            "Once enabled, the flow output (i.e., the side output) will be integrated as much as possible before being output.",
            "启用后，流动输出（即侧面的输出）会尽可能集装后再输出。");
        Register("启用", "Enable");
        Register("启用流动输出集装", "to enable flow output integration");

        Register("输出集装：", "output integration: ");
        Register("输出集装", "output integration");
        Register("产物输出集装：", "Product output integration: ");
        Register("产物输出集装", "Product output integration");
        Register("产物输出集装说明",
            "Product output (i.e., positive output) will be integrated to the extent possible before being output.",
            "产物输出（即正面的输出）会尽可能集装到该程度后再输出。");
        Register("+1 集装数目", "+1 integration count");
        Register("+1 产物输出集装数目", "to +1 product output integration count");

        Register("分馏永动说明",
            "Once enabled, when the product cache reaches its limit, the building will no longer process incoming items but will instead transport them directly to the flow output.",
            "启用后，当产物缓存达到上限时，建筑将不再处理输入的物品，而是直接将其直接搬运到流动输出。");
        Register("已启用分馏永动", "Enable fractionate forever");
        Register("未启用分馏永动", "Not enable fractionate forever");
        Register("启用分馏永动", "to enable fractionate forever");

        Register("分馏塔强化功能将在以上升级全部升满后解锁。",
            "The fractionator enhancement feature will unlock once all the above upgrades have been fully completed.");
        Register("强化等级：", "Reinforcement level: ");
        Register("强化等级", "Reinforcement level");
        StringBuilder cn = new();
        StringBuilder en = new();
        for (int i = 0; i <= MaxLevel; i++) {
            cn.Append($"\n+{i}: 加成 +{ReinforcementBonusArr[i]:P1}，强化成功率 {ReinforcementSuccessRatioArr[i]:P0}");
            en.Append(
                $"\n+{i}: Bonus +{ReinforcementBonusArr[i]:P1}, ReinforcementRatio {ReinforcementSuccessRatioArr[i]:P0}");
        }
        Register("强化等级说明",
            $"Reinforcement increases durability, power consumption, fractionation success rate, and product quantity. The relationship between reinforcement level and base reinforcement bonuses, as well as reinforcement success rate, is as follows:{en}",
            $"强化会增加耐久度、电力消耗、分馏成功率和产物数目。强化级别与强化基础加成、强化成功率的关系如下：{cn}");
        Register("敲一下！", "Knock once!");
        Register("一直敲！", "Keep knocking!");
        Register("强化此建筑", "Reinforce this building");
        Register("强化成功提示", "Great! The enhancement worked!", "耶，塔诺西！强化成功了！");
        Register("当前强化加成：", "Current Enhancement Bonuses:");
        Register("耐久度", "Durability");
        Register("电力消耗", "Power consumption");
        Register("分馏成功率", "Fractionation success ratio");
        Register("主产物数目", "Main product count");
        Register("副产物概率", "Append product ratio");

        // 各塔特质标题和说明（+6 特质）
        Register("分馏献祭", "Fractionation Sacrifice");
        Register("分馏献祭说明",
            "When the data centre holds at least 1000 fractionators of a type, the sacrifice trait consumes 10% of the current stock each second. With n fractionators sacrificed last second, fractionate recipes' success rate of the same type is increased by sqrt(n)/10, rounded down to 5% steps.",
            "当某类分馏塔在数据中心达到1000个时，献祭特质每秒消耗当前库存的10%。上一秒献祭n个分馏塔时，同类型分馏配方获得 sqrt(n)/10 的成功率加成，并向下取整到5%阶梯。");

        Register("质能裂变", "Mass-Energy Fission");
        Register("质能裂变说明",
            "Maintains an internal point pool (target: 100 x max stack). When the pool drops below the target, raw materials are consumed in bulk to replenish it (25 pts/item; 50 pts/item when Zero-Pressure Cycle is also active). When average proliferator points of inputs is below 10, points are drawn from the pool to bring them to 10.",
            "塔内维持一个点数池（目标值：100×最大集装）。当池量低于目标值时，批量消耗原料补满（每个原料换25点，同时激活零压循环时换50点）。当输入原料平均增产点数不足10时，从池中取点补足至10。");

        Register("因果溯源", "Causal Tracing");
        Register("因果溯源说明",
            "When the fractionation result is 'raw material destroyed', there is a 50% chance that the raw material is not consumed.",
            "当分馏判定为\"原料损毁\"时，有50%的概率不消耗原料。");

        // 各塔特质标题和说明（+12 特质）
        Register("维度共鸣", "Dimensional Resonance");
        Register("维度共鸣说明",
            "Sacrifice progress is calculated as n*(1 + 0.1*number of fractionator types with sacrifice progress).",
            "献祭进度视为 n*(1+0.1*具有献祭进度的分馏塔种类数)。");

        Register("零压循环", "Zero-Pressure Cycle");
        Register("零压循环说明",
            "Each consumed raw material replenishes the point pool by 50 points (overriding Mass-Energy Fission's 25 pts). When there is no output belt on either side, flow output is automatically returned to flow input; product output is also prioritised for return to flow input.",
            "每个被消耗的原料向点数池补充50点（覆盖质能裂变的25点）。当侧面无输出传送带时，流动输出自动回填至流动输入；产物输出也优先回填至流动输入。");

        Register("单路锁定", "Single-Path Lock");
        Register("单路锁定说明",
            "Allows the fractionator to output only a single conversion product. The locked output can be configured in the fractionator's control panel.",
            "允许分馏塔只输出单一转化产物。可在分馏塔操作面板中设置锁定的输出产物。");

        Register("余辉萃取", "Afterglow Extraction");
        Register("余辉萃取说明",
            "Improves high-order rectification stability. It is reserved for the hyperphase-ratio upgrade path.",
            "提高高阶精馏稳定性，预留给高阶成相率升级链路。");

        Register("超相压缩", "Hyperphase Compression");
        Register("超相压缩说明",
            "Unlocks the late-stage concept of essence compression. Essence reshaping currently follows the global compression weight model.",
            "解锁后期精华压缩概念；当前精华重整按全局压缩权重模型结算。");

        Register("特质1（+6）：", "Trait 1 (+6): ");
        Register("特质2（+12）：", "Trait 2 (+12): ");
        Register("特质未激活", "Not yet unlocked");

        Register("当前建筑强化等级", "Current Building Enhancement Level");
        Register("成长经验", "Growth EXP");
        Register("下一等级经验", "Next Level EXP");
        Register("关键节点突破", "Breakthrough");
        Register("已满级", "Maxed");
        Register("当前等级需要靠经验自动成长", "This level advances automatically via EXP", "当前等级需要靠经验自动成长");
        Register("待机/运行电力消耗", "Idle/working power consumption");
        Register("上传/下载电力消耗", "Upload/download power consumption");
        Register("物品最大堆叠", "Max item stack");
        Register("增产剂效果", "Proliferator effect");
        Register("原料流动增强", "Input flow enhancement");
        Register("能耗", "Enrg");
        Register("增产", "Prolif");
        Register("最大增产点数", "Max Inc Pts");
        Register("交互电力", "Interact Enrg");
    }

    public static void LoadConfig(ConfigFile configFile) {
        BuildingTypeEntry = configFile.Bind("BuildingOperate", "Building Type", 0, "想要查看的建筑类型。");
        if (BuildingTypeEntry.Value < 0 || BuildingTypeEntry.Value >= BuildingTypeNames.Length) {
            BuildingTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("建筑操作", objectName: "building-operate-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "查看建筑强化、关键节点突破与特质加成".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "building-operate-content-card",
                        strong: true,
                        onBuilt: root => tab = root,
                        rows: BuildContentRows(),
                        cols: [Fr(3), Fr(2)],
                        rowGap: 6f,
                        columnGap: 20f,
                        children: [
                            Grid(pos: (0, 0), span: (1, 2),
                                cols: [Px(82f), Px(220f), Fr(1), Px(44f), Px(70f), Px(44f), Px(70f)],
                                columnGap: 8f,
                                children: [
                                    TextNode("建筑类型", 15, pos: (0, 0), objectName: "building-type-label"),
                                    ComboBoxNode(onBuilt: combo => combo.WithItems(BuildingTypeNames)
                                            .WithSize(200, 0).WithConfigEntry(BuildingTypeEntry),
                                        pos: (0, 1), objectName: "building-type-combo"),
                                    ImageButtonNode(LDB.items.Select(IFE残片), 40f, onBuilt: btn => btnFragmentIcon = btn,
                                        pos: (0, 3), objectName: "building-fragment-icon"),
                                    TextNode("", 13, onBuilt: text => txtFragmentCount = text,
                                        pos: (0, 4), objectName: "building-fragment-count"),
                                    ImageButtonNode(size: 40f, onBuilt: btn => btnEssenceIcon = btn,
                                        pos: (0, 5), objectName: "building-essence-icon"),
                                    TextNode("", 13, onBuilt: text => txtEssenceCount = text,
                                        pos: (0, 6), objectName: "building-essence-count"),
                                ]),
                            Grid(pos: (1, 0), span: (1, 2), cols: [1, 1, 1, 1], columnGap: 12f,
                                children: [
                                    ButtonNode("关键节点突破", onClick: Reinforcement,
                                        onBuilt: btn => btnReinforcement = btn,
                                        pos: (0, 0), objectName: "building-breakthrough"),
                                    ButtonNode("重置等级", onClick: () => { ChangeLevelTo(0); },
                                        onBuilt: btn => reinforcementSandboxBtn[0] = btn,
                                        pos: (0, 0), objectName: "building-reset-level"),
                                    ButtonNode("等级-1", onClick: () => { ChangeLevelTo(SelectedBuilding.Level() - 1); },
                                        onBuilt: btn => reinforcementSandboxBtn[1] = btn,
                                        pos: (0, 1), objectName: "building-level-down"),
                                    ButtonNode("等级+1", onClick: () => { ChangeLevelTo(SelectedBuilding.Level() + 1); },
                                        onBuilt: btn => reinforcementSandboxBtn[2] = btn,
                                        pos: (0, 2), objectName: "building-level-up"),
                                    ButtonNode("等级升满", onClick: () => { ChangeLevelTo(MaxLevel); },
                                        onBuilt: btn => reinforcementSandboxBtn[3] = btn,
                                        pos: (0, 3), objectName: "building-level-max"),
                                ]),
                            TextNode("建筑加成：", 15, pos: (2, 0), objectName: "text-building-info-0"),
                            ..BuildLeftInfoNodes(),
                            ..BuildLevelInfoNodes(),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildContentRows() {
        var rows = new List<LayoutTrack> { Px(44f), Px(36f), Px(26f), Px(30f), Px(30f), Px(30f) };
        for (int i = 0; i < txtReinforcementBonus.Length; i++) {
            rows.Add(Px(30f));
        }
        for (int i = rows.Count; i < LevelLineCount + 2; i++) {
            rows.Add(Px(26f));
        }
        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildLeftInfoNodes() {
        var nodes = new List<LayoutNode> {
            Grid(pos: (3, 0), cols: [Fr(1), Px(28f)], columnGap: 8f, children: [
                TextNode("动态刷新", onBuilt: text => txtBuildingInfo5 = text,
                    pos: (0, 0), objectName: "building-current-level"),
                TipsButtonNode("强化等级", "强化等级说明", onBuilt: btn => btnTip5 = btn,
                    pos: (0, 1), objectName: "building-level-tip"),
            ]),
            Grid(pos: (4, 0), cols: [Fr(1), Px(28f)], columnGap: 8f, children: [
                TextNode("动态刷新", onBuilt: text => txtTrait1 = text,
                    pos: (0, 0), objectName: "building-trait-1"),
                TipsButtonNode("特质1（+6）：", "特质1（+6）：", onBuilt: btn => btnTrait1Tip = btn,
                    pos: (0, 1), objectName: "building-trait-1-tip"),
            ]),
            Grid(pos: (5, 0), cols: [Fr(1), Px(28f)], columnGap: 8f, children: [
                TextNode("动态刷新", onBuilt: text => txtTrait2 = text,
                    pos: (0, 0), objectName: "building-trait-2"),
                TipsButtonNode("特质2（+12）：", "特质2（+12）：", onBuilt: btn => btnTrait2Tip = btn,
                    pos: (0, 1), objectName: "building-trait-2-tip"),
            ]),
        };
        for (int i = 0; i < txtReinforcementBonus.Length; i++) {
            int index = i;
            nodes.Add(TextNode("动态刷新", onBuilt: text => txtReinforcementBonus[index] = text,
                pos: (6 + index, 0), objectName: $"building-reinforcement-bonus-{index}"));
        }
        return nodes;
    }

    private static IReadOnlyList<LayoutNode> BuildLevelInfoNodes() {
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < LevelLineCount; i++) {
            int index = i;
            string placeholder = i == 0 ? "当前建筑强化等级 +12" :
                i <= MaxLevel + 1 ? "+12  ×12  能耗50%  增产×2.0" : "";
            nodes.Add(TextNode(placeholder, 13, onBuilt: text => txtLevelInfo[index] = text,
                pos: (2 + index, 1), objectName: $"building-level-info-{index}"));
        }
        return nodes;
    }

    /// <summary>
    /// 返回 (trait1Key, trait2Key) 的翻译 key，null 表示该塔没有对应特质（如物流站）。
    /// </summary>
    private static (string title1, string desc1, string title2, string desc2) GetTraitKeys(int buildingId) {
        return buildingId switch {
            IFE交互塔 => ("分馏献祭", "分馏献祭说明", "维度共鸣", "维度共鸣说明"),
            IFE矿物复制塔 => ("质能裂变", "质能裂变说明", "零压循环", "零压循环说明"),
            IFE转化塔 => ("因果溯源", "因果溯源说明", "单路锁定", "单路锁定说明"),
            IFE精馏塔 => ("余辉萃取", "余辉萃取说明", "超相压缩", "超相压缩说明"),
            _ => (null, null, null, null),
        };
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        int currentEssenceId = GetMatrixEssenceItemId(GetCurrentProgressStageIndex());
        btnEssenceIcon.Proto = LDB.items.Select(currentEssenceId);
        btnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
        btnEssenceIcon.SetCount(GetItemTotalCount(currentEssenceId));
        txtFragmentCount.text = "";
        txtEssenceCount.text = "";

        string s = $"{"当前建筑强化等级".Translate()} +{SelectedBuilding.Level()}";
        txtBuildingInfo5.text = s.WithColor(SelectedBuilding.Level() / 3 + 1);
        btnTip5.gameObject.SetActive(true);

        // 特质行：按建筑类型动态填充
        var (title1, desc1, title2, desc2) = GetTraitKeys(SelectedBuilding.ID);
        bool hasTraits = title1 != null;
        bool trait1Active = SelectedBuilding.Level() >= BuildingGrowthService.LevelThresholdTrait1;
        bool trait2Active = SelectedBuilding.Level() >= BuildingGrowthService.LevelThresholdTrait2;

        if (hasTraits) {
            string trait1Name = title1.Translate();
            string trait2Name = title2.Translate();
            string activeSuffix = trait1Active
                ? "".WithColor(Gold)
                : $"（{"特质未激活".Translate()}）".WithColor(Red);
            string activeSuffix2 = trait2Active
                ? "".WithColor(Gold)
                : $"（{"特质未激活".Translate()}）".WithColor(Red);
            txtTrait1.text = ($"{"特质1（+6）：".Translate()}{trait1Name}{activeSuffix}").WithColor(trait1Active ? 4 : 2);
            txtTrait2.text = ($"{"特质2（+12）：".Translate()}{trait2Name}{activeSuffix2}").WithColor(trait2Active ? 4 : 2);
            btnTrait1Tip.tips.tipTitle = title1.Translate();
            btnTrait1Tip.tips.tipText = desc1.Translate();
            btnTrait1Tip.UpdateTip();
            btnTrait2Tip.tips.tipTitle = title2.Translate();
            btnTrait2Tip.tips.tipText = desc2.Translate();
            btnTrait2Tip.UpdateTip();
            btnTrait1Tip.gameObject.SetActive(true);
            btnTrait2Tip.gameObject.SetActive(true);
            txtTrait1.gameObject.SetActive(true);
            txtTrait2.gameObject.SetActive(true);
        } else {
            txtTrait1.text = "";
            txtTrait2.text = "";
            btnTrait1Tip.gameObject.SetActive(false);
            btnTrait2Tip.gameObject.SetActive(false);
            txtTrait1.gameObject.SetActive(false);
            txtTrait2.gameObject.SetActive(false);
        }

        if (!GameMain.sandboxToolsEnabled) {
            bool showBtn = SelectedBuilding.Level() < MaxLevel
                           && BuildingGrowthService.NeedsBreakthrough(SelectedBuilding.ID);
            btnReinforcement.gameObject.SetActive(showBtn);
            foreach (UIButton button in reinforcementSandboxBtn) {
                button.gameObject.SetActive(false);
            }
            if (showBtn) {
                btnReinforcement.SetText("关键节点突破".Translate());
            }
        } else {
            btnReinforcement.gameObject.SetActive(false);
            reinforcementSandboxBtn[0].gameObject.SetActive(true);
            reinforcementSandboxBtn[1].gameObject.SetActive(true);
            reinforcementSandboxBtn[2].gameObject.SetActive(true);
            reinforcementSandboxBtn[3].gameObject.SetActive(true);
            reinforcementSandboxBtn[0].button.interactable = SelectedBuilding.Level() > 0;
            reinforcementSandboxBtn[1].button.interactable = SelectedBuilding.Level() > 0;
            reinforcementSandboxBtn[2].button.interactable = SelectedBuilding.Level() < MaxLevel;
            reinforcementSandboxBtn[3].button.interactable = SelectedBuilding.Level() < MaxLevel;
        }
        string[] strs;
        long currentExp = BuildingGrowthService.GetBuildingExp(SelectedBuilding.ID);
        long nextExp = BuildingGrowthService.GetRequiredExpForNextLevel(SelectedBuilding.ID);
        if (SelectedBuilding.ID == IFE行星内物流交互站 || SelectedBuilding.ID == IFE星际物流交互站) {
            strs = [
                nextExp > 0 ? $"{"成长经验".Translate()} {currentExp}/{nextExp}" :
                SelectedBuilding.Level() >= MaxLevel ? "已满级".Translate() :
                $"{"关键节点突破".Translate()}：{GetBreakthroughCostText(SelectedBuilding.Level())}",
                $"{"待机/运行电力消耗".Translate()} x{SelectedBuilding.EnergyRatio():P1}",
                $"{"上传/下载电力消耗".Translate()} x{SelectedBuilding.InteractEnergyRatio():P1}",
                $"{"物品最大堆叠".Translate()} {SelectedBuilding.MaxStack()}",
            ];
        } else {
            strs = [
                nextExp > 0 ? $"{"成长经验".Translate()} {currentExp}/{nextExp}" :
                SelectedBuilding.Level() >= MaxLevel ? "已满级".Translate() :
                $"{"关键节点突破".Translate()}：{GetBreakthroughCostText(SelectedBuilding.Level())}",
                $"{"待机/运行电力消耗".Translate()} x{SelectedBuilding.EnergyRatio():P1}",
                $"{"增产剂效果".Translate()} x{SelectedBuilding.PlrRatio():P1}",
                $"{"原料流动增强".Translate()} {(SelectedBuilding.EnableFluidEnhancement() ? "启用" : "禁用").Translate()}",
                $"{"物品最大堆叠".Translate()} {SelectedBuilding.MaxStack()}",
            ];
        }
        for (int i = 0; i < txtReinforcementBonus.Length; i++) {
            if (i < strs.Length) {
                txtReinforcementBonus[i].text = strs[i].WithColor(SelectedBuilding.Level() / 3 + 1);
            } else {
                txtReinforcementBonus[i].text = "";
            }
        }

        UpdateLevelColumn();
    }

    private static void UpdateLevelColumn() {
        int currentLevel = SelectedBuilding.Level();
        int buildingId = SelectedBuilding.ID;

        txtLevelInfo[0].text = $"{"当前建筑强化等级".Translate()} +{currentLevel}".WithColor(Orange);

        for (int lvl = 0; lvl <= MaxLevel; lvl++) {
            string desc = GetLevelDescription(buildingId, lvl);
            string colored = lvl == currentLevel ? desc.WithColor(Orange) :
                lvl < currentLevel ? desc.WithColor(Green) : desc;
            txtLevelInfo[lvl + 1].text = colored;
        }

        txtLevelInfo[MaxLevel + 2].text = "";
    }

    private static string GetLevelDescription(int buildingId, int level) {
        int stack = LevelToMaxStack(level);
        if (buildingId is IFE行星内物流交互站 or IFE星际物流交互站) {
            return $"+{level}  ×{stack}  {"交互电力".Translate()}{LevelToInteractEnergyRatio(level):P0}";
        }
        float energy = LevelToEnergyRatio(level);
        return $"+{level}  ×{stack}  {"能耗".Translate()}{energy:P0}  {"增产".Translate()}×{LevelToPlrRatio(level):F1}";
    }

    private static int LevelToMaxStack(int level) => StackingManager.CurrentMaxStack;

    private static float LevelToEnergyRatio(int level) => BuildingGrowthService.GetDefaultEnergyRatioByLevel(level);

    private static float LevelToPlrRatio(int level) => BuildingGrowthService.GetDefaultPlrRatioByLevel(level);

    private static float LevelToInteractEnergyRatio(int level) =>
        BuildingGrowthService.GetStationInteractEnergyRatioByLevel(level);

    private static void Reinforcement() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.Level() >= MaxLevel) {
            return;
        }
        if (!BuildingGrowthService.NeedsBreakthrough(SelectedBuilding.ID)) {
            UIRealtimeTip.Popup("当前等级需要靠经验自动成长".Translate(), true, 2);
            return;
        }
        (int essenceId, int essenceCount, int fragmentCount) =
            BuildingGrowthService.GetBreakthroughCost(SelectedBuilding.Level());
        string essenceName = LDB.items.Select(essenceId)?.name ?? essenceId.ToString();
        Miscellaneous.ShowQuestion("提示".Translate(),
            (GameMain.sandboxToolsEnabled
                ? ""
                : $"{"要花费".Translate()} {essenceName} x {essenceCount} + 残片 x {fragmentCount} ")
            + $"{"关键节点突破".Translate()}{"吗？".Translate()}",
            () => {
                if (!TakeItemWithTip(essenceId, essenceCount, out _)
                    || !TakeItemWithTip(IFE残片, fragmentCount, out _)) {
                    return;
                }
                SelectedBuilding.Level(SelectedBuilding.Level() + 1, true);
                UIMessageBox.Show("提示".Translate(),
                    "关键节点突破".Translate(),
                    "确定".Translate(), UIMessageBox.INFO,
                    null);
            });
    }

    private static void ChangeLevelTo(int target) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.Level(target, true);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion

    private static string GetBreakthroughCostText(int currentLevel) {
        (int essenceId, int essenceCount, int fragmentCount) = BuildingGrowthService.GetBreakthroughCost(currentLevel);
        string essenceName = LDB.items.Select(essenceId)?.name ?? essenceId.ToString();
        return $"{essenceName} x{essenceCount} + 残片 x{fragmentCount}";
    }
}
