using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分解塔配方类（1A -> X原材料1 + Y原材料2 + Z原材料3 + I精华1 + J精华2 + K精华3）
/// </summary>
public class DeconstructionRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有分解配方
    /// </summary>
    public static void CreateAll() {
        float maxInc = 1.0f + (float)Cargo.incTableMilli[4];
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方核心
                || item.ID == IFE分馏塔增幅芯片
                || !item.GridIndexValid()
                || item.ID == I沙土) {
                continue;
            }
            List<OutputInfo> outputMain = [];
            bool mainCraftValid = false;
            if (item.Type != EItemType.Resource && item.maincraft != null) {
                var recipe = item.maincraft;
                // 复制配方数据
                List<int> inputIDs = recipe.Items.ToList();
                List<int> outputIDs = recipe.Results.ToList();
                List<int> inputCounts = recipe.ItemCounts.ToList();
                List<int> outputCounts = recipe.ResultCounts.ToList();
                // 抵消输入输出中的相同物品
                bool haveSameItem;
                do {
                    haveSameItem = false;
                    for (int i = 0; i < inputIDs.Count; i++) {
                        for (int j = 0; j < outputIDs.Count; j++) {
                            if (inputIDs[i] == outputIDs[j]) {
                                // 比较数量大小并抵消
                                if (inputCounts[i] > outputCounts[j]) {
                                    inputCounts[i] -= outputCounts[j];
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                } else if (inputCounts[i] < outputCounts[j]) {
                                    outputCounts[j] -= inputCounts[i];
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                } else {
                                    // 数量相等，完全抵消
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                }
                                haveSameItem = true;
                                break;
                            }
                        }
                        if (haveSameItem) break;
                    }
                } while (haveSameItem);
                // 检查配方是否可用
                mainCraftValid = inputIDs.Count > 0 && outputIDs.Count == 1 && outputIDs[0] == item.ID;
                if (mainCraftValid) {
                    List<float> inputFloatCounts = [..inputCounts];
                    int outputCount = outputCounts[outputIDs.IndexOf(item.ID)];
                    for (int i = 0; i < inputFloatCounts.Count; i++) {
                        inputFloatCounts[i] /= outputCount;
                    }
                    float totalInputCount = inputFloatCounts.Sum();
                    // 如果配方允许增产，则只能按照1.0/1.25返还；否则，按照1.0返还
                    // 对于可增产的配方，只要制作时增产点数超过4点，数目会变多
                    // 对于只能加速的配方，只有分解配方达到金色，数目才会变多
                    float outputCountPlus = recipe.productive ? 1.0f / maxInc : 1.0f;
                    for (int i = 0; i < inputFloatCounts.Count; i++) {
                        outputMain.Add(new(inputFloatCounts[i] / totalInputCount, inputIDs[i],
                            totalInputCount * outputCountPlus));
                    }
                }
            }
            if (!mainCraftValid) {
                outputMain.Add(new(1.0f, I沙土, itemValue[item.ID] / itemValue[I沙土] * 2.0f));
            }
            AddRecipe(new DeconstructionRecipe(item.ID, 0.05f,
                outputMain,
                [
                    new OutputInfo(0.01f, IFE分解精华, 1),
                ]));
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Deconstruction;

    /// <summary>
    /// 创建分解塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public DeconstructionRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

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
