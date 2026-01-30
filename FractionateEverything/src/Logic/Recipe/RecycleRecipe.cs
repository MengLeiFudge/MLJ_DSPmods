using System.Collections.Generic;
using System.IO;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 回收塔配方类
/// 将物品按照主要配方（itemproto.maincraft)回收为制作物品所需原材料的25%
/// 在回收过程中，得到的产物有12.4%概率提高品质
/// </summary>
public class RecycleRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有回收配方
    /// </summary>
    public static void CreateAll() {
        // // 遍历所有物品，为有maincraft配方的物品创建回收配方
        // foreach (ItemProto item in LDB.items.dataArray) {
        //     List<OutputInfo> outputs = [];
        //     RecipeProto mainRecipe = item.maincraft;
        //     // 只有物品满足一定条件，且主配方满足一定条件的情况下，才按照配方进行还原
        //     // 否则，只能产出自己
        //     if (item.Type != EItemType.Resource
        //         && item.Type != EItemType.Material
        //         && mainRecipe != null
        //         && mainRecipe.Type != ERecipeType.Smelt
        //         && mainRecipe.Type != ERecipeType.Chemical
        //         && mainRecipe.Type != ERecipeType.Fractionate
        //         && mainRecipe.Type != ERecipeType.Refine
        //         && mainRecipe.Type != ERecipeType.Particle
        //         && mainRecipe.Items.Length > 0
        //         && mainRecipe.Results.Length == 1) {
        //         for (int i = 0; i < mainRecipe.Items.Length; i++) {
        //             int materialId = mainRecipe.Items[i];
        //             int materialCount = mainRecipe.ItemCounts[i];
        //             outputs.Add(new(0.25f, materialId, materialCount));
        //         }
        //         AddRecipe(new RecycleRecipe(item.ID, 1.0f, outputs, []));
        //     } else {
        //         AddRecipe(new RecycleRecipe(item.ID, 1.0f, [new(0.25f, item.ID, 1)], []));
        //     }
        // }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Recycle;

    /// <summary>
    /// 创建回收配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public RecycleRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 重写GetOutputs方法，添加品质提升逻辑
    /// </summary>
    public override List<ProductOutputInfo> GetOutputs(byte quality, ref uint seed, float pointsBonus,
        float buffBonus1, float buffBonus2, float buffBonus3) {
        // 调用基类方法获取基础输出
        List<ProductOutputInfo> baseOutputs =
            base.GetOutputs(quality, ref seed, pointsBonus, buffBonus1, buffBonus2, buffBonus3);
        // 如果没有输出（损毁或无变化），直接返回
        if (baseOutputs == null || baseOutputs.Count == 0) {
            return baseOutputs;
        }
        // 处理品质提升逻辑
        List<ProductOutputInfo> finalOutputs = [];
        foreach (ProductOutputInfo output in baseOutputs) {
            if (!output.isMainOutput) {
                finalOutputs.Add(output);
                continue;
            }
            int itemId = output.itemId;
            int count = output.count;
            int[] newItemIdArr = new int[11];
            for (int i = 0; i < count; i++) {
                int newItemId = DetermineQualityIncrease(ref seed, itemId);
                newItemIdArr[GetQuality(newItemId)]++;
            }
            for (int i = 0; i < newItemIdArr.Length; i++) {
                if (newItemIdArr[i] > 0) {
                    finalOutputs.Add(new(output.isMainOutput, GetQualityItemId(itemId, (byte)i), count));
                }
            }
        }
        return finalOutputs;
    }

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
