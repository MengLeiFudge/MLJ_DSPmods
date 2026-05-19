import ast
import re
import unittest
from pathlib import Path


SOURCE_ROOT = Path("FractionateEverything/src")
MISCELLANEOUS_CANDIDATES = [
    Path("FractionateEverything/src/UI/MainPanel/Setting/Miscellaneous.cs"),
    Path("FractionateEverything/src/UI/View/Setting/Miscellaneous.cs"),
]

STRING_LITERAL_RE = r'"(?:\\.|[^"\\])*"'
REGISTER_RE = re.compile(
    rf"\b(?:Register|Edit|RegisterOrEditAsync|RegisterOrEditImmediately)\s*\(\s*({STRING_LITERAL_RE})"
)
TRANSLATE_LITERAL_RE = re.compile(rf"({STRING_LITERAL_RE})\s*\.Translate\s*\(")
CONFIG_BIND_RE = re.compile(r"configFile\.Bind\s*\((?P<args>.*?)\)\s*;", re.DOTALL)
CJK_RE = re.compile(r"[\u4e00-\u9fff]")

EXTERNAL_TRANSLATION_KEYS = {
    # They Come From Void registers these keys itself.
    "重置技能点确认标题",
    "重置技能点确认警告",
    # Vanilla DSP/CommonAPI UI keys used by FE when mirroring vanilla windows.
    "栏位已满",
    "次分馏每分",
    "正常运转",
    "电力不足",
    "停止运转",
    "待机",
    "产物堆积",
    "缺少原材料",
    "未供电",
    "其他",
}


def decode_string_literal(literal: str) -> str:
    try:
        return ast.literal_eval(literal)
    except (SyntaxError, ValueError):
        return literal[1:-1].replace(r"\"", '"').replace(r"\\", "\\")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def source_files():
    return sorted(SOURCE_ROOT.rglob("*.cs"))


def registered_keys() -> set[str]:
    keys = set()
    for path in source_files():
        for match in REGISTER_RE.finditer(read_text(path)):
            keys.add(decode_string_literal(match.group(1)))
    return keys


def requires_fe_translation_key(value: str) -> bool:
    return bool(value.strip()) and bool(CJK_RE.search(value)) and value not in EXTERNAL_TRANSLATION_KEYS


def existing_miscellaneous_file() -> Path:
    for path in MISCELLANEOUS_CANDIDATES:
        if path.exists():
            return path
    raise AssertionError("未找到 Miscellaneous 设置页文件")


class TranslationRegistrationTests(unittest.TestCase):
    def test_literal_translate_calls_have_registered_keys(self):
        keys = registered_keys()
        missing: dict[str, list[str]] = {}

        for path in source_files():
            text = read_text(path)
            for match in TRANSLATE_LITERAL_RE.finditer(text):
                value = decode_string_literal(match.group(1))
                if requires_fe_translation_key(value) and value not in keys:
                    missing.setdefault(value, []).append(str(path))

        self.assertFalse(
            missing,
            "存在未注册的字面量 Translate key: "
            + "; ".join(f"{key} @ {', '.join(paths[:3])}" for key, paths in sorted(missing.items())),
        )

    def test_miscellaneous_config_descriptions_have_registered_keys(self):
        keys = registered_keys()
        text = read_text(existing_miscellaneous_file())
        missing = []

        for match in CONFIG_BIND_RE.finditer(text):
            literals = [decode_string_literal(item) for item in re.findall(STRING_LITERAL_RE, match.group("args"))]
            if not literals:
                continue
            description = literals[-1]
            if requires_fe_translation_key(description) and description not in keys:
                missing.append(description)

        self.assertFalse(missing, "Miscellaneous 配置描述缺少 Register: " + ", ".join(sorted(set(missing))))

    def test_take_item_priority_options_are_registered_and_not_pretranslated(self):
        keys = registered_keys()
        text = read_text(existing_miscellaneous_file())
        match = re.search(r"TakeItemPriorityStrs\s*=\s*\[(?P<body>.*?)\];", text, re.DOTALL)
        self.assertIsNotNone(match, "未找到 TakeItemPriorityStrs")

        body = match.group("body")
        self.assertNotIn(".Translate()", body, "TakeItemPriorityStrs 不应在静态初始化阶段提前 Translate")

        options = [decode_string_literal(item) for item in re.findall(STRING_LITERAL_RE, body)]
        missing = [option for option in options if requires_fe_translation_key(option) and option not in keys]
        self.assertFalse(missing, "物品消耗顺序选项缺少 Register: " + ", ".join(missing))


if __name__ == "__main__":
    unittest.main()
