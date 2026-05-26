from pathlib import Path
import re
import unittest


RUNTIME = Path("FractionateEverything/src/Logic/Station/Runtime.cs")
POOL = Path("FractionateEverything/src/Logic/Station/ProliferatorPool.cs")


def extract_method(text: str, method_name: str) -> str:
    match = re.search(rf"(?:public|private) static [^{{]+ {method_name}\([^)]*\) \{{", text)
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
    def test_download_batch_is_sprayed_but_station_storage_is_not_periodically_sprayed(self):
        text = RUNTIME.read_text(encoding="utf-8-sig")
        runtime_loop = text[text.index("for (int i = 0; i < stationComponent.storage.Length; i++) {"):
                            text.index("/// <summary>设置交互站某槽位的目标物品数量并消耗对应电力</summary>")]
        set_target = extract_method(text, "SetTargetCount")

        self.assertNotIn("TryAutoSprayStationStore(ref store);", runtime_loop)
        self.assertNotIn("finally", set_target)
        self.assertNotIn("AddIncToItem(store.count, ref store.inc)", set_target)
        self.assertIn("AddIncToDownloadedItem(count, ref inc, PlanetaryInteractionStation.Level >= 12);", set_target)
        self.assertIn("PlanetaryInteractionStation.Level >= 3", set_target)

    def test_upload_still_uses_split_inc_without_download_spray(self):
        text = RUNTIME.read_text(encoding="utf-8-sig")
        set_target = extract_method(text, "SetTargetCount")
        upload_branch = set_target[set_target.index("} else {"):]

        self.assertIn("split_inc(ref store.count, ref store.inc, count)", upload_branch)
        self.assertNotIn("AddIncToDownloadedItem", upload_branch)

    def test_download_spray_uses_two_stage_point_cost(self):
        text = POOL.read_text(encoding="utf-8-sig")
        helper = extract_method(text, "AddIncToDownloadedItem")

        self.assertIn("AddIncToItem(itemCount, ref itemInc);", helper)
        self.assertIn("!allowOverdrive || itemInc < standardTargetTotal", helper)
        self.assertIn("const int overdrivePointCost = 3;", helper)
        self.assertIn("EnsureLeftInc(needInc * overdrivePointCost);", helper)
        self.assertIn("leftInc -= addedInc * overdrivePointCost;", helper)


if __name__ == "__main__":
    unittest.main()
