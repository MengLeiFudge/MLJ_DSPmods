using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Mods;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 定义资源塔输入同种资源并复制为更多同种资源的基础配方。
/// </summary>
public class MineralCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有矿物复制配方
    /// </summary>
    public static void CreateAll() {
        Create(I木材, 0.05f);
        Create(I植物燃料, 0.05f);
        Create(I铁矿, 0.05f);
        Create(I铜矿, 0.05f);
        Create(I硅石, 0.05f);
        Create(I钛石, 0.05f);
        Create(I石矿, 0.05f);
        Create(I煤矿, 0.05f);
        Create(I水, 0.05f);
        Create(I原油, 0.05f);
        Create(I硫酸, 0.05f);
        Create(I氢, 0.05f);
        Create(I重氢, 0.05f);
        if (GenesisBook.Enable) {
            Create(IGB钨矿, 0.05f);
            Create(IGB铝矿, 0.05f);
            Create(IGB硫矿, 0.05f);
            Create(IGB放射性矿物, 0.05f);
            Create(IGB海水, 0.05f);
            Create(IGB盐酸, 0.05f);
            Create(IGB硝酸, 0.05f);
            Create(IGB氨, 0.05f);
            Create(IGB氮, 0.05f);
            Create(IGB氧, 0.05f);
            Create(IGB氦, 0.05f);
            Create(IGB氦三, 0.05f);
            Create(IGB二氧化碳, 0.05f);
            Create(IGB二氧化硫, 0.05f);
        }
        if (OrbitalRing.Enable) {
            Create(IOR黄铁矿, 0.05f);
            Create(IOR铀矿, 0.05f);
            Create(IOR石墨矿, 0.05f);
        }

        Create(I可燃冰, 0.05f);
        Create(I金伯利矿石, 0.05f);
        Create(I分形硅石, 0.05f);
        Create(I光栅石, 0.05f);
        Create(I刺笋结晶, 0.05f);
        Create(I单极磁石, 0.05f);
        Create(I有机晶体, 0.05f);
        Create(I黑雾矩阵, 0.05f);
        Create(I硅基神经元, 0.05f);
        Create(I物质重组器, 0.05f);
        Create(I负熵奇点, 0.05f);
        Create(I核心素, 0.05f);
        Create(I能量碎片, 0.05f);

        //添加其他矿物的复制配方
        foreach (VeinProto vein in LDB.veins.dataArray) {
            if (GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, vein.MiningItem) == null) {
                Create(vein.MiningItem, 0.05f);
                LogWarning($"自动添加其他矿物复制配方，物品{LDB.items.Select(vein.MiningItem).name}");
            }
        }
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item.Type == EItemType.Resource
                && GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, item.ID) == null
                && LDB.recipes.dataArray.Any(recipe =>
                    recipe.Items.Contains(item.ID) || recipe.Results.Contains(item.ID))) {
                Create(item.ID, 0.05f);
                LogWarning($"自动添加其他矿物复制配方，物品{item.name}");
            }
        }
    }

    private static void Create(int inputID, float baseSuccessRatio) {
        // 临界光子和反物质必须保留戴森球、射线接收与质能转换的原版产线边界。
        if (inputID is I临界光子 or I反物质 || itemValue[inputID] >= maxValue) {
            return;
        }
        AddRecipe(new MineralCopyRecipe(inputID, baseSuccessRatio,
            [
                new(1.000f, inputID, 2),
            ],
            []));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.MineralCopy;

    /// <summary>
    /// 创建矿物复制塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    /// <summary>
    /// 初始化 MineralCopyRecipe 的新实例。
    /// </summary>
    public MineralCopyRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }
}
