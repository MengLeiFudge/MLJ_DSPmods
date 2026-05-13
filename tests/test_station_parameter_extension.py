from pathlib import Path
import unittest


SOURCE = Path("FractionateEverything/src/Logic/Station/StationParameterPatch.cs")
MODE_STATE = Path("FractionateEverything/src/Logic/Station/ModeState.cs")


class StationParameterExtensionTests(unittest.TestCase):
    def test_parameter_patch_uses_building_parameter_common_paths(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("InteractionStationParamMagic", text)
        self.assertIn("StationBaseParameterLength = 2048", text)
        self.assertIn("slotCount * 2", text)
        self.assertIn("AppendInteractionStationParams", text)
        self.assertIn("ApplyInteractionStationParams", text)

    def test_parameter_patch_covers_blueprint_q_copy_and_direct_paste(self):
        text = SOURCE.read_text(encoding="utf-8-sig")

        self.assertIn("typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData)", text)
        self.assertIn("typeof(BuildingParameters), nameof(BuildingParameters.CopyFromFactoryObject)", text)
        self.assertIn("typeof(BuildingParameters), nameof(BuildingParameters.GenerateBuildPreviews)", text)
        self.assertIn("typeof(BuildingParameters), nameof(BuildingParameters.ApplyPrebuildParametersToEntity)", text)
        self.assertIn("typeof(BuildingParameters), nameof(BuildingParameters.PasteToFactoryObject)", text)
        self.assertIn("typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown)", text)

    def test_mode_state_exposes_normalized_parameter_helpers(self):
        text = MODE_STATE.read_text(encoding="utf-8-sig")

        self.assertIn("TryGetSlotModes", text)
        self.assertIn("SetSlotModes", text)
        self.assertIn("RemoveSlotModes", text)
        self.assertIn("NormalizeTransferMode(transferMode)", text)
        self.assertIn("NormalizeCapacityMode(capacityMode)", text)


if __name__ == "__main__":
    unittest.main()
