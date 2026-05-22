from pathlib import Path
import unittest


SOURCE_ROOT = Path("FractionateEverything/src")
MAIN_WINDOW = SOURCE_ROOT / "UI/MainPanel/MainWindow.cs"
PAGE_REGISTRY = SOURCE_ROOT / "UI/MainPanel/MainWindowPageRegistry.cs"
MAIN_TASK = SOURCE_ROOT / "UI/MainPanel/ProgressTask/MainTask.cs"
ACHIEVEMENTS = SOURCE_ROOT / "UI/MainPanel/ProgressTask/Achievements.cs"
TUTORIAL_TEXTS = SOURCE_ROOT / "Logic/Progression/Tutorials/TutorialTexts.cs"
CHANGELOG = Path("FractionateEverything/CHANGELOG.md")
UI_AGENTS = SOURCE_ROOT / "UI/MainPanel/AGENTS.md"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


class RecurringTaskRemovalTests(unittest.TestCase):
    def test_recurring_task_page_class_and_runtime_hooks_are_removed(self):
        self.assertFalse((SOURCE_ROOT / "UI/MainPanel/ProgressTask/RecurringTask.cs").exists())

        main_window = read(MAIN_WINDOW)
        page_registry = read(PAGE_REGISTRY)

        self.assertNotIn("RecurringTask.", main_window)
        self.assertNotIn('"RecurringTask"', main_window)
        self.assertNotIn('"循环任务"', page_registry)
        self.assertNotIn("RecurringTask.", page_registry)

    def test_main_task_removes_recurring_branch_but_keeps_state_matrix_migration(self):
        text = read(MAIN_TASK)

        self.assertNotIn('Branch("recurring-entry"', text)
        self.assertNotIn("RecurringTask.", text)
        self.assertNotIn("主线统计-循环任务", text)
        self.assertNotIn("循环类型", text)
        self.assertIn("private static int MapSavedBranchIndex", text)
        self.assertIn("savedBranchCount <= route.Branches.Length", text)
        self.assertIn("return oldBranchIndex == 8;", text)

    def test_achievements_remove_recurring_conditions_and_reward_hooks(self):
        text = read(ACHIEVEMENTS)

        self.assertNotIn("AddRecurringAchievements(list)", text)
        self.assertNotIn("private static void AddRecurringAchievements", text)
        self.assertNotIn("RecurringTask.", text)
        self.assertNotIn('Register("成就分类-循环"', text)
        self.assertNotIn('Register("成就奖励-循环任务自动领取"', text)
        self.assertIn("recurringTaskAchievementNameOrder", text)

    def test_player_facing_docs_no_longer_describe_recurring_tasks_as_active_system(self):
        self.assertNotIn("循环任务：稳定补给", read(TUTORIAL_TEXTS))
        self.assertIn("移除循环任务", read(CHANGELOG))
        self.assertIn("| `ProgressTask/` | 2 | 主线任务、成就系统；页面内部模型跟随页面文件 |", read(UI_AGENTS))


if __name__ == "__main__":
    unittest.main()
