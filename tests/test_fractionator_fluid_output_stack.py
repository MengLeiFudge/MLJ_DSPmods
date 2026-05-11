from pathlib import Path
import unittest


SOURCE = Path("FractionateEverything/src/Logic/Fractionation/Process/ProcessManager.cs")


class FractionatorFluidOutputStackTests(unittest.TestCase):
    def test_enhanced_fluid_output_uses_input_stack_when_it_exceeds_tower_stack(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("GetPreferredFluidOutputStack", text)
        self.assertIn("Math.Max(fluidStack, inputStack)", text)
        self.assertIn("GetPreferredFluidOutputStack(enableFluidEnhancement, fluidStack, fluidInputCountPerCargo)", text)

    def test_non_enhanced_fluid_output_keeps_vanilla_average_input_stack(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("int inputStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));", text)
        self.assertIn("return enableFluidEnhancement ? Math.Max(fluidStack, inputStack) : inputStack;", text)


if __name__ == "__main__":
    unittest.main()
