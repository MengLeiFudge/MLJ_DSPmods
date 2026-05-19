import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(".")
AFTERBUILD_CSPROJ = ROOT / "AfterBuildEvent" / "AfterBuildEvent.csproj"
LOCAL_LIBRARY_PROJECTS = [
    ROOT / "FractionateEverything" / "FractionateEverything.csproj",
    ROOT / "GetDspData" / "GetDspData.csproj",
]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def xml_root(path: Path) -> ET.Element:
    return ET.fromstring(read_text(path))


def project_references(path: Path) -> dict[str, ET.Element]:
    root = xml_root(path)
    return {
        item.attrib["Include"].replace("/", "\\"): item
        for item in root.findall(".//ProjectReference")
        if "Include" in item.attrib
    }


def property_value(path: Path, name: str) -> str:
    root = xml_root(path)
    node = root.find(f".//{name}")
    return "" if node is None or node.text is None else node.text.strip()


class AfterBuildProjectLayoutTests(unittest.TestCase):
    def test_afterbuild_references_all_local_packaged_projects(self):
        references = project_references(AFTERBUILD_CSPROJ)

        missing = []
        for project in LOCAL_LIBRARY_PROJECTS:
            expected = "..\\" + str(project).replace("/", "\\")
            if expected not in references:
                missing.append(expected)

        self.assertFalse(missing, "AfterBuildEvent 缺少 ProjectReference: " + ", ".join(missing))

        wrong_reference_mode = []
        for project in LOCAL_LIBRARY_PROJECTS:
            expected = "..\\" + str(project).replace("/", "\\")
            if references[expected].attrib.get("ReferenceOutputAssembly", "").lower() != "false":
                wrong_reference_mode.append(expected)

        self.assertFalse(
            wrong_reference_mode,
            "AfterBuildEvent 的 ProjectReference 应只表达构建依赖，需设置 ReferenceOutputAssembly=false: "
            + ", ".join(wrong_reference_mode),
        )

    def test_local_packaged_projects_are_libraries(self):
        wrong_output = []
        for project in LOCAL_LIBRARY_PROJECTS:
            output_type = property_value(project, "OutputType")
            if output_type.lower() != "library":
                wrong_output.append(f"{project}: {output_type or '<missing>'}")

        self.assertFalse(wrong_output, "本地打包项目必须显式 OutputType=Library: " + ", ".join(wrong_output))

    def test_afterbuild_does_not_use_legacy_win_output_path(self):
        legacy_patterns = [
            re.compile(r'["$@]*[^"\n]*bin\\win\\', re.IGNORECASE),
            re.compile(r'["$@]*[^"\n]*"bin"\s*,\s*"win"', re.IGNORECASE),
        ]
        offenders = []

        for path in sorted((ROOT / "AfterBuildEvent").rglob("*.cs")):
            text = read_text(path)
            for pattern in legacy_patterns:
                for match in pattern.finditer(text):
                    offenders.append(f"{path}: {match.group(0)}")

        self.assertFalse(offenders, "AfterBuildEvent 不能再使用 bin\\win 输出路径: " + "; ".join(offenders))

    def test_path_config_resolves_solution_from_app_base_directory(self):
        text = read_text(ROOT / "AfterBuildEvent" / "src" / "PathConfig.cs")

        self.assertIn("AppContext.BaseDirectory", text)
        self.assertIn("MLJ_DSPmods.sln", text)
        self.assertNotIn('public static string SolutionDir => @"..\\..\\..\\.."', text)


if __name__ == "__main__":
    unittest.main()
