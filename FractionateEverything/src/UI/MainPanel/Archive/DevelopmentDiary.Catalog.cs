using System.Collections.Generic;
using System.Linq;

namespace FE.UI.MainPanel.Archive;

public static partial class DevelopmentDiary {
    private readonly struct DiaryFragment(string id, string label, string contentKey, int order) {
        public readonly string Id = id;
        public readonly string Label = label;
        public readonly string ContentKey = contentKey;
        public readonly int Order = order;
    }

    private readonly struct DiaryCategory(string labelKey, DiaryFragment[] fragments) {
        public readonly string LabelKey = labelKey;
        public readonly DiaryFragment[] Fragments = fragments;
    }

    private static readonly DiaryCategory[] diaryCategories = BuildDiaryCategories();
    private static readonly DiaryFragment[] diaryFragments =
        [.. diaryCategories.SelectMany(static category => category.Fragments)];
    private static readonly HashSet<string> validFragmentIds =
        [.. diaryFragments.Select(static fragment => fragment.Id)];

    private static DiaryCategory[] BuildDiaryCategories() {
        return [
            new DiaryCategory("日记分类-1x", BuildOneSeriesFragments()),
            new DiaryCategory("日记分类-2x", BuildTwoSeriesFragments()),
            new DiaryCategory("日记分类-IK", BuildIcarusFragments()),
        ];
    }

    private static DiaryFragment[] BuildOneSeriesFragments() {
        var fragments = new List<DiaryFragment>();
        int order = 0;

        AddSeriesFragments(fragments, "FE1.0", 9, "1.0", ref order);
        AddSeriesFragments(fragments, "FE1.1", 9, "1.1", ref order);
        fragments.Add(new DiaryFragment("FE1.4.1", "1.4.1", "141信息", ++order));
        fragments.Add(new DiaryFragment("FE1.4.2", "1.4.2", "142信息", ++order));
        fragments.Add(new DiaryFragment("FE1.4.3", "1.4.3", "143信息", ++order));

        return [.. fragments.OrderBy(static fragment => fragment.Order)];
    }

    private static DiaryFragment[] BuildTwoSeriesFragments() {
        var fragments = new List<DiaryFragment>();
        int order = 0;

        AddSeriesFragments(fragments, "FE2.0", 5, "2.0", ref order);
        AddSeriesFragments(fragments, "FE2.1", 4, "2.1", ref order);
        AddSeriesFragments(fragments, "FE2.2", 5, "2.2", ref order);
        AddSeriesFragments(fragments, "FE2.3", 5, "2.3", ref order);

        return [.. fragments.OrderBy(static fragment => fragment.Order)];
    }

    private static DiaryFragment[] BuildIcarusFragments() {
        var fragments = new List<DiaryFragment>();
        int order = 0;
        AddSeriesFragments(fragments, "IK", 20, "IK", ref order);
        return [.. fragments.OrderBy(static fragment => fragment.Order)];
    }

    private static void AddSeriesFragments(List<DiaryFragment> fragments, string prefix, int count, string labelPrefix,
        ref int order) {
        for (int index = 1; index <= count; index++) {
            string contentKey = $"{prefix}-{index}";
            fragments.Add(new DiaryFragment(contentKey, $"{labelPrefix}-{index}", contentKey, ++order));
        }
    }
}
