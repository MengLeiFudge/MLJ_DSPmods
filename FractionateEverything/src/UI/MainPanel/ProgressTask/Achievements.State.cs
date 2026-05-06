using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.MainPanel;
using FE.UI.MainPanel.Archive;
using FE.UI.MainPanel.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.GachaManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ProgressTask;

public static partial class Achievements {
    private static void SyncCurrentPageFromSharedState() {
        currentPage = Math.Max(0, MainWindow.SharedPanelState?.AchievementsCurrentPage ?? 0);
    }

    private static void SyncCurrentPageToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.AchievementsCurrentPage = currentPage;
        }
    }

    public static void LoadConfig(ConfigFile configFile) {
        achievementFlagsEntry = configFile.Bind(ConfigSection, ConfigAchievementFlags, string.Empty,
            "Achievement obtained flags. 1=obtained, 0=locked.");
        panelOpenCountEntry = configFile.Bind(ConfigSection, ConfigPanelOpenCount, 0,
            "How many times FE main panel has been opened.");

        ResetPersistentState();
        panelOpenCount = Math.Max(0, panelOpenCountEntry.Value);
        ApplyAchievementFlags(achievementFlagsEntry.Value);
        ResetTransientState();
        configLoaded = true;
        PersistAchievementConfig(forceSave: true);
    }

    public static void NotifyMainPanelOpened() {
        if (!configLoaded) {
            return;
        }

        panelOpenCount++;
        PersistAchievementConfig();
        CheckAndUnlockAchievements(showPopup: true);
    }

    public static void TickAutoUnlock() {
        if (!configLoaded) {
            return;
        }

        int frame = Time.frameCount;
        if (frame < nextAutoCheckFrame) {
            return;
        }
        nextAutoCheckFrame = frame + 60;
        CheckAndUnlockAchievements(showPopup: true);
    }

    public static void NotifyExternalConditionChanged() {
        if (!configLoaded) {
            return;
        }

        CheckAndUnlockAchievements(showPopup: true);
    }

    private static void ResetTransientState() {
        currentPage = 0;
        nextAutoCheckFrame = 0;
        SyncCurrentPageToSharedState();
    }

    private static void ResetPersistentState() {
        panelOpenCount = 0;
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);
        MarkBonusSummaryDirty();
    }

    private static void ApplyAchievementFlags(string flags) {
        if (string.IsNullOrEmpty(flags)) {
            return;
        }

        int count = Math.Min(flags.Length, achievements.Length);
        for (int i = 0; i < count; i++) {
            bool obtained = flags[i] == '1';
            unlocked[i] = obtained;
            claimed[i] = obtained;
        }
    }

    private static void ApplyClaimedFlags(bool[] flags) {
        int count = Math.Min(flags.Length, achievements.Length);
        for (int i = 0; i < count; i++) {
            if (!flags[i]) {
                continue;
            }

            unlocked[i] = true;
            claimed[i] = true;
        }
    }

    private static string BuildAchievementFlags() {
        char[] flags = new char[achievements.Length];
        for (int i = 0; i < achievements.Length; i++) {
            flags[i] = claimed[i] ? '1' : '0';
        }
        return new string(flags);
    }

    private static void PersistAchievementConfig(bool forceSave = false) {
        if (!configLoaded || achievementFlagsEntry == null || panelOpenCountEntry == null) {
            return;
        }

        string flags = BuildAchievementFlags();
        bool changed = forceSave
                       || achievementFlagsEntry.Value != flags
                       || panelOpenCountEntry.Value != panelOpenCount;
        if (!changed) {
            return;
        }

        achievementFlagsEntry.Value = flags;
        panelOpenCountEntry.Value = panelOpenCount;
        global::FE.FractionateEverything.SaveConfig();
    }

    private static bool CheckAndUnlockAchievements(bool showPopup) {
        bool changed = false;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                continue;
            }

            if (!IsConditionSatisfied(achievements[i].Condition)) {
                continue;
            }

            UnlockAchievement(i, showPopup);
            changed = true;
        }

        if (changed) {
            MarkBonusSummaryDirty();
            PersistAchievementConfig();
        }

        return changed;
    }

    private static bool IsConditionSatisfied(Func<bool> condition) {
        try {
            return condition();
        }
        catch (Exception ex) {
            LogWarning($"[Achievement] Condition check failed: {ex.Message}");
            return false;
        }
    }

    private static void UnlockAchievement(int index, bool showPopup) {
        unlocked[index] = true;
        claimed[index] = true;
        MarkBonusSummaryDirty();
        achievements[index].GrantReward?.Invoke();
        DevelopmentDiary.TryUnlockRandomFragmentFromAchievement();

        if (showPopup) {
            string message = string.Format("成就获得提示".Translate(), achievements[index].NameKey.Translate());
            UIRealtimeTip.Popup(message, true, 2);
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        ResetTransientState();
        if (!configLoaded) {
            return;
        }

        bool migrated = false;
        bool[] oldUnlocked = [];
        bool[] oldClaimed = [];
        bool[] saveClaimed = [];
        int importedPanelOpenCount = 0;
        if (r.BaseStream.Length <= 0) {
            return;
        }

        // Achievements 刻意保持全存档共享：
        // 这里不覆盖当前全局状态，只把旧档里存在的成就/计数并入当前 profile。
        r.ReadBlocks(
            ("PanelOpenCountV2", br => {
                importedPanelOpenCount = Math.Max(0, br.ReadInt32());
                if (importedPanelOpenCount > panelOpenCount) {
                    panelOpenCount = importedPanelOpenCount;
                    migrated = true;
                }
            }),
            ("ClaimedFlagsV2", br => { saveClaimed = ReadLegacyFlags(br); }),
            ("UnlockedFlags", br => { oldUnlocked = ReadLegacyFlags(br); }),
            ("ClaimedFlags", br => { oldClaimed = ReadLegacyFlags(br); })
        );

        int saveCount = Math.Min(saveClaimed.Length, achievements.Length);
        for (int i = 0; i < saveCount; i++) {
            if (!saveClaimed[i] || claimed[i]) {
                continue;
            }

            unlocked[i] = true;
            claimed[i] = true;
            migrated = true;
        }

        int oldCount = Math.Max(oldUnlocked.Length, oldClaimed.Length);
        for (int oldIndex = 0; oldIndex < oldCount && oldIndex < legacyAchievementNameOrder.Length; oldIndex++) {
            bool wasUnlocked = oldIndex < oldUnlocked.Length && oldUnlocked[oldIndex];
            bool wasClaimed = oldIndex < oldClaimed.Length && oldClaimed[oldIndex];
            if (!wasUnlocked && !wasClaimed) {
                continue;
            }

            string legacyName = legacyAchievementNameOrder[oldIndex];
            if (!achievementIndexByName.TryGetValue(legacyName, out int newIndex)) {
                continue;
            }

            if (claimed[newIndex]) {
                continue;
            }

            unlocked[newIndex] = true;
            claimed[newIndex] = true;
            migrated = true;
        }

        if (migrated) {
            MarkBonusSummaryDirty();
            PersistAchievementConfig();
        }
    }

    private static bool[] ReadLegacyFlags(BinaryReader br) {
        int count = br.ReadInt32();
        bool[] flags = new bool[count];
        for (int i = 0; i < count; i++) {
            flags[i] = br.ReadBoolean();
        }
        return flags;
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("PanelOpenCountV2", bw => bw.Write(panelOpenCount)),
            ("ClaimedFlagsV2", bw => {
                bw.Write(achievements.Length);
                for (int i = 0; i < achievements.Length; i++) {
                    bw.Write(claimed[i]);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        // 成就是全存档共享的 profile 状态，切档时只清理 UI/运行时缓存，不清 claimed/unlocked/panelOpenCount。
        ResetTransientState();
    }

    public static bool IsAchievementClaimed(string nameKey) {
        for (int i = 0; i < achievements.Length; i++) {
            if (achievements[i].NameKey == nameKey) {
                return claimed[i];
            }
        }
        return false;
    }

    public static int GetClaimedAchievementCount() {
        int count = 0;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                count++;
            }
        }
        return count;
    }

    #endregion
}
