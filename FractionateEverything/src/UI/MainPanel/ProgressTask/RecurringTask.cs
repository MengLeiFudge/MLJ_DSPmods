using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.Controls;
using FE.UI.MainPanel.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using FE.Logic.DarkFog;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using FE.UI.Layout;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.ProgressTask;
/// <summary>
/// 循环任务进度和奖励展示页面。
/// </summary>
public static class RecurringTask {
    private static RectTransform window;
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtSummary;

    private const int TaskCount = 6;

    private static readonly string[] taskNameKeys = [
        "分馏总量",
        "开线推进",
        "原胚循环",
        "工艺精进",
        "配方收集",
        "黑雾压制",
    ];
    private static readonly string[] taskCategoryKeys = [
        "生产",
        "开线",
        "原胚",
        "工艺",
        "配方",
        "黑雾",
    ];
    private static readonly int[] targets = [2000, 30, 20, 5, 10, 12];
    private static long[] baselines = new long[TaskCount];
    private static long[] claimedCountsByTask = new long[TaskCount];
    private static long totalClaimedCount;
    private static bool autoClaimUnlocked;

    private static Text[] txtTaskNames = new Text[TaskCount];
    private static Text[] txtProgress = new Text[TaskCount];
    private static Text[] txtDescriptions = new Text[TaskCount];
    private static Text[] txtRewards = new Text[TaskCount];
    private static MyImageButton[] rewardIcons = new MyImageButton[TaskCount];
    private static Text[] txtStatus = new Text[TaskCount];
    private static UIButton[] btnClaims = new UIButton[TaskCount];

    private const float TaskNameX = 0f;
    private const float TaskNameW = 160f;
    private const float TaskProgressX = 170f;
    private const float TaskProgressW = 90f;
    private const float TaskDescX = 265f;
    private const float TaskDescW = 360f;
    private const float TaskRewardIconX = 635f;
    private const float TaskRewardTextX = 667f;
    private const float TaskRewardTextW = 95f;
    private const float TaskStatusX = 770f;
    private const float TaskStatusW = 85f;
    private const float TaskActionX = 865f;
    private const float TaskActionW = 170f;

    public static void AddTranslations() {
        Register("循环任务", "Recurring Task");
        Register("任务", "Task");
        Register("进度", "Progress");
        Register("奖励", "Reward");
        Register("状态", "Status");
        Register("操作", "Action");
        Register("领取", "Claim");
        Register("已完成", "Completed");
        Register("已领取", "Claimed");
        Register("进行中", "In Progress");

        Register("生产", "Production");
        Register("开线", "Opening");
        Register("原胚", "Proto");
        Register("工艺", "Craft");
        Register("配方", "Recipe");
        Register("黑雾", "Dark Fog", "黑雾");

        Register("分馏总量", "Fractionation Throughput");
        Register("开线推进", "Opening Line Push");
        Register("原胚循环", "Proto Cycle");
        Register("工艺精进", "Craft Refinement");
        Register("配方收集", "Recipe Collection");
        Register("黑雾压制", "Dark Fog Suppression", "黑雾压制");

        Register("分馏总量描述", "Reach {0} successful fractionations", "累计完成{0}次成功分馏");
        Register("开线推进描述", "Perform {0} opening-line draws", "累计完成{0}次开线抽取");
        Register("原胚循环描述", "Own {0} tower protos in storage", "仓储中持有{0}个分馏塔原胚");
        Register("工艺精进描述", "Fully upgrade {0} recipes", "累计满级{0}个分馏配方");
        Register("配方收集描述", "Unlock {0} fractionation recipes", "累计解锁{0}个分馏配方");
        Register("黑雾压制描述", "Accumulate {0} Dark Fog combat resources in storage", "当前黑雾战斗资源强度累计达到{0}");

        Register("循环任务奖励-残片", "Fragments x{0}", "残片 x{0}");
        Register("循环任务奖励-配方核心", "Fragments x{0}", "残片 x{0}");
        Register("循环任务奖励-矩阵", "Current stage matrix x{0}", "当前阶段矩阵 x{0}");
        Register("循环任务奖励-定向原胚", "Directional proto x{0}", "定向原胚 x{0}");
        Register("循环任务奖励-黑雾矩阵", "Dark Fog Matrix x{0}", "黑雾矩阵 x{0}");
        Register("循环任务自动领取已启用", "Recurring task auto-claim enabled", "循环任务自动领取已启用");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(72f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("循环任务", objectName: "recurring-task-header", pos: (0, 0), onBuilt: refs => header = refs),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "recurring-task-summary-card",
                        strong: true,
                        children: [
                            TextNode("动态刷新", 13, wrap: true, onBuilt: text => txtSummary = text,
                                pos: (0, 0), objectName: "txtRecurringTaskSummary"),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "recurring-task-list-card",
                        rows: BuildTaskListRows(),
                        cols: [Fr(160), Fr(90), Fr(360), Px(42f), Fr(95), Fr(85), Fr(170)],
                        rowGap: 6f,
                        columnGap: 8f,
                        children: BuildTaskListNodes()),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildTaskListRows() {
        var rows = new List<LayoutTrack> { Px(28f) };
        for (int i = 0; i < TaskCount; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildTaskListNodes() {
        var nodes = new List<LayoutNode> {
            TextNode("任务", 15, pos: (0, 0), objectName: "txtHeaderTask"),
            TextNode("进度", 15, pos: (0, 1), objectName: "txtHeaderProgress"),
            TextNode("描述", 15, pos: (0, 2), objectName: "txtHeaderDesc"),
            TextNode("奖励", 15, pos: (0, 3), span: (1, 2), objectName: "txtHeaderReward"),
            TextNode("状态", 15, pos: (0, 5), objectName: "txtHeaderStatus"),
            TextNode("操作", 15, pos: (0, 6), objectName: "txtHeaderAction"),
        };

        for (int i = 0; i < TaskCount; i++) {
            int row = i + 1;
            int index = i;
            nodes.Add(TextNode("动态刷新", 15, wrap: true,
                onBuilt: text => txtTaskNames[index] = text,
                pos: (row, 0), objectName: $"txtTaskName{index}"));
            nodes.Add(TextNode("动态刷新", 15,
                onBuilt: text => txtProgress[index] = text,
                pos: (row, 1), objectName: $"txtTaskProgress{index}"));
            nodes.Add(TextNode("动态刷新", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                onBuilt: text => txtDescriptions[index] = text,
                pos: (row, 2), objectName: $"txtTaskDesc{index}"));
            nodes.Add(ImageButtonNode(size: 40f, onBuilt: btn => rewardIcons[index] = btn,
                pos: (row, 3), objectName: $"txtTaskRewardIcon{index}"));
            nodes.Add(TextNode("动态刷新", 15,
                onBuilt: text => txtRewards[index] = text,
                pos: (row, 4), objectName: $"txtTaskReward{index}"));
            nodes.Add(TextNode("动态刷新", 15,
                onBuilt: text => txtStatus[index] = text,
                pos: (row, 5), objectName: $"txtTaskStatus{index}"));
            nodes.Add(ButtonNode("领取", onClick: () => ClaimReward(index),
                onBuilt: btn => btnClaims[index] = btn,
                pos: (row, 6), objectName: $"btnTaskClaim{index}"));
        }

        return nodes;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "循环任务".Translate().WithColor(Orange);
        TryAutoClaimCompletedTasks();
        int completedCount = 0;
        for (int i = 0; i < TaskCount; i++) {
            if (IsCompleted(i)) {
                completedCount++;
            }
            RefreshTaskRow(i);
        }
        txtSummary.text = $"已完成 {completedCount}/{TaskCount}    已累计领取 {totalClaimedCount} 次循环奖励"
            .WithColor(completedCount >= TaskCount ? Green : Orange);
    }

    private static void RefreshTaskRow(int index) {
        long progress = GetProgress(index);
        bool completed = IsCompleted(index);
        bool claimedInThisCycle = !completed && baselines[index] > 0 && GetCurrentValue(index) == baselines[index];

        txtTaskNames[index].text = $"[{taskCategoryKeys[index].Translate()}] {taskNameKeys[index].Translate()}"
            .WithColor(completed ? Green : Orange);
        txtProgress[index].text = $"{progress}/{targets[index]}";
        txtProgress[index].color = completed ? Green : White;
        txtDescriptions[index].text = GetTaskDesc(index);
        (int rewardItemId, int rewardCount) = GetRewardInfo(index);
        rewardIcons[index].Proto = rewardItemId > 0 ? LDB.items.Select(rewardItemId) : null;
        rewardIcons[index].gameObject.SetActive(rewardItemId > 0);
        if (rewardItemId > 0) {
            rewardIcons[index].SetCount(rewardCount);
            txtRewards[index].text = "";
        } else {
            rewardIcons[index].ClearCountText();
            txtRewards[index].text = $"x{rewardCount}".WithColor(Blue);
        }

        if (completed) {
            txtStatus[index].text = "已完成".Translate().WithColor(Green);
            btnClaims[index].button.interactable = true;
            btnClaims[index].SetText("领取");
            return;
        }

        string progressStatus = $"{progress}/{targets[index]}";
        txtStatus[index].text = claimedInThisCycle
            ? "已领取".Translate().WithColor(Green)
            : progressStatus.WithColor(Orange);
        btnClaims[index].button.interactable = false;
        btnClaims[index].SetText(claimedInThisCycle ? "已领取" : "领取");
    }

    private static bool IsUiReady() {
        return txtTaskNames[0] != null
               && txtProgress[0] != null
               && txtDescriptions[0] != null
               && txtRewards[0] != null
               && rewardIcons[0] != null
               && txtStatus[0] != null
               && btnClaims[0] != null;
    }

    private static void ClaimReward(int index) {
        ClaimReward(index, refreshUi: true);
    }

    private static void ClaimReward(int index, bool refreshUi) {
        if (!IsCompleted(index)) {
            return;
        }

        (int rewardItemId, int rewardCount) = GetRewardInfo(index);
        AddItemToModData(rewardItemId, rewardCount, 0, true);
        UIItemup.Up(rewardItemId, rewardCount);

        baselines[index] = GetCurrentValue(index);
        claimedCountsByTask[index]++;
        totalClaimedCount++;
        if (refreshUi && IsUiReady()) {
            RefreshTaskRow(index);
        }
    }

    private static void TryAutoClaimCompletedTasks() {
        if (!autoClaimUnlocked) {
            return;
        }

        for (int i = 0; i < TaskCount; i++) {
            if (IsCompleted(i)) {
                ClaimReward(i, refreshUi: false);
            }
        }
    }

    private static (int, int) GetRewardInfo(int index) {
        return index switch {
            0 => (IFE残片, 500),
            1 => (GetCurrentStageMatrixId(), 4),
            2 => (IFE分馏塔定向原胚, 1),
            3 => (GetCurrentStageMatrixId(), 4),
            4 => (IFE残片, 500),
            5 => (I黑雾矩阵, 2),
            _ => (IFE残片, 0)
        };
    }

    private static string GetRewardText(int index) {
        return index switch {
            0 => string.Format("循环任务奖励-残片".Translate(), 500),
            1 => string.Format("循环任务奖励-矩阵".Translate(), 4),
            2 => string.Format("循环任务奖励-定向原胚".Translate(), 1),
            3 => string.Format("循环任务奖励-矩阵".Translate(), 4),
            4 => string.Format("循环任务奖励-配方核心".Translate(), 500),
            5 => string.Format("循环任务奖励-黑雾矩阵".Translate(), 2),
            _ => string.Empty
        };
    }

    private static string GetTaskDesc(int index) {
        return index switch {
            0 => string.Format("分馏总量描述".Translate(), targets[index]),
            1 => string.Format("开线推进描述".Translate(), targets[index]),
            2 => string.Format("原胚循环描述".Translate(), targets[index]),
            3 => string.Format("工艺精进描述".Translate(), targets[index]),
            4 => string.Format("配方收集描述".Translate(), targets[index]),
            5 => string.Format("黑雾压制描述".Translate(), targets[index]),
            _ => string.Empty,
        };
    }

    private static int GetCurrentStageMatrixId() {
        return GameMain.history != null && GameMain.history.TechUnlocked(T宇宙矩阵) ? I宇宙矩阵 :
            GameMain.history != null && GameMain.history.TechUnlocked(T引力矩阵) ? I引力矩阵 :
            GameMain.history != null && GameMain.history.TechUnlocked(T信息矩阵) ? I信息矩阵 :
            GameMain.history != null && GameMain.history.TechUnlocked(T结构矩阵) ? I结构矩阵 :
            GameMain.history != null && GameMain.history.TechUnlocked(T能量矩阵) ? I能量矩阵 : I电磁矩阵;
    }

    private static long GetCurrentValue(int index) {
        return index switch {
            0 => totalFractionSuccesses,
            1 => TicketRaffle.openingLineDraws,
            2 => GetProtoInventoryCount(),
            3 => GetFullyUpgradedRecipeCount(),
            4 => GetUnlockedRecipeCount(),
            5 => DarkFogCombatManager.GetCurrentDarkFogInventoryScore(),
            _ => 0
        };
    }

    private static long GetProtoInventoryCount() {
        return GetItemTotalCount(IFE交互塔原胚)
               + GetItemTotalCount(IFE矿物复制塔原胚)
               + GetItemTotalCount(IFE点数聚集塔原胚)
               + GetItemTotalCount(IFE转化塔原胚)
               + GetItemTotalCount(IFE精馏塔原胚)
               + GetItemTotalCount(IFE分馏塔定向原胚);
    }

    private static long GetFullyUpgradedRecipeCount() {
        return RecipeGrowthQueries.GetMaxedCount(ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.Conversion);
    }

    private static long GetUnlockedRecipeCount() {
        return RecipeGrowthQueries.GetUnlockedCount(ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.Conversion);
    }

    private static long GetProgress(int index) {
        long progress = GetCurrentValue(index) - baselines[index];
        progress = Math.Max(progress, 0);
        return Math.Min(progress, targets[index]);
    }

    private static bool IsCompleted(int index) {
        return GetProgress(index) >= targets[index];
    }

    public static long TotalClaimedCount => totalClaimedCount;
    public static int ClaimedTaskTypeCount {
        get {
            int count = 0;
            for (int i = 0; i < Math.Min(TaskCount, claimedCountsByTask.Length); i++) {
                if (claimedCountsByTask[i] > 0) {
                    count++;
                }
            }
            return count;
        }
    }
    public static bool HasClaimedAllTaskTypes => ClaimedTaskTypeCount >= TaskCount;
    public static bool AutoClaimUnlocked => autoClaimUnlocked;

    public static void UnlockAutoClaim() {
        if (autoClaimUnlocked) {
            return;
        }
        autoClaimUnlocked = true;
        UIRealtimeTip.Popup("循环任务自动领取已启用".Translate(), true, 2);
    }

    public static void TickAutoClaim() {
        if (!autoClaimUnlocked && Achievements.IsAchievementClaimed("成就-任务自动化")) {
            autoClaimUnlocked = true;
        }

        TryAutoClaimCompletedTasks();
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Baselines", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, TaskCount); i++) {
                    baselines[i] = br.ReadInt64();
                }
                for (int i = TaskCount; i < count; i++) {
                    br.ReadInt64();
                }
            }),
            ("ClaimedCountsByTask", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, TaskCount); i++) {
                    claimedCountsByTask[i] = Math.Max(0L, br.ReadInt64());
                }
                for (int i = TaskCount; i < count; i++) {
                    br.ReadInt64();
                }
            }),
            ("TotalClaimedCount", br => totalClaimedCount = br.ReadInt64()),
            ("AutoClaimUnlocked", br => autoClaimUnlocked = br.ReadBoolean())
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Baselines", bw => {
                bw.Write(TaskCount);
                for (int i = 0; i < TaskCount; i++) {
                    bw.Write(baselines[i]);
                }
            }),
            ("ClaimedCountsByTask", bw => {
                bw.Write(TaskCount);
                for (int i = 0; i < TaskCount; i++) {
                    bw.Write(claimedCountsByTask[i]);
                }
            }),
            ("TotalClaimedCount", bw => bw.Write(totalClaimedCount)),
            ("AutoClaimUnlocked", bw => bw.Write(autoClaimUnlocked))
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(baselines, 0, baselines.Length);
        Array.Clear(claimedCountsByTask, 0, claimedCountsByTask.Length);
        totalClaimedCount = 0;
        autoClaimUnlocked = false;
    }

    #endregion
}
