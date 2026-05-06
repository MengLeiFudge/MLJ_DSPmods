using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.UI.MainPanel.ProgressTask;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Archive;

public static partial class DevelopmentDiary {
    private static HashSet<string> unlockedFragmentIds = [];
    private static int currentCategoryIndex;
    private static int currentFragmentIndex;
    private static bool suppressSelectionCallbacks;

    private static void ResetState() {
        unlockedFragmentIds = [];
        currentCategoryIndex = 0;
        currentFragmentIndex = 0;
    }

    private static void ClampSelection() {
        if (diaryCategories.Length == 0) {
            currentCategoryIndex = 0;
            currentFragmentIndex = 0;
            return;
        }

        currentCategoryIndex = Mathf.Clamp(currentCategoryIndex, 0, diaryCategories.Length - 1);
        DiaryFragment[] fragments = diaryCategories[currentCategoryIndex].Fragments;
        currentFragmentIndex = fragments.Length == 0
            ? 0
            : Mathf.Clamp(currentFragmentIndex, 0, fragments.Length - 1);
    }

    private static DiaryFragment[] GetCurrentFragments() {
        ClampSelection();
        return diaryCategories[currentCategoryIndex].Fragments;
    }

    private static bool IsUnlocked(DiaryFragment fragment) {
        return !string.IsNullOrEmpty(fragment.Id) && unlockedFragmentIds.Contains(fragment.Id);
    }

    private static string GetFragmentDisplayLabel(DiaryFragment fragment) {
        return IsUnlocked(fragment) ? fragment.Label : "开发日记锁定标题".Translate();
    }

    private static string BuildLockedContent(string content) {
        if (string.IsNullOrEmpty(content)) {
            return string.Empty;
        }

        var builder = new StringBuilder(content.Length);
        bool inRichTag = false;

        foreach (char ch in content) {
            if (inRichTag) {
                if (ch == '>') {
                    inRichTag = false;
                }
                continue;
            }

            if (ch == '<') {
                inRichTag = true;
                continue;
            }

            if (ch is '\r' or '\n' or '\t' || char.IsWhiteSpace(ch)) {
                builder.Append(ch);
                continue;
            }

            builder.Append(ch <= 127 ? '?' : '？');
        }

        return builder.ToString();
    }

    private static bool TryUnlockRandomFragmentInternal() {
        List<int> availableCategoryIndices = [];
        for (int categoryIndex = 0; categoryIndex < diaryCategories.Length; categoryIndex++) {
            if (diaryCategories[categoryIndex].Fragments.Any(fragment => !IsUnlocked(fragment))) {
                availableCategoryIndices.Add(categoryIndex);
            }
        }

        if (availableCategoryIndices.Count == 0) {
            return false;
        }

        int selectedCategoryIndex = availableCategoryIndices[GetRandInt(0, availableCategoryIndices.Count)];
        foreach (DiaryFragment fragment in diaryCategories[selectedCategoryIndex].Fragments
                     .OrderBy(static item => item.Order)) {
            if (IsUnlocked(fragment)) {
                continue;
            }

            unlockedFragmentIds.Add(fragment.Id);
            return true;
        }

        return false;
    }

    private static void SyncUnlockedFragmentsWithAchievements() {
        int targetUnlockedCount = Math.Min(Achievements.GetClaimedAchievementCount(), diaryFragments.Length);
        while (unlockedFragmentIds.Count < targetUnlockedCount) {
            if (!TryUnlockRandomFragmentInternal()) {
                break;
            }
        }
    }

    public static bool TryUnlockRandomFragmentFromAchievement() {
        bool changed = TryUnlockRandomFragmentInternal();
        if (changed) {
            RefreshEntry();
        }
        return changed;
    }
}
