using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 点金塔配方类（1A -> X矩阵1 + Y矩阵2 + Z矩阵3）
/// </summary>
public class AlchemyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有点金配方
    /// </summary>
    public static void CreateAll() {
        List<int> matrixList = LDB.items.dataArray
            .Where(item => item.Type == EItemType.Matrix).Select(item => item.ID).ToList();
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方通用核心
                || item.ID == IFE分馏塔增幅芯片
                || !item.GridIndexValid()
                || item.ID == I沙土) {
                continue;
            }
            //点金塔不能处理矩阵（包括配方原材料含有矩阵的物品），也不能处理建筑
            if (item.Type == EItemType.Matrix
                || (item.maincraft != null && item.maincraft.Items.Any(itemID => matrixList.Contains(itemID)))
                || item.BuildMode != 0) {
                continue;
            }
            int matrixID = itemToMatrix[item.ID];
            float matrixValue = itemValue[matrixID];
            //关联度越高的物品，点金成功率越大。
            //如果某个原料是制作矩阵的第n层原料，那么点金成功率增加0.5/n，点金价值增加0.1/n
            float successRate = 0.05f;
            float valueFactor = itemValue[item.ID] / matrixValue;
            // 获取物品与矩阵的关联度
            Dictionary<int, int> relationDepth = [];
            CalcRelation(item.ID, matrixID, 1, relationDepth);
            // 根据关联度调整成功率和价值
            if (relationDepth.TryGetValue(matrixID, out int depth)) {
                if (depth > 0) {
                    successRate *= 1 + 0.5f / depth;
                    valueFactor *= 1 + 0.1f / depth;
                }
            }
            AddRecipe(new AlchemyRecipe(item.ID, successRate,
                [
                    new OutputInfo(1.0f, matrixID, valueFactor),
                ],
                [
                    new OutputInfo(0.01f, IFE点金精华, 1),
                ]));
        }
    }

    /// <summary>
    /// 计算物品与矩阵的关联深度
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="matrixId">矩阵ID</param>
    /// <param name="depth">当前深度</param>
    /// <param name="relationDepth">关联深度字典</param>
    private static void CalcRelation(int itemId, int matrixId, int depth, Dictionary<int, int> relationDepth) {
        // 如果已经计算过这个物品的关联深度，并且新的深度更大，则跳过
        if (relationDepth.TryGetValue(itemId, out int existingDepth) && existingDepth <= depth) {
            return;
        }
        // 记录当前物品的关联深度
        relationDepth[itemId] = depth;
        // 如果当前物品就是目标矩阵，则关联深度为0
        if (itemId == matrixId) {
            relationDepth[itemId] = 0;
            return;
        }
        // 获取物品的主要制作配方
        var recipe = LDB.items.Select(itemId)?.maincraft;
        if (recipe == null) return;
        // 递归计算所有原料的关联深度
        for (int i = 0; i < recipe.Items.Length; i++) {
            int materialId = recipe.Items[i];
            CalcRelation(materialId, matrixId, depth + 1, relationDepth);
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Alchemy;

    /// <summary>
    /// 创建点金塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public AlchemyRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public override float MainOutputCountInc => 1.0f + (IsMaxQuality ? 0.01f * Level : 0);

    /// <summary>
    /// 附加产物数目增幅
    /// </summary>
    public override float AppendOutputCountInc => 1.0f + (Quality - 1) * 0.25f;

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        int version = r.ReadInt32();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.Write(1);
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
