using UnityEngine;

namespace FE.UI.MainPanel.Shell.Analysis;

/// <summary>
/// Analysis 主面板内容区骨架与尺寸布局逻辑。
/// </summary>
public partial class AnalysisMainPanelWindow {
    private void CreateContainerSkeleton() {
        RectTransform host = contentPanelHost;
        if (host == null) {
            host = transform.Find("panel-bg") as RectTransform;
        }
        if (host == null) return;

        contentRootContainer = CreateContainerRect(
            "analysis-content-root",
            host,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f));

        // 统一中间黑色区标准尺寸，后续页面都基于这个区域计算布局。
        contentRootContainer.anchorMin = new Vector2(0f, 1f);
        contentRootContainer.anchorMax = new Vector2(0f, 1f);
        contentRootContainer.pivot = new Vector2(0f, 1f);
        contentRootContainer.sizeDelta = new Vector2(AnalysisBlackAreaWidth, AnalysisBlackAreaHeight);
        contentRootContainer.anchoredPosition = Vector2.zero;

        contentRootContainer.SetAsLastSibling();
    }

    private static RectTransform GetOrCreateDesignRoot(RectTransform pageRoot) {
        if (pageRoot == null) {
            return null;
        }

        Transform existing = pageRoot.Find("analysis-design-root");
        if (existing is RectTransform existingRect) {
            return existingRect;
        }

        return CreateContainerRect(
            "analysis-design-root",
            pageRoot,
            new Vector2(AnalysisContentGap, AnalysisContentGap),
            new Vector2(-AnalysisContentGap, -AnalysisContentGap),
            Vector2.zero,
            Vector2.one);
    }

    private void ApplyStatWindowSize() {
        if (windowTrans == null) return;

        analysisWindowSize = TryGetStatWindowSize(out Vector2 statWindowSize)
            ? statWindowSize
            : new(WindowWidth, WindowHeight);
        windowTrans.sizeDelta = analysisWindowSize;
    }

    private static bool TryGetStatWindowSize(out Vector2 size) {
        size = default;
        UIGame uiGame = UIRoot.instance?.uiGame;
        if (uiGame?.statWindow == null) {
            return false;
        }

        // analysis 窗口尺寸对齐原生 P 面板，来源是 UIRoot.instance.uiGame.statWindow 的根 RectTransform。
        RectTransform statWindowRect = uiGame.statWindow.GetComponent<RectTransform>();
        if (statWindowRect == null) {
            return false;
        }

        size = statWindowRect.sizeDelta;
        return size.x > 0f && size.y > 0f;
    }

    private static RectTransform CreateContainerRect(string objectName, RectTransform parent, Vector2 offsetMin,
        Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax) {
        var obj = new GameObject(objectName, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
}
