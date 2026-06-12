import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(".")
AFTERBUILD_CSPROJ = ROOT / "AfterBuildEvent" / "AfterBuildEvent.csproj"
AFTERBUILD_CS = ROOT / "AfterBuildEvent" / "src" / "AfterBuildEvent.cs"
AFTERBUILD_PUBLISHING_CS = ROOT / "AfterBuildEvent" / "src" / "Publishing" / "ModPublishing.cs"
ROOT_AGENTS = ROOT / "AGENTS.md"
AFTERBUILD_AGENTS = ROOT / "AfterBuildEvent" / "src" / "AGENTS.md"
LOCAL_LIBRARY_PROJECTS = [
    ROOT / "FractionateEverything" / "FractionateEverything.csproj",
    ROOT / "GetDspData" / "GetDspData.csproj",
    ROOT / "SaveDataExporter" / "SaveDataExporter.csproj",
    ROOT / "UXAEnhance" / "UXAEnhance.csproj",
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

    def test_packaging_deletes_only_current_version_zip(self):
        text = read_text(AFTERBUILD_PUBLISHING_CS)

        self.assertNotIn('Directory.GetFiles(@".\\ModZips")', text)
        self.assertNotIn('file.StartsWith($@".\\ModZips\\{projectName}")', text)
        self.assertIn("DeleteExistingVersionModZip(zipFile);", text)

        helper_match = re.search(
            r"private static void DeleteExistingVersionModZip\(string zipFile\) \{(?P<body>.*?)^    \}",
            text,
            re.DOTALL | re.MULTILINE,
        )
        self.assertIsNotNone(helper_match, "缺少同版本 zip 清理 helper")
        helper_body = helper_match.group("body")
        self.assertIn("File.Exists(zipFile)", helper_body)
        self.assertIn("File.Delete(zipFile)", helper_body)
        self.assertNotIn("Directory.GetFiles", helper_body)
        self.assertNotIn("projectName", helper_body)

    def test_root_agents_requires_afterbuild_automation_publish_for_packaged_changes(self):
        text = read_text(ROOT_AGENTS)

        forbidden_interactive_publish_rules = [
            "Manual/local interactive work",
            "without arguments",
            "do not auto-select any mode",
            "wt.exe -d",
        ]
        for phrase in forbidden_interactive_publish_rules:
            self.assertNotIn(phrase, text)

        required_publish_terms = [
            "always run `AfterBuildEvent.exe 1`",
            "latest commit body is the publish message source",
            "generic local `publish-local` admin API",
            "AfterBuildEvent` owns the publish target list",
            "manually starts `AfterBuildEvent.exe` and selects option `1`",
            "qqbot upload failure is not complete",
        ]
        for phrase in required_publish_terms:
            self.assertIn(phrase, text)

        removed_protocol_terms = [
            "AFTERBUILD_PUBLISH_SUMMARY",
            "ModZips/afterbuild-result.json",
            "non-empty publish summary",
        ]
        for phrase in removed_protocol_terms:
            self.assertNotIn(phrase, text)

    def test_afterbuild_agents_treats_qqbot_delivery_as_publish_completion(self):
        text = read_text(AFTERBUILD_AGENTS)

        required_terms = [
            "do not use the old no-argument interactive mode as publish completion",
            "after the worktree is accepted and merged back into the Windows-mounted target branch",
            "/admin/api/artifacts/publish-local",
            "do not claim the package was delivered",
            "manually chooses option `1`",
            "latest commit body is the publish message source",
            "PublishTargets",
            "deletes only bot-uploaded files with the exact same name",
        ]
        for phrase in required_terms:
            self.assertIn(phrase, text)

        removed_terms = [
            "afterbuild-result.json",
            "AFTERBUILD_PUBLISH_SUMMARY",
            "schema 2 package fingerprints",
            "60-second freshness window",
        ]
        for phrase in removed_terms:
            self.assertNotIn(phrase, text)

    def test_afterbuild_option_one_publishes_for_manual_and_automation_modes(self):
        text = read_text(AFTERBUILD_CS) + read_text(AFTERBUILD_PUBLISHING_CS)

        self.assertIn("List<GeneratedPackageInfo> generatedPackages = [];", text)
        self.assertIn("BuildGeneratedPackageInfo(projectName, zipFile, contentSha256)", text)
        self.assertIn("/admin/api/artifacts/publish-local", text)
        self.assertIn("PublishTargets", text)
        self.assertIn("BuildQqbotPublishFiles(generatedPackages)", text)
        self.assertIn("bool publishSucceeded = TryPublishGeneratedPackagesToQqbot", text)
        self.assertIn('["commit_detail"] = TryGetGitOutput("log -1 --pretty=%b")', text)
        self.assertIn("StandardOutputEncoding = Utf8NoBom", text)
        self.assertIn("StandardErrorEncoding = Utf8NoBom", text)
        self.assertNotIn("WriteAutomationResult", text)
        self.assertNotIn("afterbuild-result.json", text)
        self.assertNotIn("AFTERBUILD_PUBLISH_SUMMARY", text)
        self.assertIn('Console.WriteLine("手动模式：已上传生成的 zip 到 QQ 群");', text)
        self.assertIn("CalculateSha256(fullPath)", text)
        self.assertIn("CalculatePackageContentSha256(fileList)", text)
        self.assertIn('["content_sha256"] = package.ContentSha256', text)

    def test_afterbuild_option_one_can_filter_projects_by_argv(self):
        text = read_text(AFTERBUILD_PUBLISHING_CS)

        required_terms = [
            "HashSet<string> selectedProjects = ParseSelectedPublishProjects(args);",
            "selectedProjects.Count > 0 && !selectedProjects.Contains(projectName)",
            "ReportMissingSelectedProjects(selectedProjects, generatedPackages);",
            "arg.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)",
        ]
        for phrase in required_terms:
            self.assertIn(phrase, text)


if __name__ == "__main__":
    unittest.main()
