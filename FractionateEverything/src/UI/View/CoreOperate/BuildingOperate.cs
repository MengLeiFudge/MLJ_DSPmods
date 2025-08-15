using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class BuildingOperate {
    public static RectTransform _windowTrans;

    #region 建筑操作

    public static ConfigEntry<int> BuildingTypeEntry;
    public static ItemProto SelectedBuilding => LDB.items.Select(BuildingIds[BuildingTypeEntry.Value]);
    public static string[] BuildingTypeNames = [
        "交互塔".Translate(), "矿物复制塔".Translate(), "点数聚集塔".Translate(),
        "量子复制塔".Translate(), "点金塔".Translate(), "分解塔".Translate(), "转化塔".Translate()
    ];
    public static int[] BuildingIds = [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔];

    private static Text textBuildingInfo1;
    private static UIButton btnBuildingInfo1;
    private static Text textBuildingInfo2;
    private static UIButton btnBuildingInfo2;
    private static Text textBuildingInfo3;
    private static UIButton btnBuildingInfo3;
    private static Text textBuildingInfo4;
    private static UIButton btnTip4;
    private static UIButton btnBuildingInfo4;

    #endregion

    public static void LoadConfig(ConfigFile configFile) {
        BuildingTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Building Type", 0, "想要查看的建筑类型。");
        if (BuildingTypeEntry.Value < 0 || BuildingTypeEntry.Value >= BuildingTypeNames.Length) {
            BuildingTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        var tab = wnd.AddTab(trans, "建筑操作");
        float x = 0f;
        float y = 10f;
        wnd.AddComboBox(x, y, tab, "建筑类型").WithItems(BuildingTypeNames).WithSize(150f, 0f)
            .WithConfigEntry(BuildingTypeEntry);
        y += 36f;
        wnd.AddText2(x, y, tab, "建筑加成：", 15, "text-building-info-0");
        y += 36f;
        textBuildingInfo1 = wnd.AddText2(x, y, tab, "流动输出堆叠", 15, "text-building-info-1");
        wnd.AddTipsButton2(x + 200, y + 6, tab, "流动输出堆叠",
            "启用后，流动输出（即侧面的输出）会尽可能以4堆叠进行输出。");
        btnBuildingInfo1 = wnd.AddButton(x + 350, y, tab, "启用", 16, "button-enable-fluid-output-stack",
            SetFluidOutputStack);
        y += 36f;
        textBuildingInfo2 = wnd.AddText2(x, y, tab, "产物输出最大堆叠", 15, "text-building-info-2");
        wnd.AddTipsButton2(x + 200, y + 6, tab, "产物输出最大堆叠",
            "产物输出（即正面的输出）会尽可能以该项的数目进行输出。");
        btnBuildingInfo2 = wnd.AddButton(x + 350, y, tab, "堆叠+1", 16, "button-add-max-product-output-stack",
            AddMaxProductOutputStack);
        y += 36f;
        textBuildingInfo3 = wnd.AddText2(x, y, tab, "分馏永动", 15, "text-building-info-3");
        wnd.AddTipsButton2(x + 200, y + 6, tab, "分馏永动",
            "启用后，当产物缓存到达一定数目后，建筑将不再处理输入的物品，而是直接将其搬运到流动输出。\n该功能可以确保环路的持续运行。");
        btnBuildingInfo3 = wnd.AddButton(x + 350, y, tab, "启用", 16, "button-enable-frac-forever",
            SetFracForever);
        y += 36f;
        textBuildingInfo4 = wnd.AddText2(x, y, tab, "点数聚集效率层次", 15, "text-building-info-4");
        btnTip4 = wnd.AddTipsButton2(x + 200, y + 6, tab, "点数聚集效率层次",
            "点数聚集的效率层次会影响产物的输出速率、产物的最大增产点数，上限为7。");
        btnBuildingInfo4 = wnd.AddButton(x + 350, y, tab, "层次+1", 16, "button-add-point-aggregate-level",
            AddPointAggregateLevel);
        y += 36f;
    }

    public static void UpdateUI() {
        textBuildingInfo1.text = SelectedBuilding.EnableFluidOutputStack()
            ? "已启用流动输出堆叠".WithColor(Orange)
            : "未启用流动输出堆叠".WithColor(Red);
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnBuildingInfo1.gameObject.SetActive(!SelectedBuilding.EnableFluidOutputStack());

        string s = $"产物输出堆叠：{SelectedBuilding.MaxProductOutputStack()}";
        textBuildingInfo2.text = SelectedBuilding.MaxProductOutputStack() >= 4
            ? s.WithColor(Orange)
            : s.WithQualityColor(SelectedBuilding.MaxProductOutputStack());
        btnBuildingInfo2.gameObject.SetActive(SelectedBuilding.MaxProductOutputStack() < 4);

        textBuildingInfo3.text = SelectedBuilding.EnableFracForever()
            ? "已启用分馏永动".WithColor(Orange)
            : "未启用分馏永动".WithColor(Red);
        btnBuildingInfo3.gameObject.SetActive(!SelectedBuilding.EnableFracForever());

        if (SelectedBuilding.ID == IFE点数聚集塔) {
            s = $"点数聚集效率层次：{PointAggregateTower.Level}";
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
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 启用流动输出堆叠吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFluidOutputStack(true);
                UIMessageBox.Show("提示", "已启用流动输出堆叠！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    private static void AddMaxProductOutputStack() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        if (SelectedBuilding.MaxProductOutputStack() >= 4) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 将产物输出堆叠 +1 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.MaxProductOutputStack(SelectedBuilding.MaxProductOutputStack() + 1);
                UIMessageBox.Show("提示", "已将产物输出堆叠 +1！",
                    "确定", UIMessageBox.INFO);
            }, null);
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
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 启用分馏永动吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                SelectedBuilding.EnableFracForever(true);
                UIMessageBox.Show("提示", "已启用分馏永动！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    private static void AddPointAggregateLevel() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int takeId = IFE分馏塔增幅芯片;
        int takeCount = 1;
        if (itemValue[takeId] >= maxValue || takeCount == 0) {
            return;
        }
        if (PointAggregateTower.Level >= 7) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 将点数聚集效率层次 +1 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                PointAggregateTower.Level++;
                UIMessageBox.Show("提示", "已将点数聚集效率层次 +1！",
                    "确定", UIMessageBox.INFO);
            }, null);
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
