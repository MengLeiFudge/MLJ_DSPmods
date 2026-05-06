using System;
using System.IO;
using System.Linq;
using static FE.Utils.Utils;

namespace FE.UI.View.Archive;

public static partial class DevelopmentDiary {
    private const string UnlockedFragmentsBlockTag = "UnlockedFragmentsV1";
    private const string SelectionBlockTag = "SelectionV1";

    #region IModCanSave

    public static void Import(BinaryReader r) {
        ResetState();
        r.ReadBlocks(
            (UnlockedFragmentsBlockTag, br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    string fragmentId = br.ReadString();
                    if (validFragmentIds.Contains(fragmentId)) {
                        unlockedFragmentIds.Add(fragmentId);
                    }
                }
            }),
            (SelectionBlockTag, br => {
                currentCategoryIndex = Math.Max(0, br.ReadInt32());
                currentFragmentIndex = Math.Max(0, br.ReadInt32());
            })
        );
        ClampSelection();
        SyncUnlockedFragmentsWithAchievements();
    }

    public static void Export(BinaryWriter w) {
        ClampSelection();
        w.WriteBlocks(
            (UnlockedFragmentsBlockTag, bw => {
                string[] orderedUnlockedIds = [
                    .. diaryFragments
                        .Where(IsUnlocked)
                        .Select(static fragment => fragment.Id)
                ];
                bw.Write(orderedUnlockedIds.Length);
                foreach (string fragmentId in orderedUnlockedIds) {
                    bw.Write(fragmentId);
                }
            }),
            (SelectionBlockTag, bw => {
                bw.Write(currentCategoryIndex);
                bw.Write(currentFragmentIndex);
            })
        );
    }

    public static void IntoOtherSave() {
        ResetState();
        SyncUnlockedFragmentsWithAchievements();
    }

    #endregion
}
