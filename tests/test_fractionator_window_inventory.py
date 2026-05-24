from pathlib import Path
import re
import unittest


SOURCE = Path(
    "FractionateEverything/src/Logic/Fractionation/Presentation/FractionatorWindow/Inventory.cs"
)


class FractionatorWindowInventoryTests(unittest.TestCase):
    def test_slot_count_writes_back_to_fractionator_pool_ref(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn(
            "ref FractionatorComponent fractionator =\n"
            "            ref __instance.factorySystem.fractionatorPool[__instance.fractionatorId];",
            text,
        )
        self.assertIn(
            "private static void SetModSlotCount(ref FractionatorComponent fractionator",
            text,
        )
        self.assertNotRegex(
            text,
            re.compile(
                r"FractionatorComponent\s+fractionator\s*=\s*__instance\.factorySystem"
                r"\.fractionatorPool\[__instance\.fractionatorId\];"
            ),
        )

    def test_product_slot_takeout_updates_primary_product_mirror(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("SetModSlotCount(ref fractionator, products, itemId, 0, slot);", text)
        self.assertIn(
            "if (slotProduct.itemId == fractionator.productId && slotProduct.isMainOutput)",
            text,
        )
        self.assertIn("fractionator.productOutputCount = count;", text)


if __name__ == "__main__":
    unittest.main()
