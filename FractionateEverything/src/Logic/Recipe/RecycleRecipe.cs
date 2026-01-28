using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonAPI.Systems;
using FE.Utils;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 回收塔配方类
/// 将物品按照主要配方(itemproto.maincraft)回收为制作物品所需原材料的25%
/// 在回收过程中，得到的产物有12.4%概率提高品质
/// </summary>
public class RecycleRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有回收配方
    /// </summary>
    public static void CreateAll() {
        // 遍历所有物品，为有maincraft配方的物品创建回收配方
        foreach (ItemProto item in LDB.items.dataArray) {
            // 获取物品的主要配方
            RecipeProto mainRecipe = item.maincraft;
            if (mainRecipe == null) {
                continue;
            }
            // 跳过没有输入材料的配方
            if (mainRecipe.Items.Length == 0) {
                continue;
            }
            // 跳过输出多个物品的配方（只处理单一产物的配方）
            if (mainRecipe.Results.Length != 1) {
                continue;
            }

            // 计算25%的材料产出
            List<OutputInfo> outputs = new List<OutputInfo>();
            for (int i = 0; i < mainRecipe.Items.Length; i++) {
                int materialId = mainRecipe.Items[i];
                int materialCount = mainRecipe.ItemCounts[i];
                outputs.Add(new OutputInfo(0.25f, materialId, materialCount));
            }

            // 如果没有有效的输出，跳过
            if (outputs.Count == 0) continue;

            // 创建回收配方，基础成功率100%
            AddRecipe(new RecycleRecipe(item.ID, 1.0f, outputs, []));
        }
        //遍历配方，为每个配方添加对应的高品质版本配方
        foreach (RecipeProto recipe in LDB.recipes.dataArray) {
            if (recipe.ID > 1000 && recipe.ID < 10000) {
                int[] qualityArr = [10, 100, 1000, 10000];
                foreach (int multi in qualityArr) {
                    ProtoRegistry.RegisterRecipe(recipe.ID * multi,
                        recipe.Type, recipe.TimeSpend,
                        recipe.Items.Select(id => id * multi).ToArray(), recipe.ItemCounts,
                        recipe.Results.Select(id => id * multi).ToArray(), recipe.ResultCounts,
                        recipe.Description, recipe.preTech?.ID ?? 0, recipe.GridIndex, recipe.Name, recipe.IconPath);
                }
            }
        }
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
    public new List<ProductOutputInfo> GetOutputs(ref uint seed, float pointsBonus,
        float buffBonus1, float buffBonus2, float buffBonus3) {
        // 调用基类方法获取基础输出
        List<ProductOutputInfo> baseOutputs =
            base.GetOutputs(ref seed, pointsBonus, buffBonus1, buffBonus2, buffBonus3);

        // 如果没有输出（损毁或无变化），直接返回
        if (baseOutputs == null || baseOutputs.Count == 0) {
            return baseOutputs;
        }

        // 处理品质提升逻辑
        List<ProductOutputInfo> finalOutputs = new List<ProductOutputInfo>();

        foreach (ProductOutputInfo output in baseOutputs) {
            int itemId = output.itemId;
            int count = output.count;

            // 12.4%概率触发品质提升
            if (GetRandDouble(ref seed) < 0.124) {
                // 获取当前物品的品质等级
                int currentQuality = QualitySystem.GetQualityLevel(itemId);

                // 如果无法识别品质或已经是最高品质，不提升
                if (currentQuality < 1 || currentQuality >= QualitySystem.MaxQuality) {
                    finalOutputs.Add(output);
                    continue;
                }

                // 根据当前品质确定提升星级的概率分布
                int qualityIncrease = DetermineQualityIncrease(ref seed, currentQuality);

                // 计算新的品质等级
                int newQuality = currentQuality + qualityIncrease;
                if (newQuality > QualitySystem.MaxQuality) {
                    newQuality = QualitySystem.MaxQuality;
                }

                // 获取基础物品ID
                int baseItemId = QualitySystem.GetBaseItemId(itemId);
                if (baseItemId < 0) {
                    // 无法识别基础ID，保持原样
                    finalOutputs.Add(output);
                    continue;
                }

                // 计算新的品质物品ID
                int newItemId = QualitySystem.GetQualityItemId(baseItemId, newQuality);
                if (newItemId < 0) {
                    // 无法生成新ID，保持原样
                    finalOutputs.Add(output);
                    continue;
                }

                // 添加提升品质后的物品
                finalOutputs.Add(new ProductOutputInfo(output.isMainOutput, newItemId, count));
            } else {
                // 不提升品质，保持原样
                finalOutputs.Add(output);
            }
        }

        return finalOutputs;
    }

    /// <summary>
    /// 根据当前品质确定提升的星级数
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="currentQuality">当前品质等级（1-5）</param>
    /// <returns>提升的星级数（1-3）</returns>
    private int DetermineQualityIncrease(ref uint seed, int currentQuality) {
        double rand = GetRandDouble(ref seed);

        if (currentQuality <= 3) {
            // 一星、二星、三星物品：90%提升1星，9%提升2星，0.9%提升3星
            if (rand < 0.9) return 1;
            if (rand < 0.99) return 2;
            return 3;
        } else if (currentQuality == 4) {
            // 四星物品：90%提升1星，10%提升2星
            if (rand < 0.9) return 1;
            return 2;
        } else {
            // 五星物品：不提升（但这个分支理论上不会被调用，因为外层已经检查）
            return 0;
        }
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
