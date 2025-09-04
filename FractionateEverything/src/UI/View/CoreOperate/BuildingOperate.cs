using System.IO;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class BuildingOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<int> BuildingTypeEntry;
    private static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[BuildingTypeEntry.Value]);
    private static readonly int[] BuildingIds = [
        IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔
    ];
    private static readonly string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(),
        "量子复制塔".Translate(), "点金塔".Translate(), "分解塔".Translate(), "转化塔".Translate()
    ];
    private static Text txtChipCount;

    private static Text txtBuildingInfo1;
    private static UIButton btnBuildingInfo1;
    private static Text txtBuildingInfo2;
    private static UIButton btnBuildingInfo2;
    private static Text txtBuildingInfo3;
    private static UIButton btnBuildingInfo3;
    private static Text txtBuildingInfo4;
    private static UIButton btnTip4;
    private static UIButton btnBuildingInfo4;
    private static Text txtBuildingInfo5;
    private static UIButton btnTip5;
    private static UIButton btnBuildingInfo5;
    private static UIButton[] reinforcementBtn = new UIButton[4];
    private static Text[] txtReinforcementBonus = new Text[6];

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
        for (int i = 0; i <= MaxReinforcementLevel; i++) {
            cn.Append($"\n+{i}: 加成 +{ReinforcementBonusArr[i]:P1}，强化成功率 {ReinforcementSuccessRateArr[i]:P0}");
            en.Append(
                $"\n+{i}: Bonus +{ReinforcementBonusArr[i]:P1}, ReinforcementRate {ReinforcementSuccessRateArr[i]:P0}");
        }
        Register("强化等级说明",
            $"Reinforcement increases durability, power consumption, fractionation success rate, and product quantity. The relationship between reinforcement level and base reinforcement bonuses, as well as reinforcement success rate, is as follows:{en}",
            $"强化会增加耐久度、电力消耗、分馏成功率和产物数目。强化级别与强化基础加成、强化成功率的关系如下：{cn}");
        Register("敲一下！", "Knock once!");
        Register("强化此建筑", "Reinforce this building");
        Register("强化成功提示", "Great! The enhancement worked!", "耶，塔诺西！强化成功了！");
        Register("强化失败提示", "Awful! The enhancement failed...", "呜，苦露西！强化失败了……");
        Register("当前强化加成：", "Current Enhancement Bonuses:");
        Register("耐久度", "Durability");
        Register("电力消耗", "Power consumption");
        Register("分馏成功率", "Fractionation success rate");
        Register("主产物数目", "Main product count");
        Register("副产物概率", "Append product rate");
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
        wnd.AddComboBox(x, y, tab, "建筑类型")
            .WithItems(BuildingTypeNames).WithSize(200, 0).WithConfigEntry(BuildingTypeEntry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏塔增幅芯片);
        txtChipCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
        y += 36f;
        txtBuildingInfo1 = wnd.AddText2(x, y, tab, "动态刷新");
        wnd.AddTipsButton2(x + 250, y, tab, "流动输出集装", "流动输出集装说明");
        btnBuildingInfo1 = wnd.AddButton(1, 2, y, tab, "启用",
            onClick: SetFluidOutputStack);
        y += 36f;
        txtBuildingInfo2 = wnd.AddText2(x, y, tab, "动态刷新");
        wnd.AddTipsButton2(x + 250, y, tab, "产物输出集装", "产物输出集装说明");
        btnBuildingInfo2 = wnd.AddButton(1, 2, y, tab, "+1 集装数目",
            onClick: AddMaxProductOutputStack);
        y += 36f;
        txtBuildingInfo3 = wnd.AddText2(x, y, tab, "动态刷新");
        wnd.AddTipsButton2(x + 250, y, tab, "分馏永动", "分馏永动说明");
        btnBuildingInfo3 = wnd.AddButton(1, 2, y, tab, "启用",
            onClick: SetFracForever);
        y += 36f;
        txtBuildingInfo4 = wnd.AddText2(x, y, tab, "动态刷新");
        btnTip4 = wnd.AddTipsButton2(x + 250, y, tab, "点数聚集效率层次", "点数聚集效率层次说明");
        btnBuildingInfo4 = wnd.AddButton(1, 2, y, tab, "+1 聚集层次",
            onClick: AddPointAggregateLevel);
        y += 36f;
        txtBuildingInfo5 = wnd.AddText2(x, y, tab, "动态刷新");
        btnTip5 = wnd.AddTipsButton2(x + 250, y, tab, "强化等级", "强化等级说明");

        if (!GameMain.sandboxToolsEnabled) {
            btnBuildingInfo5 = wnd.AddButton(1, 2, y, tab, "敲一下！",
                onClick: Reinforcement);
        } else {
            reinforcementBtn[0] = wnd.AddButton(1, 2, y, tab, "重置",
                onClick: Reset);
            reinforcementBtn[1] = wnd.AddButton(1, 2, y + 36f, tab, "降级",
                onClick: Downgrade);
            reinforcementBtn[2] = wnd.AddButton(1, 2, y + 36f * 2, tab, "升级",
                onClick: Upgrade);
            reinforcementBtn[3] = wnd.AddButton(1, 2, y + 36f * 3, tab, "升满",
                onClick: FullUpgrade);
        }
        y += 36f;
        for (int i = 0; i < txtReinforcementBonus.Length; i++) {
            txtReinforcementBonus[i] = wnd.AddText2(x, y, tab, "动态刷新");
            y += 36f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        txtChipCount.text = $"x {GetItemTotalCount(IFE分馏塔增幅芯片)}";

        bool reinforcementPreCondition = true;

        reinforcementPreCondition &= SelectedBuilding.EnableFluidOutputStack();
        txtBuildingInfo1.text = SelectedBuilding.EnableFluidOutputStack()
            ? "已启用流动输出集装".Translate().WithColor(Orange)
            : "未启用流动输出集装".Translate().WithColor(Red);
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnBuildingInfo1.gameObject.SetActive(!SelectedBuilding.EnableFluidOutputStack());

        string s = $"{"产物输出集装：".Translate()}{SelectedBuilding.MaxProductOutputStack()}";
        reinforcementPreCondition &= SelectedBuilding.MaxProductOutputStack() >= 4;
        txtBuildingInfo2.text = SelectedBuilding.MaxProductOutputStack() >= 4
            ? s.WithColor(Orange)
            : s.WithQualityColor(SelectedBuilding.MaxProductOutputStack());
        btnBuildingInfo2.gameObject.SetActive(SelectedBuilding.MaxProductOutputStack() < 4);

        reinforcementPreCondition &= SelectedBuilding.EnableFracForever();
        txtBuildingInfo3.text = SelectedBuilding.EnableFracForever()
            ? "已启用分馏永动".Translate().WithColor(Orange)
            : "未启用分馏永动".Translate().WithColor(Red);
        btnBuildingInfo3.gameObject.SetActive(!SelectedBuilding.EnableFracForever());

        if (SelectedBuilding.ID == IFE点数聚集塔) {
            s = $"{"点数聚集效率层次：".Translate()}{PointAggregateTower.Level}";
            reinforcementPreCondition &= PointAggregateTower.IsMaxLevel;
            txtBuildingInfo4.text = s.WithPALvColor(PointAggregateTower.Level);
            btnTip4.gameObject.SetActive(true);
            btnBuildingInfo4.gameObject.SetActive(!PointAggregateTower.IsMaxLevel);
        } else {
            txtBuildingInfo4.text = "";
            btnTip4.gameObject.SetActive(false);
            btnBuildingInfo4.gameObject.SetActive(false);
        }

        if (reinforcementPreCondition) {
            s = $"{"强化等级：".Translate()}{SelectedBuilding.ReinforcementLevel()}";
            txtBuildingInfo5.text = SelectedBuilding.ReinforcementLevel() >= MaxReinforcementLevel
                ? s.WithColor(Orange)
                : s.WithQualityColor(SelectedBuilding.ReinforcementLevel() / 4 + 1);
            btnTip5.gameObject.SetActive(true);
            if (!GameMain.sandboxToolsEnabled) {
                btnBuildingInfo5.gameObject.SetActive(SelectedBuilding.ReinforcementLevel() < MaxReinforcementLevel);
            } else {
                reinforcementBtn[0].gameObject.SetActive(true);
                reinforcementBtn[1].gameObject.SetActive(SelectedBuilding.ReinforcementLevel() > 0);
                reinforcementBtn[2].gameObject.SetActive(SelectedBuilding.ReinforcementLevel() < MaxReinforcementLevel);
                reinforcementBtn[3].gameObject.SetActive(SelectedBuilding.ReinforcementLevel() < MaxReinforcementLevel);
            }

            string[] strs = [
                "当前强化加成：".Translate(),
                $"{"耐久度".Translate()} +{SelectedBuilding.ReinforcementBonusDurability():P1}",
                $"{"电力消耗".Translate()} +{SelectedBuilding.ReinforcementBonusEnergy():P1}",
                $"{"分馏成功率".Translate()} +{SelectedBuilding.ReinforcementBonusFracSuccess():P1}",
                $"{"主产物数目".Translate()} +{SelectedBuilding.ReinforcementBonusMainOutputCount():P1}",
                $"{"副产物概率".Translate()} +{SelectedBuilding.ReinforcementBonusAppendOutputRate():P1}",
            ];
            for (int i = 0; i < txtReinforcementBonus.Length; i++) {
                txtReinforcementBonus[i].text = SelectedBuilding.ReinforcementLevel() >= MaxReinforcementLevel
                    ? strs[i].WithColor(Orange)
                    : strs[i].WithQualityColor(SelectedBuilding.ReinforcementLevel() / 4 + 1);
            }
        } else {
            txtBuildingInfo5.text = "分馏塔强化功能将在以上升级全部升满后解锁。".Translate();
            btnTip5.gameObject.SetActive(false);
            if (!GameMain.sandboxToolsEnabled) {
                btnBuildingInfo5.gameObject.SetActive(false);
            } else {
                for (int i = 0; i < reinforcementBtn.Length; i++) {
                    reinforcementBtn[i].gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < txtReinforcementBonus.Length; i++) {
                txtReinforcementBonus[i].text = "";
            }
        }
    }

    private static void SetFluidOutputStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.EnableFluidOutputStack()) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 3;
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
            + $"{"启用流动输出集装".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!GameMain.sandboxToolsEnabled && !TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFluidOutputStack(true);
            },
            null);
    }

    private static void AddMaxProductOutputStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (SelectedBuilding.MaxProductOutputStack() >= 4) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
            + $"{"+1 产物输出集装数目".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!GameMain.sandboxToolsEnabled && !TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.MaxProductOutputStack(SelectedBuilding.MaxProductOutputStack() + 1);
            },
            null);
    }

    private static void SetFracForever() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.EnableFracForever()) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 5;
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
            + $"{"启用分馏永动".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!GameMain.sandboxToolsEnabled && !TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFracForever(true);
            },
            null);
    }

    private static void AddPointAggregateLevel() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (PointAggregateTower.Level >= 7) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
            + $"{"+1 点数聚集效率层次".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!GameMain.sandboxToolsEnabled && !TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                PointAggregateTower.Level++;
            },
            null);
    }

    private static void Reinforcement() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.ReinforcementLevel() >= MaxReinforcementLevel) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
            + $"{"强化此建筑".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                if (GetRandDouble() > SelectedBuilding.ReinforcementSuccessRate()) {
                    UIMessageBox.Show("提示".Translate(),
                        "强化失败提示".Translate(),
                        "确定".Translate(), UIMessageBox.ERROR,
                        null);
                    return;
                }
                SelectedBuilding.ReinforcementLevel(SelectedBuilding.ReinforcementLevel() + 1);
                UIMessageBox.Show("提示".Translate(),
                    "强化成功提示".Translate(),
                    "确定".Translate(), UIMessageBox.INFO,
                    null);
            },
            null);
    }

    private static void Upgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.ReinforcementLevel(SelectedBuilding.ReinforcementLevel() + 1);
    }

    private static void Downgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.ReinforcementLevel(SelectedBuilding.ReinforcementLevel() - 1);
    }

    private static void FullUpgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.ReinforcementLevel(MaxReinforcementLevel);
    }

    private static void Reset() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.ReinforcementLevel(0);
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
