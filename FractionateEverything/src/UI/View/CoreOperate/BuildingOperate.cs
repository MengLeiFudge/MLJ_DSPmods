using System.IO;
using System.Text;
using BepInEx.Configuration;
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
        IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE回收塔, IFE行星内物流交互站
    ];
    private static readonly string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(), "转化塔".Translate(),
        "回收塔".Translate(), "物流交互站".Translate()
    ];
    private static Text txtChipCount;

    // private static Text txtBuildingInfo1;
    // private static UIButton btnTip1;
    // private static UIButton btnBuildingInfo1;
    // private static Text txtBuildingInfo2;
    // private static UIButton btnTip2;
    // private static UIButton btnBuildingInfo2;
    // private static Text txtBuildingInfo3;
    // private static UIButton btnBuildingInfo3;
    // private static UIButton btnTip3;
    // private static Text txtBuildingInfo4;
    // private static UIButton btnTip4;
    // private static UIButton btnBuildingInfo4;
    private static Text txtBuildingInfo5;
    private static UIButton btnTip5;
    private static Text txtTrait1;
    private static UIButton btnTrait1Tip;
    private static Text txtTrait2;
    private static UIButton btnTrait2Tip;
    private static UIButton btnReinforcement;
    private static UIButton btnReinforcementMax;
    private static UIButton[] reinforcementSandboxBtn = new UIButton[4];
    private static Text[] txtReinforcementBonus = new Text[10];

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
        Register("强化失败提示", "Awful! The enhancement failed...", "呜，苦露西！强化失败了……");
        Register("一键强化提示",
            "Confirm that you wish to enhance this building using multiple Fractionator Increase Chips until it reaches Level 20?",
            "确认使用数个分馏塔增幅芯片强化此建筑，直至达到20级吗？");
        Register("当前强化加成：", "Current Enhancement Bonuses:");
        Register("耐久度", "Durability");
        Register("电力消耗", "Power consumption");
        Register("分馏成功率", "Fractionation success ratio");
        Register("主产物数目", "Main product count");
        Register("副产物概率", "Append product ratio");

        // 各塔特质标题和说明（+6 特质）
        Register("分馏献祭", "Fractionation Sacrifice");
        Register("分馏献祭说明",
            "When the total number of fractionators in the data centre exceeds 1000, they are automatically decomposed at 10% per second. With n decomposed fractionators: Success Rate = 1 + sqrt(n/120), Processing Speed = 1 + sqrt(n/60).",
            "当数据中心的分馏塔数目超过1000时，会以每秒10%的速率自动分解。若损毁n个分馏塔，将使成功率变为 1+sqrt(n/120)，处理速率变为 1+sqrt(n/60)。");

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
            "With n decomposed fractionators: Success Rate = 1 + sqrt(n/240), Processing Speed = 1 + sqrt(n/120). When all five fractionator types are simultaneously boosted by the Sacrifice Trait, the boost of each type is doubled.",
            "损毁n个分馏塔时：成功率变为 1+sqrt(n/240)，处理速率变为 1+sqrt(n/120)。当所有种类的分馏塔同时拥有献祭增幅时，各塔的增幅效果翻倍。");

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
    }

    public static void LoadConfig(ConfigFile configFile) {
        BuildingTypeEntry = configFile.Bind("BuildingOperate", "Building Type", 0, "想要查看的建筑类型。");
        if (BuildingTypeEntry.Value < 0 || BuildingTypeEntry.Value >= BuildingTypeNames.Length) {
            BuildingTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        //todo: 优化显示（把文字改成图片）
        window = trans;
        tab = wnd.AddTab(trans, "建筑操作");
        float x = 0f;
        float y = 18f + 7f;
        var txt = wnd.AddText2(x, y, tab, "建筑类型");
        wnd.AddComboBox(x + 5 + txt.preferredWidth, y, tab)
            .WithItems(BuildingTypeNames).WithSize(200, 0).WithConfigEntry(BuildingTypeEntry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE分馏塔增幅芯片));
        txtChipCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
        y += 36f;
        // txtBuildingInfo1 = wnd.AddText2(x, y, tab, "动态刷新");
        // btnTip1 = wnd.AddTipsButton2(x + 250, y, tab, "流动输出集装", "流动输出集装说明");
        // btnBuildingInfo1 = wnd.AddButton(1, 2, y, tab, "启用",
        //     onClick: SetFluidOutputStack);
        // y += 36f;
        // txtBuildingInfo2 = wnd.AddText2(x, y, tab, "动态刷新");
        // btnTip2 = wnd.AddTipsButton2(x + 250, y, tab, "产物输出集装", "产物输出集装说明");
        // btnBuildingInfo2 = wnd.AddButton(1, 2, y, tab, "+1 集装数目",
        //     onClick: AddMaxStack);
        // y += 36f;
        // txtBuildingInfo3 = wnd.AddText2(x, y, tab, "动态刷新");
        // btnTip3 = wnd.AddTipsButton2(x + 250, y, tab, "分馏永动", "分馏永动说明");
        // btnBuildingInfo3 = wnd.AddButton(1, 2, y, tab, "启用",
        //     onClick: SetFracForever);
        // y += 36f;
        // txtBuildingInfo4 = wnd.AddText2(x, y, tab, "动态刷新");
        // btnTip4 = wnd.AddTipsButton2(x + 250, y, tab, "点数聚集效率层次", "点数聚集效率层次说明");
        // btnBuildingInfo4 = wnd.AddButton(1, 2, y, tab, "+1 聚集层次",
        //     onClick: AddPointAggregateLevel);
        // y += 36f;
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

        if (!GameMain.sandboxToolsEnabled) {
            btnReinforcement = wnd.AddButton(1, 2, y, tab, "敲一下！",
                onClick: Reinforcement);
            btnReinforcementMax = wnd.AddButton(1, 2, y + 36f, tab, "一直敲！",
                onClick: ReinforcementMax);
        } else {
            reinforcementSandboxBtn[0] = wnd.AddButton(1, 2, y, tab, "重置",
                onClick: Reset);
            reinforcementSandboxBtn[1] = wnd.AddButton(1, 2, y + 36f, tab, "降级",
                onClick: Downgrade);
            reinforcementSandboxBtn[2] = wnd.AddButton(1, 2, y + 36f * 2, tab, "升级",
                onClick: Upgrade);
            reinforcementSandboxBtn[3] = wnd.AddButton(1, 2, y + 36f * 3, tab, "升满",
                onClick: FullUpgrade);
        }
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
            // 回收塔暂无特质说明
            _ => (null, null, null, null),
        };
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        txtChipCount.text = $"x {GetItemTotalCount(IFE分馏塔增幅芯片)}";

        bool reinforcementPreCondition = true;

        // if (SelectedBuilding.ID != IFE行星内物流交互站) {
        //     reinforcementPreCondition &= SelectedBuilding.EnableFluidEnhancement();
        //     txtBuildingInfo1.text = SelectedBuilding.EnableFluidEnhancement()
        //         ? "已启用流动输出集装".Translate().WithColor(Gold)
        //         : "未启用流动输出集装".Translate().WithColor(Red);
        //     //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        //     btnTip1.gameObject.SetActive(true);
        //     btnBuildingInfo1.gameObject.SetActive(!SelectedBuilding.EnableFluidEnhancement());
        // } else {
        //     txtBuildingInfo1.text = "";
        //     btnTip1.gameObject.SetActive(false);
        //     btnBuildingInfo1.gameObject.SetActive(false);
        // }

        // string s = SelectedBuilding.ID != IFE行星内物流交互站
        //     ? $"{"产物输出集装：".Translate()}{SelectedBuilding.MaxStack()}"
        //     : $"{"输出集装：".Translate()}{SelectedBuilding.MaxStack()}";
        // reinforcementPreCondition &= SelectedBuilding.MaxStack() >= 4;
        // txtBuildingInfo2.text = s.WithColor(SelectedBuilding.MaxStack() * 2 - 1);
        // btnTip2.gameObject.SetActive(true);
        // btnBuildingInfo2.gameObject.SetActive(SelectedBuilding.MaxStack() < 4);
        //
        // if (SelectedBuilding.ID != IFE行星内物流交互站) {
        //     reinforcementPreCondition &= SelectedBuilding.EnableFracForever();
        //     txtBuildingInfo3.text = SelectedBuilding.EnableFracForever()
        //         ? "已启用分馏永动".Translate().WithColor(Gold)
        //         : "未启用分馏永动".Translate().WithColor(Red);
        //     btnTip3.gameObject.SetActive(true);
        //     btnBuildingInfo3.gameObject.SetActive(
        //         SelectedBuilding.EnableFluidEnhancement()
        //         && SelectedBuilding.MaxStack() >= 4
        //         && !SelectedBuilding.EnableFracForever()
        //     );
        // } else {
        //     txtBuildingInfo3.text = "";
        //     btnTip3.gameObject.SetActive(false);
        //     btnBuildingInfo3.gameObject.SetActive(false);
        // }
        //
        // if (SelectedBuilding.ID == IFE点数聚集塔) {
        //     s = $"{"点数聚集效率层次：".Translate()}{PointAggregateTower.Level}";
        //     reinforcementPreCondition &= PointAggregateTower.IsMaxLevel;
        //     txtBuildingInfo4.text = s.WithColor(PointAggregateTower.Level);
        //     btnTip4.gameObject.SetActive(true);
        //     btnBuildingInfo4.gameObject.SetActive(!PointAggregateTower.IsMaxLevel);
        // } else {
        //     txtBuildingInfo4.text = "";
        //     btnTip4.gameObject.SetActive(false);
        //     btnBuildingInfo4.gameObject.SetActive(false);
        // }

        string s = $"{"强化等级：".Translate()}{SelectedBuilding.Level()}";
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
            bool showBtn = reinforcementPreCondition && SelectedBuilding.Level() < MaxLevel;
            btnReinforcement.gameObject.SetActive(showBtn);
            btnReinforcementMax.gameObject.SetActive(showBtn);
        } else {
            reinforcementSandboxBtn[0].gameObject.SetActive(true);
            reinforcementSandboxBtn[1].gameObject.SetActive(SelectedBuilding.Level() > 0);
            reinforcementSandboxBtn[2].gameObject
                .SetActive(SelectedBuilding.Level() < MaxLevel);
            reinforcementSandboxBtn[3].gameObject
                .SetActive(SelectedBuilding.Level() < MaxLevel);
        }
        string[] strs;
        // if (SelectedBuilding.ID == IFE点数聚集塔) {
        //     strs = [
        //         "当前强化加成：".Translate(),
        //         $"{"耐久度".Translate()} +{SelectedBuilding.ReinforcementBonusDurability():P1}",
        //         $"{"电力消耗".Translate()} +{SelectedBuilding.ReinforcementBonusEnergy():P1}",
        //         $"{"分馏成功率".Translate()} +{SelectedBuilding.ReinforcementBonusFracSuccess():P1}",
        //         "",
        //         "",
        //         ""
        //     ];
        // } else if (SelectedBuilding.ID == IFE行星内物流交互站) {
        //     strs = [
        //         "当前强化加成：".Translate(),
        //         $"{"耐久度".Translate()} +{SelectedBuilding.ReinforcementBonusDurability():P1}",
        //         $"{"电力消耗".Translate()} -{1 - SelectedBuilding.ReinforcementBonusEnergy():P1}",
        //         "",
        //         "",
        //         ""
        //     ];
        // } else {
        //     strs = [
        //         "当前强化加成：".Translate(),
        //         $"{"耐久度".Translate()} +{SelectedBuilding.ReinforcementBonusDurability():P1}",
        //         $"{"电力消耗".Translate()} +{SelectedBuilding.ReinforcementBonusEnergy():P1}",
        //         $"{"主产物数目".Translate()} +{SelectedBuilding.ReinforcementBonusMainOutputCount():P1}",
        //         $"{"副产物概率".Translate()} +{SelectedBuilding.ReinforcementBonusAppendOutputRatio():P1}",
        //         ""
        //     ];
        // }
        if (SelectedBuilding.ID == IFE行星内物流交互站 || SelectedBuilding.ID == IFE星际物流交互站) {
            strs = [
                "当前强化加成：".Translate(),//todo
                $"{"待机/运行电力消耗".Translate()} x{SelectedBuilding.EnergyRatio():P1}",
                $"{"上传/下载电力消耗".Translate()} x{SelectedBuilding.InteractEnergyRatio():P1}",
                $"{"物品最大堆叠".Translate()} {SelectedBuilding.MaxStack()}",
            ];
        } else {
            strs = [
                "当前强化加成：".Translate(),//todo
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
    }

    // private static void SetFluidOutputStack() {
    //     if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
    //         return;
    //     }
    //     if (SelectedBuilding.EnableFluidEnhancement()) {
    //         return;
    //     }
    //     int takeId = IFE分馏塔增幅芯片;
    //     int takeCount = 3;
    //     ItemProto takeProto = LDB.items.Select(takeId);
    //     if (GameMain.sandboxToolsEnabled) {
    //         SelectedBuilding.EnableFluidEnhancement(true);
    //         if (NebulaModAPI.IsMultiplayerActive) {
    //             NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                 new BuildingChangePacket(BuildingTypeEntry.Value, 1));
    //         }
    //     } else {
    //         UIMessageBox.Show("提示".Translate(),
    //             $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
    //             + $"{"启用流动输出集装".Translate()}{"吗？".Translate()}",
    //             "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
    //             () => {
    //                 if (!TakeItemWithTip(takeId, takeCount, out _)) {
    //                     return;
    //                 }
    //                 SelectedBuilding.EnableFluidEnhancement(true);
    //                 if (NebulaModAPI.IsMultiplayerActive) {
    //                     NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                         new BuildingChangePacket(BuildingTypeEntry.Value, 1));
    //                 }
    //             },
    //             null);
    //     }
    // }
    //
    // private static void AddMaxStack() {
    //     if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
    //         return;
    //     }
    //     int takeId = IFE分馏塔增幅芯片;
    //     int takeCount = SelectedBuilding.ID == IFE行星内物流交互站 ? 6 : 1;
    //     if (SelectedBuilding.MaxStack() >= 4) {
    //         return;
    //     }
    //     ItemProto takeProto = LDB.items.Select(takeId);
    //     if (GameMain.sandboxToolsEnabled) {
    //         SelectedBuilding.MaxStack(SelectedBuilding.MaxStack() + 1);
    //         if (NebulaModAPI.IsMultiplayerActive) {
    //             NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                 new BuildingChangePacket(BuildingTypeEntry.Value, 2));
    //         }
    //     } else {
    //         UIMessageBox.Show("提示".Translate(),
    //             (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
    //             + $"{"+1 产物输出集装数目".Translate()}{"吗？".Translate()}",
    //             "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
    //             () => {
    //                 if (!GameMain.sandboxToolsEnabled && !TakeItemWithTip(takeId, takeCount, out _)) {
    //                     return;
    //                 }
    //                 SelectedBuilding.MaxStack(SelectedBuilding.MaxStack() + 1);
    //                 if (NebulaModAPI.IsMultiplayerActive) {
    //                     NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                         new BuildingChangePacket(BuildingTypeEntry.Value, 2));
    //                 }
    //             },
    //             null);
    //     }
    // }
    //
    // private static void SetFracForever() {
    //     if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
    //         return;
    //     }
    //     if (SelectedBuilding.EnableFracForever()) {
    //         return;
    //     }
    //     int takeId = IFE分馏塔增幅芯片;
    //     int takeCount = 2;
    //     ItemProto takeProto = LDB.items.Select(takeId);
    //     if (GameMain.sandboxToolsEnabled) {
    //         SelectedBuilding.EnableFracForever(true);
    //         if (NebulaModAPI.IsMultiplayerActive) {
    //             NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                 new BuildingChangePacket(BuildingTypeEntry.Value, 3));
    //         }
    //     } else {
    //         UIMessageBox.Show("提示".Translate(),
    //             (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
    //             + $"{"启用分馏永动".Translate()}{"吗？".Translate()}",
    //             "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
    //             () => {
    //                 if (!GameMain.sandboxToolsEnabled && !TakeItemWithTip(takeId, takeCount, out _)) {
    //                     return;
    //                 }
    //                 SelectedBuilding.EnableFracForever(true);
    //                 if (NebulaModAPI.IsMultiplayerActive) {
    //                     NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                         new BuildingChangePacket(BuildingTypeEntry.Value, 3));
    //                 }
    //             },
    //             null);
    //     }
    // }
    //
    // private static void AddPointAggregateLevel() {
    //     if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
    //         return;
    //     }
    //     int takeId = IFE分馏塔增幅芯片;
    //     int takeCount = 1;
    //     if (PointAggregateTower.Level >= 7) {
    //         return;
    //     }
    //     ItemProto takeProto = LDB.items.Select(takeId);
    //     if (GameMain.sandboxToolsEnabled) {
    //         PointAggregateTower.Level++;
    //         if (NebulaModAPI.IsMultiplayerActive) {
    //             NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                 new BuildingChangePacket(BuildingTypeEntry.Value, 4));
    //         }
    //     } else {
    //         UIMessageBox.Show("提示".Translate(),
    //             (GameMain.sandboxToolsEnabled ? "" : $"{"要花费".Translate()} {takeProto.name} x {takeCount} ")
    //             + $"{"+1 点数聚集效率层次".Translate()}{"吗？".Translate()}",
    //             "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
    //             () => {
    //                 if (!GameMain.sandboxToolsEnabled && !TakeItemWithTip(takeId, takeCount, out _)) {
    //                     return;
    //                 }
    //                 PointAggregateTower.Level++;
    //                 if (NebulaModAPI.IsMultiplayerActive) {
    //                     NebulaModAPI.MultiplayerSession.Network.SendPacket(
    //                         new BuildingChangePacket(BuildingTypeEntry.Value, 4));
    //                 }
    //             },
    //             null);
    //     }
    // }

    private static void Reinforcement() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.Level() >= MaxLevel) {
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
                if (!TakeItemWithTip(takeId, takeCount, out _)) {
                    return;
                }
                if (GetRandDouble() > 0) {
                    //todo
                    UIMessageBox.Show("提示".Translate(),
                        "强化失败提示".Translate(),
                        "确定".Translate(), UIMessageBox.ERROR,
                        null);
                    return;
                }
                SelectedBuilding.Level(SelectedBuilding.Level() + 1, true);
                UIMessageBox.Show("提示".Translate(),
                    "强化成功提示".Translate(),
                    "确定".Translate(), UIMessageBox.INFO,
                    null);
            },
            null);
    }

    private static void ReinforcementMax() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (SelectedBuilding.Level() >= MaxLevel) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        UIMessageBox.Show("提示".Translate(),
            "一键强化提示".Translate(),
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                while (true) {
                    if (SelectedBuilding.Level() >= MaxLevel) {
                        return;
                    }
                    if (!TakeItemWithTip(takeId, takeCount, out _)) {
                        return;
                    }
                    if (GetRandDouble() < 0) {
                        //todo
                        continue;
                    }
                    SelectedBuilding.Level(SelectedBuilding.Level() + 1, true);
                }
            },
            null);
    }

    private static void Upgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.Level(SelectedBuilding.Level() + 1, true);
    }

    private static void Downgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.Level(SelectedBuilding.Level() - 1, true);
    }

    private static void FullUpgrade() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.Level(MaxLevel, true);
    }

    private static void Reset() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        SelectedBuilding.Level(0, true);
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
