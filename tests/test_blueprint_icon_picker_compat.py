import unittest
from pathlib import Path


ITEM_PICKER_PATCH_CANDIDATES = [
    Path("FractionateEverything/src/Logic/Items/Presentation/ItemPickerDuplicateGridPatch.cs"),
    Path("FractionateEverything/src/UI/Patches/ItemPickerDuplicateGridPatch.cs"),
]
SIGNAL_PICKER_PATCH_CANDIDATES = [
    Path("FractionateEverything/src/Logic/Items/Presentation/SignalPickerDuplicateGridPatch.cs"),
    Path("FractionateEverything/src/UI/Patches/SignalPickerDuplicateGridPatch.cs"),
]
DECOMPILED_SOURCE = Path("gamedata/DecompiledSource/Assembly-CSharp")


def existing_path(candidates):
    for path in candidates:
        if path.exists():
            return path
    raise AssertionError("未找到候选文件: " + ", ".join(str(path) for path in candidates))


def read_text(path):
    return path.read_text(encoding="utf-8-sig")


class BlueprintIconPickerCompatTests(unittest.TestCase):
    def test_duplicate_grid_patches_cover_item_and_signal_pickers(self):
        item_picker_text = read_text(existing_path(ITEM_PICKER_PATCH_CANDIDATES))
        signal_picker_text = read_text(existing_path(SIGNAL_PICKER_PATCH_CANDIDATES))

        self.assertIn("HarmonyPatch(typeof(UIItemPicker), nameof(UIItemPicker.RefreshIcons))", item_picker_text)
        self.assertIn("protoArray", item_picker_text)

        self.assertIn("HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))", signal_picker_text)
        self.assertIn("signalArray", signal_picker_text)
        self.assertIn("SignalProtoSet.SignalId(ESignalType.Item", signal_picker_text)
        self.assertNotIn("HarmonyPrepare", signal_picker_text)

    def test_signal_picker_duplicate_grid_patch_handles_known_item_pages(self):
        signal_picker_text = read_text(existing_path(SIGNAL_PICKER_PATCH_CANDIDATES))

        self.assertIn("currentType == 2", signal_picker_text)
        self.assertIn("return 1", signal_picker_text)
        self.assertIn("currentType == 3", signal_picker_text)
        self.assertIn("return 2", signal_picker_text)
        self.assertIn("GenesisBook.Enable ? 17 : 14", signal_picker_text)
        self.assertIn("GenesisBook.Enable ? 7 : 10", signal_picker_text)
        self.assertIn("OrbitalRing.Enable", signal_picker_text)

    def test_blueprint_icon_entrypoints_when_decompiled_source_is_available(self):
        blueprint_inspector = DECOMPILED_SOURCE / "UIBlueprintInspector.cs"
        blueprint_book_inspector = DECOMPILED_SOURCE / "UIBlueprintBookInspector.cs"
        if not blueprint_inspector.exists() or not blueprint_book_inspector.exists():
            self.skipTest("本地未包含忽略目录 gamedata/DecompiledSource")

        blueprint_text = read_text(blueprint_inspector)
        blueprint_book_text = read_text(blueprint_book_inspector)

        self.assertIn("UISignalPicker.Popup(new Vector2(50f, 350f), OnSignalPickerReturn)", blueprint_text)
        self.assertIn("UIItemPicker.Popup(new Vector2(50f, 350f), OnItemPickerReturn)", blueprint_book_text)


if __name__ == "__main__":
    unittest.main()
