using System;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    public static void AddTechs() {
        var tech分馏数据中心 = ProtoRegistry.RegisterTech(
            TFE分馏数据中心, "T分馏数据中心", "分馏数据中心描述", "分馏数据中心结果", "Assets/fe/tech分馏数据中心",
            GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
            //注：哈希块是3600的x倍时，实际需要的物品数目为当前数目*x
            [I电磁矩阵], [10], 3600,
            [],
            GetTechPos(1, 0)
        );
        tech分馏数据中心.PreTechsImplicit = [T电磁矩阵];
        tech分馏数据中心.AddItems = [IFE交互塔原胚];
        tech分馏数据中心.AddItemCounts = [80];//20用于解锁分馏塔原胚科技，60赠送
        tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
        tech分馏数据中心.PropertyItemCounts = [10];
        tech分馏数据中心.IconTag = "flsjzx";


        var tech超值礼包1 = ProtoRegistry.RegisterTech(
            TFE超值礼包1, "T超值礼包1", "超值礼包1描述", "超值礼包1结果", "Assets/fe/tech超值礼包",
            [TFE分馏数据中心],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(0, 1)
        );
        tech超值礼包1.PreTechsImplicit = [TFE物品交互];
        tech超值礼包1.AddItems = [IFE残片, IFE交互塔原胚];
        tech超值礼包1.AddItemCounts = [300, 20];
        tech超值礼包1.PropertyOverrideItems = [I电磁矩阵];
        tech超值礼包1.PropertyItemCounts = [100];
        tech超值礼包1.IconTag = "tczlb1";

        var tech超值礼包2 = ProtoRegistry.RegisterTech(
            TFE超值礼包2, "T超值礼包2", "超值礼包2描述", "超值礼包2结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包1],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(0, 2)
        );
        tech超值礼包2.PreTechsImplicit = [T能量矩阵];
        tech超值礼包2.AddItems = [IFE残片, IFE矿物复制塔原胚];
        tech超值礼包2.AddItemCounts = [400, 20];
        tech超值礼包2.PropertyOverrideItems = [I能量矩阵];
        tech超值礼包2.PropertyItemCounts = [100];
        tech超值礼包2.IconTag = "tczlb2";

        var tech超值礼包3 = ProtoRegistry.RegisterTech(
            TFE超值礼包3, "T超值礼包3", "超值礼包3描述", "超值礼包3结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包2],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(0, 3)
        );
        tech超值礼包3.PreTechsImplicit = [T结构矩阵];
        tech超值礼包3.AddItems = [IFE残片, IFE点数聚集塔原胚];
        tech超值礼包3.AddItemCounts = [500, 10];
        tech超值礼包3.PropertyOverrideItems = [I结构矩阵];
        tech超值礼包3.PropertyItemCounts = [100];
        tech超值礼包3.IconTag = "tczlb3";

        var tech超值礼包4 = ProtoRegistry.RegisterTech(
            TFE超值礼包4, "T超值礼包4", "超值礼包4描述", "超值礼包4结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包3],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(0, 4)
        );
        tech超值礼包4.PreTechsImplicit = [T信息矩阵];
        tech超值礼包4.AddItems = [IFE残片, IFE转化塔原胚];
        tech超值礼包4.AddItemCounts = [600, 10];
        tech超值礼包4.PropertyOverrideItems = [I信息矩阵];
        tech超值礼包4.PropertyItemCounts = [100];
        tech超值礼包4.IconTag = "tczlb4";

        var tech超值礼包5 = ProtoRegistry.RegisterTech(
            TFE超值礼包5, "T超值礼包5", "超值礼包5描述", "超值礼包5结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包4],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(0, 5)
        );
        tech超值礼包5.PreTechsImplicit = [T引力矩阵];
        tech超值礼包5.AddItems = [IFE残片, IFE精馏塔原胚];
        tech超值礼包5.AddItemCounts = [800, 10];
        tech超值礼包5.PropertyOverrideItems = [I引力矩阵];
        tech超值礼包5.PropertyItemCounts = [100];
        tech超值礼包5.IconTag = "tczlb5";

        var tech超值礼包6 = ProtoRegistry.RegisterTech(
            TFE超值礼包6, "T超值礼包6", "超值礼包6描述", "超值礼包6结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包5],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(0, 6)
        );
        tech超值礼包6.PreTechsImplicit = [T宇宙矩阵];
        tech超值礼包6.AddItems = [IFE残片, IFE分馏塔定向原胚];
        tech超值礼包6.AddItemCounts = [1200, 2];
        tech超值礼包6.PropertyOverrideItems = [I宇宙矩阵];
        tech超值礼包6.PropertyItemCounts = [100];
        tech超值礼包6.IconTag = "tczlb6";

        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [TFE分馏数据中心],
            [IFE交互塔原胚], [20], 3600,
            [],
            GetTechPos(1, 1)
        );
        tech分馏塔原胚.AddItems = [IFE交互塔, IFE交互塔原胚, IFE矿物复制塔原胚, IFE分馏塔定向原胚];
        tech分馏塔原胚.AddItemCounts = [1, 30, 30, 20];
        tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
        tech分馏塔原胚.PropertyItemCounts = [100];
        tech分馏塔原胚.IconTag = "tfltyp";

        var tech物品交互 = ProtoRegistry.RegisterTech(
            TFE物品交互, "T物品交互", "物品交互描述", "物品交互结果", "Assets/fe/tech物品交互",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE交互塔],
            GetTechPos(1, 2)
        );
        tech物品交互.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品交互.PropertyOverrideItems = [I电磁矩阵];
        tech物品交互.PropertyItemCounts = [200];
        tech物品交互.IconTag = "twpjh";

        var tech矿物复制 = ProtoRegistry.RegisterTech(
            TFE矿物复制, "T矿物复制", "矿物复制描述", "矿物复制结果", "Assets/fe/tech矿物复制",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE矿物复制塔],
            GetTechPos(1, 3)
        );
        tech矿物复制.PreTechsImplicit = [TFE分馏塔原胚];
        tech矿物复制.PropertyOverrideItems = [I电磁矩阵];
        tech矿物复制.PropertyItemCounts = [200];
        tech矿物复制.IconTag = "tkwfz";

        var tech增产点数聚集 = ProtoRegistry.RegisterTech(
            TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果", "Assets/fe/tech增产点数聚集",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE点数聚集塔],
            GetTechPos(1, 4)
        );
        tech增产点数聚集.PreTechsImplicit = [TFE分馏塔原胚];
        tech增产点数聚集.PropertyOverrideItems = [I电磁矩阵];
        tech增产点数聚集.PropertyItemCounts = [200];
        tech增产点数聚集.IconTag = "zcdsjj";

        var tech物品转化 = ProtoRegistry.RegisterTech(
            TFE物品转化, "T物品转化", "物品转化描述", "物品转化结果", "Assets/fe/tech物品转化",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE转化塔],
            GetTechPos(1, 5)
        );
        tech物品转化.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品转化.PropertyOverrideItems = [I电磁矩阵];
        tech物品转化.PropertyItemCounts = [200];
        tech物品转化.IconTag = "twpzh";

        var tech物品精馏 = ProtoRegistry.RegisterTech(
            TFE物品精馏, "T物品精馏", "物品精馏描述", "物品精馏结果", "Assets/fe/tech物品分解",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE精馏塔],
            GetTechPos(1, 6)
        );
        tech物品精馏.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品精馏.PropertyOverrideItems = [I电磁矩阵];
        tech物品精馏.PropertyItemCounts = [200];
        tech物品精馏.IconTag = "twpjl";

        var tech行星物流系统 = LDB.techs.Select(T行星物流系统);
        var tech行星内物流交互 = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果", tech行星物流系统.IconPath,
            [],
            [..tech行星物流系统.Items], [..tech行星物流系统.ItemPoints], tech行星物流系统.HashNeeded,
            [RFE行星内物流交互站],
            GetTechPos(1, 7)
        );
        tech行星内物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE物品交互, tech行星物流系统.ID];
        tech行星内物流交互.PropertyOverrideItems = [..tech行星物流系统.PropertyOverrideItems];
        tech行星内物流交互.PropertyItemCounts = [..tech行星物流系统.PropertyItemCounts];
        tech行星内物流交互.IconTag = "txxnjh";

        var tech星际物流系统 = LDB.techs.Select(T星际物流系统);
        var tech星际物流交互 = ProtoRegistry.RegisterTech(
            TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果", tech星际物流系统.IconPath,
            [],
            [..tech星际物流系统.Items], [..tech星际物流系统.ItemPoints], tech星际物流系统.HashNeeded,
            [RFE星际物流交互站],
            GetTechPos(1, 8)
        );
        tech星际物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE物品交互, tech星际物流系统.ID];
        tech星际物流交互.PropertyOverrideItems = [..tech星际物流系统.PropertyOverrideItems];
        tech星际物流交互.PropertyItemCounts = [..tech星际物流系统.PropertyItemCounts];
        tech星际物流交互.IconTag = "txjjh";
    }

    /// <summary>
    /// 根据输入的行列，生成科技所在位置。
    /// </summary>
    /// <param name="row">从0开始，数字越大越靠下</param>
    /// <param name="column">从0开始，数字越大越靠右</param>
    /// <returns></returns>
    private static Vector2 GetTechPos(int row, int column) {
        if (GenesisBook.Enable) {
            return new(9 + column * 4, -47 - row * 4);
        }
        if (OrbitalRing.Enable) {
            return new(8 + column * 4, -76 - row * 4);
        }
        return new(13 + column * 4, -67 - row * 4);
    }

    /// <summary>
    /// 判断某一主线矩阵层的有限科技是否已全部研究完成。
    /// 隐藏科技与无限科技不参与该判定。
    /// </summary>
}
