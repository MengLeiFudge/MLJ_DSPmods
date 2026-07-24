using CommonAPI.Systems;
using FE.Compatibility.Mods;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

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
        tech分馏数据中心.AddItems = [];
        tech分馏数据中心.AddItemCounts = [];
        tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
        tech分馏数据中心.PropertyItemCounts = [10];
        tech分馏数据中心.IconTag = "flsjzx";


        var tech阶段补给1 = ProtoRegistry.RegisterTech(
            TFE阶段补给1, "T阶段补给1", "阶段补给1描述", "阶段补给1结果", "Assets/fe/tech阶段补给",
            [TFE分馏数据中心],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(0, 1)
        );
        tech阶段补给1.PreTechsImplicit = [TFE培养塔];
        tech阶段补给1.AddItems = [IFE残片];
        tech阶段补给1.AddItemCounts = [300];
        tech阶段补给1.PropertyOverrideItems = [I电磁矩阵];
        tech阶段补给1.PropertyItemCounts = [100];
        tech阶段补给1.IconTag = "tczlb1";

        var tech阶段补给2 = ProtoRegistry.RegisterTech(
            TFE阶段补给2, "T阶段补给2", "阶段补给2描述", "阶段补给2结果", "Assets/fe/tech阶段补给",
            [TFE阶段补给1],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(0, 2)
        );
        tech阶段补给2.PreTechsImplicit = [T能量矩阵];
        tech阶段补给2.AddItems = [IFE残片];
        tech阶段补给2.AddItemCounts = [400];
        tech阶段补给2.PropertyOverrideItems = [I能量矩阵];
        tech阶段补给2.PropertyItemCounts = [100];
        tech阶段补给2.IconTag = "tczlb2";

        var tech阶段补给3 = ProtoRegistry.RegisterTech(
            TFE阶段补给3, "T阶段补给3", "阶段补给3描述", "阶段补给3结果", "Assets/fe/tech阶段补给",
            [TFE阶段补给2],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(0, 3)
        );
        tech阶段补给3.PreTechsImplicit = [T结构矩阵];
        tech阶段补给3.AddItems = [IFE残片];
        tech阶段补给3.AddItemCounts = [500];
        tech阶段补给3.PropertyOverrideItems = [I结构矩阵];
        tech阶段补给3.PropertyItemCounts = [100];
        tech阶段补给3.IconTag = "tczlb3";

        var tech阶段补给4 = ProtoRegistry.RegisterTech(
            TFE阶段补给4, "T阶段补给4", "阶段补给4描述", "阶段补给4结果", "Assets/fe/tech阶段补给",
            [TFE阶段补给3],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(0, 4)
        );
        tech阶段补给4.PreTechsImplicit = [T信息矩阵];
        tech阶段补给4.AddItems = [IFE残片];
        tech阶段补给4.AddItemCounts = [600];
        tech阶段补给4.PropertyOverrideItems = [I信息矩阵];
        tech阶段补给4.PropertyItemCounts = [100];
        tech阶段补给4.IconTag = "tczlb4";

        var tech阶段补给5 = ProtoRegistry.RegisterTech(
            TFE阶段补给5, "T阶段补给5", "阶段补给5描述", "阶段补给5结果", "Assets/fe/tech阶段补给",
            [TFE阶段补给4],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(0, 5)
        );
        tech阶段补给5.PreTechsImplicit = [T引力矩阵];
        tech阶段补给5.AddItems = [IFE残片];
        tech阶段补给5.AddItemCounts = [800];
        tech阶段补给5.PropertyOverrideItems = [I引力矩阵];
        tech阶段补给5.PropertyItemCounts = [100];
        tech阶段补给5.IconTag = "tczlb5";

        var tech阶段补给6 = ProtoRegistry.RegisterTech(
            TFE阶段补给6, "T阶段补给6", "阶段补给6描述", "阶段补给6结果", "Assets/fe/tech阶段补给",
            [TFE阶段补给5],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(0, 6)
        );
        tech阶段补给6.PreTechsImplicit = [T宇宙矩阵];
        tech阶段补给6.AddItems = [IFE残片];
        tech阶段补给6.AddItemCounts = [1200];
        tech阶段补给6.PropertyOverrideItems = [I宇宙矩阵];
        tech阶段补给6.PropertyItemCounts = [100];
        tech阶段补给6.IconTag = "tczlb6";

        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [TFE分馏数据中心],
            [], [], 3600,
            [],
            GetTechPos(1, 1)
        );
        tech分馏塔原胚.AddItems = [IFE培养塔, IFE通用原胚];
        tech分馏塔原胚.AddItemCounts = [1, 80];
        tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
        tech分馏塔原胚.PropertyItemCounts = [100];
        tech分馏塔原胚.IconTag = "tfltyp";

        var tech培养塔 = ProtoRegistry.RegisterTech(
            TFE培养塔, "T培养塔", "培养塔描述", "培养塔结果", "Assets/fe/tech物品交互",
            [],
            [], [], 3600000,
            [],
            GetTechPos(1, 2)
        );
        tech培养塔.PreTechsImplicit = [TFE分馏塔原胚];
        tech培养塔.PropertyOverrideItems = [I电磁矩阵];
        tech培养塔.PropertyItemCounts = [200];
        tech培养塔.IconTag = "twpjh";

        var tech解析塔 = ProtoRegistry.RegisterTech(
            TFE解析塔, "T解析塔", "解析塔描述", "解析塔结果", "Assets/fe/tech物品分解",
            [],
            [], [], 3600000,
            [],
            GetTechPos(1, 3)
        );
        tech解析塔.PreTechsImplicit = [TFE分馏塔原胚];
        tech解析塔.PropertyOverrideItems = [I电磁矩阵];
        tech解析塔.PropertyItemCounts = [200];
        tech解析塔.IconTag = "twpjl";

        var tech复制塔 = ProtoRegistry.RegisterTech(
            TFE复制塔, "T复制塔", "复制塔描述", "复制塔结果", "Assets/fe/tech资源复制",
            [],
            [], [], 3600000,
            [],
            GetTechPos(1, 4)
        );
        tech复制塔.PreTechsImplicit = [TFE分馏塔原胚];
        tech复制塔.PropertyOverrideItems = [I电磁矩阵];
        tech复制塔.PropertyItemCounts = [200];
        tech复制塔.IconTag = "tkwfz";

        var tech转化塔 = ProtoRegistry.RegisterTech(
            TFE转化塔, "T转化塔", "转化塔描述", "转化塔结果", "Assets/fe/tech物品转化",
            [],
            [], [], 3600000,
            [],
            GetTechPos(1, 5)
        );
        tech转化塔.PreTechsImplicit = [TFE分馏塔原胚];
        tech转化塔.PropertyOverrideItems = [I电磁矩阵];
        tech转化塔.PropertyItemCounts = [200];
        tech转化塔.IconTag = "twpzh";

        var tech行星物流系统 = LDB.techs.Select(T行星物流系统);
        var tech行星内物流交互 = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果", tech行星物流系统.IconPath,
            [],
            [..tech行星物流系统.Items], [..tech行星物流系统.ItemPoints], tech行星物流系统.HashNeeded,
            [RFE行星内物流交互站],
            GetTechPos(1, 6)
        );
        tech行星内物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE培养塔, tech行星物流系统.ID];
        tech行星内物流交互.PropertyOverrideItems = [..tech行星物流系统.PropertyOverrideItems];
        tech行星内物流交互.PropertyItemCounts = [..tech行星物流系统.PropertyItemCounts];
        tech行星内物流交互.IconTag = "txxnjh";

        var tech星际物流系统 = LDB.techs.Select(T星际物流系统);
        var tech星际物流交互 = ProtoRegistry.RegisterTech(
            TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果", tech星际物流系统.IconPath,
            [],
            [..tech星际物流系统.Items], [..tech星际物流系统.ItemPoints], tech星际物流系统.HashNeeded,
            [RFE星际物流交互站],
            GetTechPos(1, 7)
        );
        tech星际物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE培养塔, tech星际物流系统.ID];
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
}
