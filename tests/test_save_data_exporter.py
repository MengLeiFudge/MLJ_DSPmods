from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


PROJECT = Path("SaveDataExporter/SaveDataExporter.csproj")
SOURCE = Path("SaveDataExporter/src/SaveDataExporterPlugin.cs")
README = Path("SaveDataExporter/README.md")
MANIFEST = Path("SaveDataExporter/Assets/manifest.json")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


class SaveDataExporterTests(unittest.TestCase):
    def test_project_is_packaged_commonapi_mod(self):
        root = ET.fromstring(read_text(PROJECT))

        self.assertEqual(root.findtext(".//OutputType"), "Library")
        self.assertEqual(root.findtext(".//PackageId"), "SaveDataExporter")
        self.assertEqual(root.findtext(".//BepInExPluginGuid"), "com.menglei.dsp.savedataexporter")

        package_refs = {node.attrib["Include"] for node in root.findall(".//PackageReference")}
        self.assertIn("BepInEx.Core", package_refs)
        self.assertIn("DysonSphereProgram.Modding.CommonAPI", package_refs)
        self.assertIn("DysonSphereProgram.GameLibs", package_refs)
        references = {node.attrib["Include"] for node in root.findall(".//Reference")}
        self.assertIn("System.IO.Compression", references)

    def test_hotkey_starts_unbound_and_requires_loaded_save(self):
        text = read_text(SOURCE)

        self.assertIn("CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>", text)
        self.assertIn("new CombineKey(0, 0, ECombineKeyAction.OnceClick, true)", text)
        self.assertIn("LocalizationModule.RegisterTranslation(\"KEY\" + ExportKeyId", text)
        self.assertIn("GameMain.isRunning", text)
        self.assertIn("gameData?.galaxy != null", text)
        self.assertIn("gameData.statistics?.production?.factoryStatPool != null", text)
        self.assertIn("未载入存档，跳过导出", text)

    def test_export_matches_template_shape_and_statistics_source(self):
        text = read_text(SOURCE)

        self.assertIn("calculator.AddFactory(i);", text)
        self.assertIn("calculator.CalculateImmediately();", text)
        self.assertIn("productStat.total[productionLevel] / divisor", text)
        self.assertIn("productStat.refProductSpeed", text)
        self.assertIn("productStat.total[consumptionLevel] / divisor", text)
        self.assertIn("productStat.refConsumeSpeed", text)
        self.assertIn("星球信息导出模板1", text)
        self.assertIn("星球信息导出模板2", text)
        self.assertIn("实际产量", text)
        self.assertIn("理论产量", text)
        self.assertIn("实际消耗", text)
        self.assertIn("理论消耗", text)
        self.assertIn("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", text)

    def test_output_file_name_mode_supports_timestamped_and_overwrite_modes(self):
        text = read_text(SOURCE)

        self.assertIn("ConfigEntry<OutputFileNameMode> outputFileNameModeEntry", text)
        self.assertIn("\"OutputFileNameMode\"", text)
        self.assertIn("OutputFileNameMode.TimestampedNewFile", text)
        self.assertIn("OutputFileNameMode.SaveNameOverwrite", text)
        self.assertIn("SaveDataExporter_{saveName}.xlsx", text)
        self.assertIn("SaveDataExporter_{saveName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx", text)
        self.assertIn("FileMode.Create", text)

    def test_output_file_name_mode_is_added_to_game_misc_settings_page(self):
        text = read_text(SOURCE)

        self.assertIn("HarmonyPatch(typeof(UIOptionWindow), \"_OnOpen\")", text)
        self.assertIn("HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))", text)
        self.assertIn("HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnRevertButtonClick))", text)
        self.assertIn("content-5", text)
        self.assertIn("OptionRowStartY = -220f", text)
        self.assertIn("OptionRowStepY = 40f", text)
        self.assertIn("GetNextOptionRowY", text)
        self.assertIn("outputFileNameModeParent = parent", text)
        self.assertIn("FindDirectChild(outputFileNameModeParent, OptionWindowLabelName)", text)
        self.assertIn("outputFileNameModeComboBox.Items.Clear()", text)
        self.assertIn("org.LoShin.GenesisBook", text)
        self.assertIn("org.ProfessorCat305.OrbitalRing", text)
        self.assertIn("导出文件命名模式", text)
        self.assertIn("固定存档名（覆盖同名文件）", text)

    def test_user_docs_describe_usage_and_empty_hotkey(self):
        readme = read_text(README)
        manifest = read_text(MANIFEST)

        self.assertIn("默认无按键", readme)
        self.assertIn("主页或未载入存档时不会导出", readme)
        self.assertIn("BepInEx/config/SaveDataExporter", readme)
        self.assertIn("1143,6006", readme)
        self.assertIn("OutputFileNameMode", readme)
        self.assertIn("游戏设置的“杂项”页面", readme)
        self.assertIn("TimestampedNewFile", readme)
        self.assertIn("SaveNameOverwrite", readme)
        self.assertIn("SaveDataExporter_<存档名>.xlsx", readme)
        self.assertIn("SaveDataExporter", manifest)


if __name__ == "__main__":
    unittest.main()
