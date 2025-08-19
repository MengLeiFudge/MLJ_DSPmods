using System.IO;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class BuildingOperate {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<int> BuildingTypeEntry;
    private static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[BuildingTypeEntry.Value]);
    private static string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(),
        "量子复制塔".Translate(), "点金塔".Translate(), "分解塔".Translate(), "转化塔".Translate()
    ];
    private static int[] BuildingIds = [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔];

    private static Text textBuildingInfo1;
    private static UIButton btnBuildingInfo1;
    private static Text textBuildingInfo2;
    private static UIButton btnBuildingInfo2;
    private static Text textBuildingInfo3;
    private static UIButton btnBuildingInfo3;
    private static Text textBuildingInfo4;
    private static UIButton btnTip4;
    private static UIButton btnBuildingInfo4;

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
        float y = 18f;
        wnd.AddComboBox(x, y, tab, "建筑类型")
            .WithItems(BuildingTypeNames).WithSize(200, 0).WithConfigEntry(BuildingTypeEntry);
        y += 36f;
        wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
        y += 36f;
        textBuildingInfo1 = wnd.AddText2(x, y, tab, "动态刷新", 15, "text-building-info-1");
        wnd.AddTipsButton2(x + 250, y, tab, "流动输出集装", "流动输出集装说明");
        btnBuildingInfo1 = wnd.AddButton(1, 2, y, tab, "启用", 16, "button-enable-fluid-output-stack",
            SetFluidOutputStack);
        y += 36f;
        textBuildingInfo2 = wnd.AddText2(x, y, tab, "动态刷新", 15, "text-building-info-2");
        wnd.AddTipsButton2(x + 250, y, tab, "产物输出集装", "产物输出集装说明");
        btnBuildingInfo2 = wnd.AddButton(1, 2, y, tab, "+1 集装数目", 16, "button-add-max-product-output-stack",
            AddMaxProductOutputStack);
        y += 36f;
        textBuildingInfo3 = wnd.AddText2(x, y, tab, "动态刷新", 15, "text-building-info-3");
        wnd.AddTipsButton2(x + 250, y, tab, "分馏永动", "分馏永动说明");
        btnBuildingInfo3 = wnd.AddButton(1, 2, y, tab, "启用", 16, "button-enable-frac-forever",
            SetFracForever);
        y += 36f;
        textBuildingInfo4 = wnd.AddText2(x, y, tab, "动态刷新", 15, "text-building-info-4");
        btnTip4 = wnd.AddTipsButton2(x + 250, y, tab, "点数聚集效率层次", "点数聚集效率层次说明");
        btnBuildingInfo4 = wnd.AddButton(1, 2, y, tab, "+1 聚集层次", 16, "button-add-point-aggregate-level",
            AddPointAggregateLevel);
        y += 36f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        textBuildingInfo1.text = SelectedBuilding.EnableFluidOutputStack()
            ? "已启用流动输出集装".Translate().WithColor(Orange)
            : "未启用流动输出集装".Translate().WithColor(Red);
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnBuildingInfo1.gameObject.SetActive(!SelectedBuilding.EnableFluidOutputStack());

        string s = $"{"产物输出集装：".Translate()}{SelectedBuilding.MaxProductOutputStack()}";
        textBuildingInfo2.text = SelectedBuilding.MaxProductOutputStack() >= 4
            ? s.WithColor(Orange)
            : s.WithQualityColor(SelectedBuilding.MaxProductOutputStack());
        btnBuildingInfo2.gameObject.SetActive(SelectedBuilding.MaxProductOutputStack() < 4);

        textBuildingInfo3.text = SelectedBuilding.EnableFracForever()
            ? "已启用分馏永动".Translate().WithColor(Orange)
            : "未启用分馏永动".Translate().WithColor(Red);
        btnBuildingInfo3.gameObject.SetActive(!SelectedBuilding.EnableFracForever());

        if (SelectedBuilding.ID == IFE点数聚集塔) {
            s = $"{"点数聚集效率层次：".Translate()}{PointAggregateTower.Level}";
            textBuildingInfo4.text = s.WithPALvColor(PointAggregateTower.Level);
            textBuildingInfo4.enabled = true;
            btnTip4.gameObject.SetActive(true);
            btnBuildingInfo4.gameObject.SetActive(!PointAggregateTower.IsMaxLevel);
        } else {
            textBuildingInfo4.enabled = false;
            btnTip4.gameObject.SetActive(false);
            btnBuildingInfo4.gameObject.SetActive(false);
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
        int takeCount = 2;
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} {"启用流动输出集装".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
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
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} {"+1 产物输出集装数目".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
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
        int takeCount = 3;
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} {"启用分馏永动".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
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
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} {"+1 点数聚集效率层次".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                PointAggregateTower.Level++;
            },
            null);
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
