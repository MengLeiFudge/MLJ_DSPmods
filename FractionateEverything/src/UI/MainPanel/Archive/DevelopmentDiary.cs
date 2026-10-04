using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Archive;

/// <summary>历史开发手记与伊卡洛斯手记的卡片数据；历史手记始终可读。</summary>
public static class DevelopmentDiary {
    /// <summary>历史手记单篇条目的标题与正文键。</summary>
    private readonly struct DiaryFragment(string label, string contentKey) {
        public readonly string Label = label;
        public readonly string ContentKey = contentKey;
    }

    private static readonly DiaryFragment[][] HistoryCategories = [BuildOneSeriesFragments(), BuildTwoSeriesFragments()];

    /// <summary>读取独立手记页的卡片；伊卡洛斯记录随主线展开。</summary>
    /// <param name="category">0为1.x，1为2.x，2为伊卡洛斯。</param>
    /// <returns>只读展示卡片。</returns>
    public static IReadOnlyList<PageCard> GetCards(int category) {
        if (category == 2) {
            return Enumerable.Range(1, 18).Select(number => {
                bool unlocked = number <= ExplorationManager.StoryCompleted;
                return new PageCard { Key = number.ToString(), ItemId = IFE记忆源点,
                    Title = unlocked ? ExplorationManager.GetStoryTitle(number) : "？？？",
                    Status = unlocked ? "已记录".Translate() : "尚未发现".Translate(),
                    Detail = unlocked ? ExplorationManager.GetStoryNarrative(number) : "这段记录还没有被找到。".Translate() };
            }).ToArray();
        }
        return HistoryCategories[Math.Max(0, Math.Min(1, category))].Select(fragment => new PageCard {
            Key = fragment.ContentKey, ItemId = IFE残片, Title = fragment.Label, Status = "历史手记".Translate(),
            Detail = fragment.ContentKey.Translate(),
        }).ToArray();
    }

    private static DiaryFragment[] BuildOneSeriesFragments() {
        var fragments = new List<DiaryFragment>();
        AddSeriesFragments(fragments, "FE1.0", 9, "1.0");
        AddSeriesFragments(fragments, "FE1.1", 9, "1.1");
        fragments.Add(new DiaryFragment("1.4.1", "141信息"));
        fragments.Add(new DiaryFragment("1.4.2", "142信息"));
        fragments.Add(new DiaryFragment("1.4.3", "143信息"));
        return [.. fragments];
    }

    private static DiaryFragment[] BuildTwoSeriesFragments() {
        var fragments = new List<DiaryFragment>();
        AddSeriesFragments(fragments, "FE2.0", 5, "2.0");
        AddSeriesFragments(fragments, "FE2.1", 4, "2.1");
        AddSeriesFragments(fragments, "FE2.2", 5, "2.2");
        AddSeriesFragments(fragments, "FE2.3", 5, "2.3");
        return [.. fragments];
    }

    private static void AddSeriesFragments(List<DiaryFragment> fragments, string prefix, int count, string labelPrefix) {
        for (int index = 1; index <= count; index++) {
            fragments.Add(new DiaryFragment($"{labelPrefix}-{index}", $"{prefix}-{index}"));
        }
    }

}
