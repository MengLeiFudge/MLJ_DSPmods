import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
UXA_SRC = ROOT / "UXAEnhance" / "src"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


class UxaEnhanceTests(unittest.TestCase):
    def test_uses_uxassist_config_window_callback_instead_of_createui_patch(self):
        plugin = read_text(UXA_SRC / "UXAEnhancePlugin.cs")
        ui_patch = read_text(UXA_SRC / "UXAssistConfigWindowPatch.cs")

        self.assertIn("MyConfigWindow.OnUICreated += UXAssistConfigWindowPatch.OnUxAssistConfigWindowCreated", plugin)
        self.assertIn("MyConfigWindow.OnUICreated -= UXAssistConfigWindowPatch.OnUxAssistConfigWindowCreated", plugin)
        self.assertNotIn("using HarmonyLib;", plugin)
        self.assertNotIn("PatchAll", plugin)
        self.assertNotIn("HarmonyPatch(typeof(UIConfigWindow), \"CreateUI\")", ui_patch)
        self.assertNotIn("using UXAssist;", ui_patch)

    def test_ils_boolean_options_have_apply_buttons_and_service_targets(self):
        ui_patch = read_text(UXA_SRC / "UXAssistConfigWindowPatch.cs")
        targets = read_text(UXA_SRC / "AutoConfigApplyTarget.cs")
        service = read_text(UXA_SRC / "AutoConfigGlobalApplyService.cs")

        self.assertIn("AddBooleanApplyButton(wnd, tab, AutoConfigApplyTarget.IlsIncludeOrbitCollector", ui_patch)
        self.assertIn("AddBooleanApplyButton(wnd, tab, AutoConfigApplyTarget.IlsWarperNecessary", ui_patch)
        self.assertIn("IlsIncludeOrbitCollector", targets)
        self.assertIn("IlsWarperNecessary", targets)
        self.assertIn("station.includeOrbitCollector = LogisticsPatch.AutoConfigILSIncludeOrbitCollector.Value", service)
        self.assertIn("station.warperNecessary = LogisticsPatch.AutoConfigILSWarperNecessary.Value", service)

    def test_charging_power_apply_tips_explain_uxa_units(self):
        ui_patch = read_text(UXA_SRC / "UXAssistConfigWindowPatch.cs")

        self.assertIn("星际物流运输站：1 = 15MW", ui_patch)
        self.assertIn("行星物流运输站：1 = 3MW", ui_patch)
        self.assertIn("物流配送器：1 = 0.3MW", ui_patch)


if __name__ == "__main__":
    unittest.main()
