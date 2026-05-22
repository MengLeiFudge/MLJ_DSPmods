from pathlib import Path
import unittest


SOURCE = Path("FractionateEverything/src/Logic/Manager/ProcessManager.cs")


class FractionatorFluidOutputStackTests(unittest.TestCase):
    def test_missing_or_locked_recipe_fluid_output_forces_single_stack_passthrough(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("bool moveDirectly = recipe == null || recipe.Locked;", text)
        self.assertIn("int outputCargoStack = moveDirectly", text)
        self.assertIn("!building.EnableFluidOutputStack() || moveDirectly", text)
        self.assertIn("outputCargoStack, 1,", text)
        self.assertIn("无配方/配方未解锁等直通状态不应用塔等级集装输出", text)


if __name__ == "__main__":
    unittest.main()
