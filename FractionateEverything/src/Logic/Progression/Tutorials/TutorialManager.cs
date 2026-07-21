using static FE.Utils.Utils;
using xiaoye97;

namespace FE.Logic.Progression;

/// <summary>
/// 注册 FE 教程及其解锁条件；教程阅读不再承载旧成就状态。
/// </summary>
public static partial class TutorialManager {
    private readonly struct TutorialRegistration(
        int id,
        string baseName,
        string determinatorName,
        long[] determinatorParams) {
        public readonly int Id = id;
        public readonly string BaseName = baseName;
        public readonly string DeterminatorName = determinatorName;
        public readonly long[] DeterminatorParams = determinatorParams;
    }

    private const int FirstTutorialId = 201;
    private const string FeTutorialLayoutPrefix = "tutorial-fe-";
    private static readonly TutorialRegistration[] tutorialRegistrations = BuildTutorialRegistrations();

    public static void AddTutorials() {
        foreach (TutorialRegistration registration in tutorialRegistrations) {
            AddTutorial(registration);
        }
    }

    private static TutorialRegistration[] BuildTutorialRegistrations() {
        int nextId = FirstTutorialId;
        return [
            new(nextId++, "万物分馏简介", "TOR_GameSecond", [10]),
            new(nextId++, "分馏数据中心", "TOR_TechUnlocked", [TFE分馏数据中心, 4]),
            new(nextId++, "分馏塔使用指南", "TOR_OnBuild",
                [IFE交互塔, IFE资源塔, IFE转化塔, IFE解析塔]),
            new(nextId++, "物流交互站使用指南", "TOR_OnBuild",
                [IFE行星内物流交互站, IFE星际物流交互站]),
        ];
    }

    private static void AddTutorial(TutorialRegistration registration) {
        TutorialProto proto = new() {
            ID = registration.Id,
            SID = "",
            Name = $"{registration.BaseName}标题",
            name = $"{registration.BaseName}标题",
            LayoutFileName = $"{FeTutorialLayoutPrefix}{registration.Id}",
            DeterminatorName = registration.DeterminatorName,
            DeterminatorParams = registration.DeterminatorParams,
        };
        LDBTool.PreAddProto(proto);
        proto.Preload();
    }
}
