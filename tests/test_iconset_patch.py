import re
import unittest
from pathlib import Path


ICON_SET_PATCH = Path("FractionateEverything/src/Logic/Items/Presentation/IconSetPatch.cs")


def read_icon_set_patch() -> str:
    return ICON_SET_PATCH.read_text(encoding="utf-8-sig")


def tech_loop_body() -> str:
    text = read_icon_set_patch()
    match = re.search(
        r"TechProto\[\] dataArray4 = LDB\.techs\.dataArray;(?P<body>.*?)"
        r"__instance\.techIconIndexBuffer\.SetData\(__instance\.techIconIndex\);",
        text,
        re.DOTALL,
    )
    if not match:
        raise AssertionError("未找到 IconSetPatch 科技图标循环")
    return match.group("body")


class IconSetPatchTests(unittest.TestCase):
    def test_upgrade_techs_reuse_icon_without_skipping_mapping_registration(self):
        body = tech_loop_body()

        self.assertIn("TextIconMapping.IconConfig", body)
        self.assertIn("num17 = lastTechIconIndex;", body)
        self.assertNotIn("continue;", body, "科技图标循环不能在注册 IconTag 映射前跳过低级升级科技")


if __name__ == "__main__":
    unittest.main()
