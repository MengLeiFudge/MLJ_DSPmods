from pathlib import Path
import unittest


RECIPE = Path("FractionateEverything/src/Logic/Fractionation/FracRecipes/PointAggregateRecipe.cs")
POOL = Path("FractionateEverything/src/Logic/Station/ProliferatorPool.cs")
PROCESS = Path("FractionateEverything/src/Logic/Fractionation/Process/ProcessManager.cs")
BUFFER = Path("FractionateEverything/src/Logic/Fractionation/FracRecipes/ProductOutputBuffer.cs")
BUILDING_UI = Path("FractionateEverything/src/UI/MainPanel/CoreOperate/BuildingOperate.cs")
RECIPE_UI = Path("FractionateEverything/src/UI/MainPanel/CoreOperate/FracRecipeOperate.cs")
TOWER = Path("FractionateEverything/src/Logic/Fractionation/Fractionators/PointAggregateTower.cs")
README = Path("FractionateEverything/README.md")


class PointAggregateTowerTests(unittest.TestCase):
    def test_success_ratio_keeps_common_fractionator_meaning(self):
        text = RECIPE.read_text(encoding="utf-8-sig")

        self.assertIn("new(item.ID, 0.25f", text)
        self.assertIn("RollBinomialApprox(ref seed, batchCount, GetCandidateSuccessRatio(successBoost))", text)
        self.assertNotIn("fluidInputIncAvg / 10.0f * SuccessRatio", text)
        self.assertNotIn("PointAggregateTower.MaxInc * 7", text)

    def test_batch_uses_total_input_points_and_optional_global_pool(self):
        text = RECIPE.read_text(encoding="utf-8-sig")

        self.assertIn("int batchInputInc = TakeBatchInputInc", text)
        self.assertIn("GetPayableSuccessCount(candidateSuccessCount, batchInputInc", text)
        self.assertIn("PointAggregateTower.EnableVoidAggregation", text)
        self.assertIn("ProliferatorPool.GetAvailableInc()", text)
        self.assertIn("ProliferatorPool.TryConsumeInc(usedPoolInc)", text)
        self.assertIn("PassThroughInc = batchInputInc - usedInputInc", text)

    def test_process_manager_uses_recipe_reported_passthrough_points(self):
        buffer_text = BUFFER.read_text(encoding="utf-8-sig")
        process_text = PROCESS.read_text(encoding="utf-8-sig")

        self.assertIn("public int PassThroughInc;", buffer_text)
        self.assertIn("__instance.fluidOutputInc += batchResult.PassThroughInc;", process_text)
        self.assertNotIn("__instance.fluidOutputInc += fluidInputIncAvg * batchResult.PassThroughCount;", process_text)

    def test_global_point_pool_exposes_locked_consume_api(self):
        text = POOL.read_text(encoding="utf-8-sig")

        self.assertIn("public static int GetAvailableInc()", text)
        self.assertIn("public static bool TryConsumeInc(int need)", text)
        self.assertIn("lock (centerItemCount)", text)
        self.assertIn("if (leftInc < need)", text)

    def test_trait_and_docs_use_void_aggregation_language(self):
        tower_text = TOWER.read_text(encoding="utf-8-sig")
        building_ui_text = BUILDING_UI.read_text(encoding="utf-8-sig")
        recipe_ui_text = RECIPE_UI.read_text(encoding="utf-8-sig")
        readme_text = README.read_text(encoding="utf-8-sig")

        self.assertIn("EnableVoidAggregation", tower_text)
        self.assertIn("虚空聚集", building_ui_text)
        self.assertIn("全局增产点数池", building_ui_text)
        self.assertIn("PointAggregateTower.EnableVoidAggregation", recipe_ui_text)
        self.assertIn("global point pool", readme_text)
        self.assertNotIn("双重点数", building_ui_text)
        self.assertNotIn("双倍点数", recipe_ui_text)


if __name__ == "__main__":
    unittest.main()
