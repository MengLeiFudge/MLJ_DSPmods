using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class BuildingOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<int> BuildingTypeEntry;
    private static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[BuildingTypeEntry.Value]);
    private static readonly int[] BuildingIds = [
        IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE精馏塔, IFE行星内物流交互站
    ];
    private static readonly string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(), "转化塔".Translate(),
        "精馏塔".Translate(), "物流交互站".Translate()
    ];
    private static MyImageButton btnFragmentIcon;
    private static Text txtFragmentCount;
    private static MyImageButton btnMatrixIcon;
    private static Text txtMatrixCount;

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
    private const float RightColX = 620f;
    private const float LineHeight = 22f;
    private static float buildingInfoBaseY;
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

        //Register("分馏永动", "Frac forever");//已注册
        Register("分馏永动说明",
            "Once enabled, when the product cache reaches its limit, the building will no longer process incoming items but will instead transport them directly to the flow output.",
            "启用后，当产物缓存达到上限时，建筑将不再处理输入的物品，而是直接将其直接搬运到流动输出。");
        Register("已启用分馏永动", "Enable fractionate forever");
        Register("未启用分馏永动", "Not enable fractionate forever");
        //Register("启用", "Enable");//已注册
        Register("启用分馏永动", "to enable fractionate forever");

        Register("点数聚集效率层次", "Point accumulation efficiency level");
        Register("点数聚集效率层次说明",
            "The efficiency level of point accumulation affects the output rate of the product and the maximum increase in points for the product, with an upper limit of 7.",
            "点数聚集的效率层次会影响产物的输出速率、产物的最大增产点数，上限为7。");
        Register("点数聚集效率层次：", "Point accumulation efficiency level: ");
        Register("+1 聚集层次", "+1 aggregate level");
        Register("+1 点数聚集效率层次", "to +1 point accumulation efficiency level");

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
            "When the total number of fractionators in the data centre exceeds 1000, they are automatically decomposed at 10% per second. With n decomposed fractionators, fractionate recipes' success rate of the same type increased by 1+n/60 times.",
            "当数据中心的分馏塔数目超过1000时，会以每秒10%的速率自动分解。损毁n个分馏塔时，同类型分馏配方成功率变为 1+n/60 倍。");

        Register("质能裂变", "Mass-Energy Fission");
        Register("质能裂变说明",
            "Maintains an internal point pool (target: 100 x max stack). When the pool drops below the target, raw materials are consumed in bulk to replenish it (25 pts/item; 50 pts/item when Zero-Pressure Cycle is also active). When average proliferator points of inputs is below 10, points are drawn from the pool to bring them to 10.",
            "塔内维持一个点数池（目标值：100×最大集装）。当池量低于目标值时，批量消耗原料补满（每个原料换25点，同时激活零压循环时换50点）。当输入原料平均增产点数不足10时，从池中取点补足至10。");

        Register("虚空喷涂", "Void Spray");
        Register("虚空喷涂说明",
            "When the average proliferator points of inputs is below 4, the tower automatically uses proliferators from the fractionation data centre to spray the inputs.",
            "当原料的平均增产点数不足4时，会自动使用分馏数据中心的增产剂对原料进行喷涂。");

        Register("因果溯源", "Causal Tracing");
        Register("因果溯源说明",
            "When the fractionation result is 'raw material destroyed', there is a 50% chance that the raw material is not consumed.",
            "当分馏判定为\"原料损毁\"时，有50%的概率不消耗原料。");

        // 各塔特质标题和说明（+12 特质）
        Register("维度共鸣", "Dimensional Resonance");
        Register("维度共鸣说明",
            "The number of damaged fractionators is calculated as n*(1 + 0.1*number of fractionator types with sacrifice bonuses).",
            "损毁的分馏塔数目视为 n*(1+0.1*具有献祭加成的分馏塔种类数)。");

        Register("零压循环", "Zero-Pressure Cycle");
        Register("零压循环说明",
            "Each consumed raw material replenishes the point pool by 50 points (overriding Mass-Energy Fission's 25 pts). When there is no output belt on either side, flow output is automatically returned to flow input; product output is also prioritised for return to flow input.",
            "每个被消耗的原料向点数池补充50点（覆盖质能裂变的25点）。当侧面无输出传送带时，流动输出自动回填至流动输入；产物输出也优先回填至流动输入。");

        Register("双重点数", "Double Points");
        Register("双重点数说明",
            "Each 1 proliferator point on the input is converted as 2 points during transfer.",
            "原料的1点增产点数在转移时变为2点。");

        Register("单路锁定", "Single-Path Lock");
        Register("单路锁定说明",
            "Allows the fractionator to output only a single conversion product. The locked output can be configured in the fractionator's control panel.",
            "允许分馏塔只输出单一转化产物。可在分馏塔操作面板中设置锁定的输出产物。");

        Register("特质1（+6）：", "Trait 1 (+6): ");
        Register("特质2（+12）：", "Trait 2 (+12): ");
        Register("特质未激活", "Not yet unlocked");

        Register("当前建筑强化等级", "Current Building Enhancement Level");
        Register("成长经验", "Growth EXP");
        Register("下一等级经验", "Next Level EXP");
        Register("关键节点突破", "Breakthrough");
        Register("已满级", "Maxed");
        Register("当前等级需要靠经验自动成长", "This level advances automatically via EXP", "当前等级需要靠经验自动成长");
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

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "建筑操作");
        float x = 0f;
        float y = 18f + 7f;
        var txt = wnd.AddText2(x, y, tab, "建筑类型");
        wnd.AddComboBox(x + 5 + txt.preferredWidth, y, tab)
            .WithItems(BuildingTypeNames).WithSize(200, 0).WithConfigEntry(BuildingTypeEntry);
        btnFragmentIcon = wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE残片));
        txtFragmentCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        btnMatrixIcon = wnd.AddImageButton(GetPosition(3, 4).Item1 + 120f, y, tab, null);
        txtMatrixCount = wnd.AddText2(GetPosition(3, 4).Item1 + 120f + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;

        if (!GameMain.sandboxToolsEnabled) {
            btnReinforcement = wnd.AddButton(0, 4, y, tab, "关键节点突破",
                onClick: Reinforcement);
        } else {
            reinforcementSandboxBtn[0] = wnd.AddButton(0, 4, y, tab, "重置",
                onClick: () => { ChangeLevelTo(0); });
            reinforcementSandboxBtn[1] = wnd.AddButton(1, 4, y, tab, "降级",
                onClick: () => { ChangeLevelTo(SelectedBuilding.Level() - 1); });
            reinforcementSandboxBtn[2] = wnd.AddButton(2, 4, y, tab, "升级",
                onClick: () => { ChangeLevelTo(SelectedBuilding.Level() + 1); });
            reinforcementSandboxBtn[3] = wnd.AddButton(3, 4, y, tab, "升满",
                onClick: () => { ChangeLevelTo(MaxLevel); });
        }
        y += 36f + 7f;

        wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
        buildingInfoBaseY = y;
        for (int i = 0; i < LevelLineCount; i++) {
            string placeholder = i == 0 ? "当前建筑强化等级 +12" :
                i <= MaxLevel + 1 ? "+12  ×12  能耗50%  增产×2.0" : "";
            txtLevelInfo[i] = wnd.AddText2(RightColX, 0f, tab, placeholder, 14);
        }
        y += 36f;
        txtBuildingInfo5 = wnd.AddText2(x, y, tab, "动态刷新");
        btnTip5 = wnd.AddTipsButton2(x + 250, y, tab, "强化等级", "强化等级说明");
        y += 36f;
        // 特质1（+6）
        txtTrait1 = wnd.AddText2(x, y, tab, "动态刷新");
        btnTrait1Tip = wnd.AddTipsButton2(x + 250, y, tab, "特质1（+6）：", "特质1（+6）：");
        y += 36f;
        // 特质2（+12）
        txtTrait2 = wnd.AddText2(x, y, tab, "动态刷新");
        btnTrait2Tip = wnd.AddTipsButton2(x + 250, y, tab, "特质2（+12）：", "特质2（+12）：");
        for (int i = 0; i < txtReinforcementBonus.Length; i++) {
            y += 36f;
            txtReinforcementBonus[i] = wnd.AddText2(x, y, tab, "动态刷新");
        }
    }

    /// <summary>
    /// 返回 (trait1Key, trait2Key) 的翻译 key，null 表示该塔没有对应特质（如物流站）。
    /// </summary>
    private static (string title1, string desc1, string title2, string desc2) GetTraitKeys(int buildingId) {
        return buildingId switch {
            IFE交互塔 => ("分馏献祭", "分馏献祭说明", "维度共鸣", "维度共鸣说明"),
            IFE矿物复制塔 => ("质能裂变", "质能裂变说明", "零压循环", "零压循环说明"),
            IFE点数聚集塔 => ("虚空喷涂", "虚空喷涂说明", "双重点数", "双重点数说明"),
            IFE转化塔 => ("因果溯源", "因果溯源说明", "单路锁定", "单路锁定说明"),
            // 精馏塔暂无特质说明
            _ => (null, null, null, null),
        };
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        int currentMatrixId = GetCurrentProgressMatrixId();
        btnMatrixIcon.Proto = LDB.items.Select(currentMatrixId);
        txtFragmentCount.text = $"x {GetItemTotalCount(IFE残片)}";
        txtMatrixCount.text = $"x {GetItemTotalCount(currentMatrixId)}";

        string s = $"{"当前建筑强化等级".Translate()} +{SelectedBuilding.Level()}";
        txtBuildingInfo5.text = s.WithColor(SelectedBuilding.Level() / 3 + 1);
        btnTip5.gameObject.SetActive(true);

        // 特质行：按建筑类型动态填充
        var (title1, desc1, title2, desc2) = GetTraitKeys(SelectedBuilding.ID);
        bool hasTraits = title1 != null;
        bool trait1Active = SelectedBuilding.Level() >= 6;
        bool trait2Active = SelectedBuilding.Level() >= 12;

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
            bool showBtn = SelectedBuilding.Level() < MaxLevel && BuildingManager.NeedsBreakthrough(SelectedBuilding.ID);
            btnReinforcement.gameObject.SetActive(showBtn);
            if (showBtn) {
                btnReinforcement.SetText("关键节点突破".Translate());
            }
        } else {
            reinforcementSandboxBtn[0].gameObject.SetActive(true);
            reinforcementSandboxBtn[1].gameObject.SetActive(SelectedBuilding.Level() > 0);
            reinforcementSandboxBtn[2].gameObject
                .SetActive(SelectedBuilding.Level() < MaxLevel);
            reinforcementSandboxBtn[3].gameObject
                .SetActive(SelectedBuilding.Level() < MaxLevel);
        }
        string[] strs;
        long currentExp = BuildingManager.GetBuildingExp(SelectedBuilding.ID);
        long nextExp = BuildingManager.GetRequiredExpForNextLevel(SelectedBuilding.ID);
        if (SelectedBuilding.ID == IFE行星内物流交互站 || SelectedBuilding.ID == IFE星际物流交互站) {
            strs = [
                nextExp > 0
                    ? $"{"成长经验".Translate()} {currentExp}/{nextExp}"
                    : SelectedBuilding.Level() >= MaxLevel
                        ? "已满级".Translate()
                        : $"{"关键节点突破".Translate()}：{GetBreakthroughCostText(SelectedBuilding.Level())}",
                $"{"待机/运行电力消耗".Translate()} x{SelectedBuilding.EnergyRatio():P1}",
                $"{"上传/下载电力消耗".Translate()} x{SelectedBuilding.InteractEnergyRatio():P1}",
                $"{"物品最大堆叠".Translate()} {SelectedBuilding.MaxStack()}",
            ];
        } else {
            strs = [
                nextExp > 0
                    ? $"{"成长经验".Translate()} {currentExp}/{nextExp}"
                    : SelectedBuilding.Level() >= MaxLevel
                        ? "已满级".Translate()
                        : $"{"关键节点突破".Translate()}：{GetBreakthroughCostText(SelectedBuilding.Level())}",
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
        NormalizeRectWithMidLeft(txtLevelInfo[0], RightColX, buildingInfoBaseY);

        for (int lvl = 0; lvl <= MaxLevel; lvl++) {
            string desc = GetLevelDescription(buildingId, lvl);
            string colored = lvl == currentLevel ? desc.WithColor(Orange) :
                lvl < currentLevel ? desc.WithColor(Green) : desc;
            txtLevelInfo[lvl + 1].text = colored;
            NormalizeRectWithMidLeft(txtLevelInfo[lvl + 1], RightColX, buildingInfoBaseY + LineHeight * (lvl + 1));
        }

        txtLevelInfo[MaxLevel + 2].text = "";
    }

    private static string GetLevelDescription(int buildingId, int level) {
        int stack = LevelToMaxStack(level);
        if (buildingId is IFE行星内物流交互站 or IFE星际物流交互站) {
            return $"+{level}  ×{stack}  {"交互电力".Translate()}{LevelToInteractEnergyRatio(level):P0}";
        }
        float energy = LevelToEnergyRatio(level);
        if (buildingId == IFE点数聚集塔) {
            int maxInc = Math.Min(level + 4, 10);
            return $"+{level}  ×{stack}  {"能耗".Translate()}{energy:P0}  {"最大增产点数".Translate()}{maxInc}";
        }
        return $"+{level}  ×{stack}  {"能耗".Translate()}{energy:P0}  {"增产".Translate()}×{LevelToPlrRatio(level):F1}";
    }

    private static int LevelToMaxStack(int level) => BuildingManager.GetDefaultMaxStackByLevel(level);

    private static float LevelToEnergyRatio(int level) => BuildingManager.GetDefaultEnergyRatioByLevel(level);

    private static float LevelToPlrRatio(int level) => BuildingManager.GetDefaultPlrRatioByLevel(level);

    private static float LevelToInteractEnergyRatio(int level) => BuildingManager.GetStationInteractEnergyRatioByLevel(level);

    private static void Reinforcement() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.Level() >= MaxLevel) {
            return;
        }
        if (!BuildingManager.NeedsBreakthrough(SelectedBuilding.ID)) {
            UIRealtimeTip.Popup("当前等级需要靠经验自动成长".Translate(), true, 2);
            return;
        }
        (int matrixId, int matrixCount, int fragmentCount) = GetReinforcementCost(SelectedBuilding.Level());
        string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {matrixName} x {matrixCount} + 残片 x {fragmentCount} ")
            + $"{"关键节点突破".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(matrixId, matrixCount, out _)
                    || !TakeItemWithTip(IFE残片, fragmentCount, out _)) {
                    return;
                }
                SelectedBuilding.Level(SelectedBuilding.Level() + 1, true);
                UIMessageBox.Show("提示".Translate(),
                    "关键节点突破".Translate(),
                    "确定".Translate(), UIMessageBox.INFO,
                    null);
            },
            null);
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

    private static (int matrixId, int matrixCount, int fragmentCount) GetReinforcementCost(int currentLevel) {
        int stageMatrixId = GetCurrentProgressMatrixId();
        int fragmentCost = currentLevel switch {
            2 => 36,
            5 => 120,
            8 => 360,
            11 => 960,
            _ => 0,
        };
        int matrixCost = currentLevel switch {
            2 => 1,
            5 => 2,
            8 => 4,
            11 => 8,
            _ => 0,
        };
        return (stageMatrixId, matrixCost, fragmentCost);
    }

    private static string GetBreakthroughCostText(int currentLevel) {
        (int matrixId, int matrixCount, int fragmentCount) = GetReinforcementCost(currentLevel);
        string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
        return $"{matrixName} x{matrixCount} + 残片 x{fragmentCount}";
    }
}
