from pathlib import Path
import re
import unittest


RUNTIME = Path("FractionateEverything/src/Logic/Station/Runtime.cs")


def extract_method(text: str, method_name: str) -> str:
    match = re.search(rf"private static [^{{]+ {method_name}\([^)]*\) \{{", text)
    if not match:
        raise AssertionError(f"{method_name} method not found")

    start = match.start()
    depth = 0
    for index in range(match.end() - 1, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start:index + 1]
    raise AssertionError(f"{method_name} method body not closed")


class StationAutoSprayTests(unittest.TestCase):
    def test_auto_spray_is_periodic_slot_behavior_not_set_target_finally(self):
        text = RUNTIME.read_text(encoding="utf-8-sig")
        runtime_loop = text[text.index("for (int i = 0; i < stationComponent.storage.Length; i++) {"):
                            text.index("/// <summary>设置交互站某槽位的目标物品数量并消耗对应电力</summary>")]
        set_target = extract_method(text, "SetTargetCount")

        self.assertIn("TryAutoSprayStationStore(ref store);", runtime_loop)
        self.assertNotIn("finally", set_target)
        self.assertNotIn("AddIncToItem(store.count, ref store.inc)", set_target)

    def test_auto_spray_helper_keeps_level_and_count_guards(self):
        text = RUNTIME.read_text(encoding="utf-8-sig")
        helper = extract_method(text, "TryAutoSprayStationStore")

        self.assertIn("PlanetaryInteractionStation.Level < 3", helper)
        self.assertIn("store.count <= 0", helper)
        self.assertIn("AddIncToItem(store.count, ref store.inc);", helper)


if __name__ == "__main__":
    unittest.main()
