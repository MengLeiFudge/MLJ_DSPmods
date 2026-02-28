using System.Collections.Generic;
using FE.Logic.Building;
using FE.Logic.Manager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

public class PointAggregateRecipe : BaseRecipe {
    public override ERecipe RecipeType => ERecipe.PointAggregate;

    public PointAggregateRecipe(int inputID) 
        : base(inputID, 1.0f, [new(1.0f, inputID, 1)], []) { }

    public override void GetOutputs(ref uint seed, float pointsBonus,
        float successRatioBonus, float mainOutputCountBonus, float appendOutputRatioBonus,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        
        // 点数聚集逻辑：如果平均增产等级足够，则有概率聚集成功
        float ratio = fluidInputIncAvg >= PointAggregateTower.MaxInc ? (PointAggregateTower.Level / 20.0f) : 0;
        
        if (GetRandDouble(ref seed) < ratio) {
            // 成功聚集：消耗 MaxInc 点数，产出一个原物品
            inputChange = -1;
            outputs = new List<ProductOutputInfo> { new(true, InputID, 1) };
            fluidInputInc -= PointAggregateTower.MaxInc;
            return;
        }

        // 失败：直通
        inputChange = -1;
        outputs = ProcessManager.emptyOutputs;
        fluidInputInc -= fluidInputIncAvg;
    }

    public override byte GetOutputInc(int itemId) => (byte)PointAggregateTower.MaxInc;
}
