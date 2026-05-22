from pathlib import Path
import unittest


SOURCE_ROOT = Path("FractionateEverything/src")
MAIN_WINDOW = SOURCE_ROOT / "UI/View/MainWindow.cs"
CHANGELOG = Path("FractionateEverything/CHANGELOG.md")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


class RecurringTaskRemovalTests(unittest.TestCase):
    def test_recurring_task_page_class_and_runtime_hooks_are_removed(self):
        self.assertFalse((SOURCE_ROOT / "UI/View/ProgressSystem/RecurringTask.cs").exists())

        main_window = read(MAIN_WINDOW)

        self.assertNotIn("RecurringTask.", main_window)
        self.assertNotIn('"RecurringTask"', main_window)
        self.assertIn("if (version <= 2) {\n            r.ReadInt32();\n        }", main_window)
        self.assertIn("w.Write(3);", main_window)

    def test_player_facing_changelog_notes_recurring_task_removal(self):
        changelog = read(CHANGELOG)

        self.assertIn("移除循环任务入口", changelog)
        self.assertIn("Removed the recurring task entry", changelog)


if __name__ == "__main__":
    unittest.main()
