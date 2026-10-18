using System;
using System.Linq;

namespace FE.Logic.Civilization;

/// <summary>从最终科技树读取科研材料；阶段身份由调用方声明，不从材料顺序反推。</summary>
internal static class MatrixResearchCosts {
    /// <summary>六级成长以无人机引擎各级为成本参照；兼容模组修改后的材料与比例。</summary>
    private static readonly int[] ReferenceTechIds = [2601, 2602, 2603, 2604, 2605, 2606];

    /// <summary>取得参考科技，原型缺失时明确中断注册，避免生成免费的成长科技。</summary>
    /// <param name="stage">从零开始的FE阶段。</param>
    /// <returns>当前科技树的参考原型。</returns>
    public static TechProto GetReference(int stage) {
        if (stage < 0 || stage >= ReferenceTechIds.Length) { throw new ArgumentOutOfRangeException(nameof(stage)); }
        TechProto reference = LDB.techs.Select(ReferenceTechIds[stage]);
        if (reference?.Items == null || reference.Items.Length == 0
            || reference.ItemPoints == null || reference.Items.Length != reference.ItemPoints.Length
            || reference.ItemPoints.Any(points => points <= 0)) {
            throw new InvalidOperationException($"FE科研参考科技{ReferenceTechIds[stage]}没有有效材料成本。");
        }
        return reference;
    }

    /// <summary>按参考科技的材料比例与研究效率设置成本，预算表示最多消耗的单种材料件数。</summary>
    /// <param name="tech">待配置科技。</param>
    /// <param name="stage">从零开始的FE阶段。</param>
    /// <param name="budget">首级最大单种材料预算。</param>
    /// <param name="increment">每后续等级增加的单种材料预算。</param>
    public static void Apply(TechProto tech, int stage, int budget, int increment = 0) {
        TechProto reference = GetReference(stage);
        int maxPoints = reference.ItemPoints.Max();
        long firstHash = (long)Math.Ceiling(Math.Max(1, budget) * 3600d / maxPoints);
        int addedHash = checked((int)Math.Ceiling(Math.Max(0, increment) * 3600d / maxPoints));
        tech.Items = (int[])reference.Items.Clone();
        tech.ItemPoints = (int[])reference.ItemPoints.Clone();
        tech.LevelCoef1 = addedHash;
        tech.LevelCoef2 = 0;
        tech.HashNeeded = firstHash - (long)tech.Level * addedHash;
        tech.PropertyOverrideItems = (int[])tech.Items.Clone();
        tech.PropertyItemCounts = tech.ItemPoints.Select(points =>
            checked((int)Math.Ceiling(firstHash * (double)points / 3600d))).ToArray();
        tech.PreTechsImplicit = GetMatrixPrerequisites(tech.Items);
    }

    /// <summary>读取所有科研材料的制造前置，兼容非连续ID和模组新增矩阵。</summary>
    /// <param name="items">实际科研材料。</param>
    /// <returns>去重后的材料解锁科技，不包含不存在的零ID。</returns>
    private static int[] GetMatrixPrerequisites(int[] items) => items
        .SelectMany(itemId => {
            ItemProto item = LDB.items.Select(itemId);
            if (item?.preTech != null) { return new[] { item.preTech.ID }; }
            int[] recipes = LDB.recipes.dataArray.Where(recipe => recipe?.Results?.Contains(itemId) == true)
                .Select(recipe => recipe.ID).ToArray();
            return LDB.techs.dataArray.Where(tech => tech?.UnlockRecipes?.Any(recipes.Contains) == true)
                .Select(tech => tech.ID).ToArray();
        }).Distinct().OrderBy(id => id).ToArray();

    /// <summary>仅替换既有科技的阶段材料，保留其原本预算、研究工作量和解锁效果。</summary>
    /// <param name="tech">既有科技。</param>
    /// <param name="stage">目标成本阶段。</param>
    public static void Retier(TechProto tech, int stage) {
        TechProto reference = GetReference(stage);
        int points = tech.ItemPoints?.Length > 0 ? tech.ItemPoints.Max() : 0;
        if (points <= 0) { throw new InvalidOperationException($"科技{tech.ID}没有有效研究成本。"); }
        int referenceMax = reference.ItemPoints.Max();
        tech.Items = (int[])reference.Items.Clone();
        tech.ItemPoints = reference.ItemPoints.Select(value =>
            Math.Max(1, checked((int)Math.Round(points * (double)value / referenceMax)))).ToArray();
        tech.PropertyOverrideItems = (int[])tech.Items.Clone();
        long hash = tech.GetHashNeeded(tech.Level);
        tech.PropertyItemCounts = tech.ItemPoints.Select(value =>
            checked((int)Math.Ceiling(hash * (double)value / 3600d))).ToArray();
        tech.PreTechsImplicit = GetMatrixPrerequisites(tech.Items);
    }
}
