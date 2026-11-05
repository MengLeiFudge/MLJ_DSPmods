using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using FE.UI.MainPanel.Civilization;
using FE.UI.MainPanel.Theme;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>按塔型集中展示配方发现、解析与解锁；只为已解锁配方公开完整投入产出。</summary>
/// <param name="type">本页所属的生产塔型。</param>
public sealed class RecipeGalleryPage(ERecipe type) : CardBrowserPage {
    protected override string Title => LDB.items.Select(type.GetSpriteItemId())?.name ?? type.GetName();
    protected override string Summary => "FE.Recipes.Summary".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetRecipeRecordSnapshots(type)
        .Select(record => record.State == AnalysisRecordState.Completed
            ? CreateRecipeCard(RecipeManager.GetRecipe(record.Key)) : AnalysisRecordCards.Create(record)).ToArray();

    /// <summary>完整配方详情沿用运行公式，区分催化剂持有量、抽中产量与平均产量。</summary>
    private static PageCard CreateRecipeCard(WorldviewRecipe recipe) {
        float probability = recipe.IsAdvanced
            ? AdvancedFractionatorRuntime.GetSuccessProbability(recipe,
                ResearchManager.GetAdvancedTowerSacrificeBoost(recipe.RecipeType.GetSpriteItemId()))
            : recipe.CycleSuccessProbability;
        string inputs = string.Join("\n", recipe.Inputs.Select(input =>
            $"{LDB.items.Select(input.ItemId)?.name} ×{input.Count}"
            + (input.ConsumeProbability < 1 ? string.Format(" · 消耗概率{0:P0}".Translate(), input.ConsumeProbability) : "")));
        string outputs = string.Join("\n", recipe.Products.Select(output => {
            string name = LDB.items.Select(output.ItemId)?.name;
            if (recipe.Kind != WorldviewRecipeKind.Replacement) {
                return $"{name} ×{recipe.GetOutputPerSuccess(output):0.###}";
            }
            double chance = recipe.GetOutputProbability(output);
            return string.Format("FE.Recipes.Candidate".Translate(), name, recipe.GetOutputPerSuccess(output),
                chance > 0 && chance < 0.0001 ? "<0.01%" : chance.ToString("P2"),
                recipe.GetExpectedOutputPerSuccess(output));
        }));
        return new PageCard {
            Key = recipe.StableId, Title = recipe.DisplayName, ItemId = recipe.PrimaryProductId,
            Status = "已恢复".Translate(), Progress = 1f,
            DetailItems = recipe.Inputs.Select(i => i.ItemId).Concat(recipe.Products.Select(p => p.ItemId)).Distinct().ToArray(),
            Detail = (recipe.IsAdvanced ? "在对应高级塔选择本配方，使用矩阵供养。".Translate()
                    : "基础矿物塔自动匹配输入矿物。".Translate())
                + "\n\n" + string.Format("成功率 {0:P0}".Translate(), probability)
                + "\n\n" + "FE.Recipes.Inputs".Translate() + "\n" + inputs
                + "\n\n" + (recipe.Kind == WorldviewRecipeKind.Replacement ? "FE.Recipes.Candidates" : "FE.Recipes.Outputs").Translate()
                + "\n" + outputs + "FE.RecipeGalleryPage.Effects".Translate(),
        };
    }
}
