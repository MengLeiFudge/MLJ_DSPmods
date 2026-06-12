from pathlib import Path
import unittest


SOURCE = Path("FractionateEverything/src/Logic/Fractionation/Process/ProcessManager.cs")


class FractionatorFluidOutputStackTests(unittest.TestCase):
    def test_enhanced_fluid_output_uses_input_stack_when_it_exceeds_tower_stack(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("GetPreferredFluidOutputStack", text)
        self.assertIn("Math.Max(fluidStack, inputStack)", text)
        self.assertIn(
            "GetPreferredFluidOutputStack(enableFluidEnhancement, fluidStack, fluidInputCountPerCargo,",
            text,
        )

    def test_non_enhanced_fluid_output_keeps_vanilla_average_input_stack(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("int inputStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));", text)
        self.assertIn("return enableFluidEnhancement ? Math.Max(fluidStack, inputStack) : inputStack;", text)

    def test_missing_or_locked_recipe_fluid_output_forces_single_stack_passthrough(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("bool canProcessRecipe = recipe != null && RecipeGrowthQueries.IsUnlocked(recipe);", text)
        self.assertIn("bool moveDirectly = !canProcessRecipe;", text)
        self.assertIn("enableFracForever && !moveDirectly", text)
        self.assertIn("forceSingleStack: moveDirectly", text)
        self.assertIn("if (forceSingleStack) {\n            return 1;\n        }", text)

    def test_mineral_replication_traits_only_apply_to_processable_recipes(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("if (enableMassEnergyFission && canProcessRecipe && __instance.fluidInputCount > 0)", text)
        self.assertIn(
            "if (isMineralReplicationTower\n            && MineralReplicationTower.EnableZeroPressureCycle\n            && canProcessRecipe)",
            text,
        )

    def test_zero_pressure_replenishes_input_then_fluid_output_before_product_output(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertNotIn("if (!hasFluidOutputBelt)", text)
        self.assertNotIn("productOutputReserve", text)
        fluid_to_input = text.index("int fluidMoveCount = Math.Min(__instance.fluidOutputCount, needForInput);")
        product_to_input = text.index("int moveToInput = Math.Min(mainProduct.count, productNeedForInput);")
        product_to_output = text.index("int moveToOutput = Math.Min(mainProduct.count, needForOutput);")
        product_belt_output = text.index("SelectProductForBeltOutput(products, productStack, lockedOutputId,")

        self.assertLess(fluid_to_input, product_to_input)
        self.assertLess(product_to_input, product_to_output)
        self.assertLess(product_to_output, product_belt_output)

    def test_enhanced_fluid_output_can_fill_partial_head_stack(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("TryInsertFluidOutputAtHead", text)
        self.assertIn(
            "cargoPath.TryUpdateItemAtHeadAndFillBlank(itemId, maxStack, (byte)outputStack,",
            text,
        )
        self.assertIn("insertedStack = 1;", text)
        self.assertIn(
            "RemoveFluidOutput(ref fractionator, insertedStack, fluidOutputIncAvg);",
            text,
        )
        self.assertNotIn("cargoTraffic.TryInsertItemAtHead(beltId", text)


if __name__ == "__main__":
    unittest.main()
