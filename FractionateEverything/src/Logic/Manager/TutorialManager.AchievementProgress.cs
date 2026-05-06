using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class TutorialManager {
    private static readonly HashSet<int> viewedToBottomTutorialIds = [];
    private static readonly Vector3[] viewportWorldCorners = new Vector3[4];
    private static readonly Vector3[] contentWorldCorners = new Vector3[4];

    private static void TryMarkCurrentTutorialViewedToBottom(UITutorialWindow tutorialWindow) {
        if (tutorialWindow?.tutorialProto == null) {
            return;
        }

        int tutorialId = tutorialWindow.tutorialProto.ID;
        if (!tutorialAchievementIds.Contains(tutorialId) || viewedToBottomTutorialIds.Contains(tutorialId)) {
            return;
        }

        if (!HasReachedTutorialBottom(tutorialWindow)) {
            return;
        }

        if (viewedToBottomTutorialIds.Add(tutorialId)) {
            Achievements.NotifyExternalConditionChanged();
        }
    }

    private static bool HasReachedTutorialBottom(UITutorialWindow tutorialWindow) {
        if (tutorialWindow.scrollViewRect == null || tutorialWindow.contentRect == null) {
            return false;
        }

        float viewportHeight = tutorialWindow.scrollViewRect.rect.height;
        float contentHeight = Mathf.Max(tutorialWindow.contentRect.rect.height, tutorialWindow.contentRect.sizeDelta.y);
        if (contentHeight <= viewportHeight + 1f) {
            return true;
        }

        // 这里通过右侧内容区与视口的世界坐标关系判断是否滚到底部，
        // 不依赖 prefab 是否显式暴露 ScrollRect 字段，后续拆分更多教程页时更稳。
        tutorialWindow.scrollViewRect.GetWorldCorners(viewportWorldCorners);
        tutorialWindow.contentRect.GetWorldCorners(contentWorldCorners);
        float viewportBottom = viewportWorldCorners[0].y;
        float contentBottom = contentWorldCorners[0].y;
        return contentBottom >= viewportBottom - 1f;
    }
}
