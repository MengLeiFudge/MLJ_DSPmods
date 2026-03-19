using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.GetItemRecipe;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressSystem;

public static class RecurringTask {
    private static RectTransform window;
    private static RectTransform tab;

    private const int TaskCount = 5;

    private static readonly string[] taskNameKeys = ["分馏学徒", "分馏大师", "幸运抽奖", "符文强化", "配方收藏家"];
    private static readonly string[] taskCategoryKeys = ["生产", "生产", "抽卡", "升级", "收集"];
    private static readonly int[] targets = [500, 5000, 50, 10, 10];
    private static long[] baselines = new long[TaskCount];

    private static Text[] txtTaskNames = new Text[TaskCount];
    private static Text[] txtProgress = new Text[TaskCount];
    private static Text[] txtRewards = new Text[TaskCount];
    private static Text[] txtStatus = new Text[TaskCount];
    private static UIButton[] btnClaims = new UIButton[TaskCount];

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

        Register("生产", "Production");
        Register("抽卡", "Draw");
        Register("升级", "Upgrade");
        Register("收集", "Collection");

        Register("分馏学徒", "Fractionation Apprentice");
        Register("分馏大师", "Fractionation Master");
        Register("幸运抽奖", "Lucky Draw");
        Register("符文强化", "Rune Enhancement");
        Register("配方收藏家", "Recipe Collector");

        Register("分馏学徒描述", "Complete {0} successful fractionations", "完成{0}次成功分馏");
        Register("分馏大师描述", "Complete {0} successful fractionations", "完成{0}次成功分馏");
        Register("幸运抽奖描述", "Perform {0} raffle draws", "累计抽奖{0}次");
        Register("符文强化描述", "Upgrade runes {0} times", "累计符文强化{0}次");
        Register("配方收藏家描述", "Unlock {0} recipes", "解锁{0}个分馏配方");

        Register("循环任务奖励-最高奖券", "Highest unlocked ticket x{0}", "当前最高已解锁奖券 x{0}");
        Register("循环任务奖励-配方核心", "Fractionation recipe core x1", "分馏配方核心 x1");
        Register("循环任务奖励-随机精华", "Random essence x200", "随机精华 x200");
        Register("循环任务奖励-增幅芯片", "Fractionator amplification chip x2", "分馏塔增幅芯片 x2");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "循环任务");
        float x = 0f;
        float y = 18f + 7f;

        (float nameX, float nameW) = GetPosition(0, 4);
        (float progressX, float progressW) = GetPosition(1, 4);
        (float rewardX, float rewardW) = GetPosition(2, 4);
        (float actionX, float actionW) = GetPosition(3, 4);

        wnd.AddText2(nameX, y, tab, "任务", 16, "txtHeaderTask");
        wnd.AddText2(progressX, y, tab, "进度", 16, "txtHeaderProgress");
        wnd.AddText2(rewardX, y, tab, "奖励", 16, "txtHeaderReward");
        wnd.AddText2(actionX, y, tab, "操作", 16, "txtHeaderAction");

        y += 36f + 7f;
        for (int i = 0; i < TaskCount; i++) {
            int j = i;
            txtTaskNames[j] = wnd.AddText2(nameX + x, y, tab, "动态刷新", 15, $"txtTaskName{j}");
            txtTaskNames[j].supportRichText = true;

            txtProgress[j] = wnd.AddText2(progressX + x, y, tab, "动态刷新", 15, $"txtTaskProgress{j}");
            txtProgress[j].supportRichText = true;

            txtRewards[j] = wnd.AddText2(rewardX + x, y, tab, "动态刷新", 15, $"txtTaskReward{j}");
            txtRewards[j].supportRichText = true;

            txtStatus[j] = wnd.AddText2(actionX + x, y, tab, "动态刷新", 15, $"txtTaskStatus{j}");
            txtStatus[j].SetPosition(actionX + x - 85f, y);
            txtStatus[j].supportRichText = true;

            btnClaims[j] = wnd.AddButton(actionX + x, y, actionW, tab, "领取", 15, $"btnTaskClaim{j}",
                () => ClaimReward(j));
            y += 36f + 7f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        for (int i = 0; i < TaskCount; i++) {
            RefreshTaskRow(i);
        }
    }

    private static void RefreshTaskRow(int index) {
        long progress = GetProgress(index);
        bool completed = IsCompleted(index);
        bool claimedInThisCycle = !completed && baselines[index] > 0 && GetCurrentValue(index) == baselines[index];

        txtTaskNames[index].text = $"[{taskCategoryKeys[index].Translate()}] {taskNameKeys[index].Translate()}"
            .WithColor(completed ? Green : Orange);
        txtProgress[index].text = $"{progress}/{targets[index]}";
        txtProgress[index].color = completed ? Green : White;

        txtRewards[index].text = GetRewardText(index).WithColor(Blue);

        if (completed) {
            txtStatus[index].text = "已完成".Translate().WithColor(Green);
            btnClaims[index].button.interactable = true;
            btnClaims[index].SetText("领取");
            return;
        }

        txtStatus[index].text = claimedInThisCycle
            ? "已领取".Translate().WithColor(Green)
            : "进度".Translate().WithColor(Orange);
        btnClaims[index].button.interactable = false;
        btnClaims[index].SetText(claimedInThisCycle ? "已领取" : "领取");
    }

    private static void ClaimReward(int index) {
        if (!IsCompleted(index)) {
            return;
        }

        (int rewardItemId, int rewardCount) = GetRewardInfo(index);
        AddItemToModData(rewardItemId, rewardCount, 0, true);
        UIItemup.Up(rewardItemId, rewardCount);

        baselines[index] = GetCurrentValue(index);
        RefreshTaskRow(index);
    }

    private static (int, int) GetRewardInfo(int index) {
        return index switch {
            0 => (GetHighestTicketId(), 10),
            1 => (GetHighestTicketId(), 50),
            2 => (IFE分馏配方核心, 1),
            3 => (GetRandomEssenceId(), 200),
            4 => (IFE分馏塔增幅芯片, 2),
            _ => (IFE电磁奖券, 0)
        };
    }

    private static string GetRewardText(int index) {
        return index switch {
            0 => string.Format("循环任务奖励-最高奖券".Translate(), 10),
            1 => string.Format("循环任务奖励-最高奖券".Translate(), 50),
            2 => "循环任务奖励-配方核心".Translate(),
            3 => "循环任务奖励-随机精华".Translate(),
            4 => "循环任务奖励-增幅芯片".Translate(),
            _ => string.Empty
        };
    }

    private static int GetHighestTicketId() {
        int[] ticketIds = [IFE宇宙奖券, IFE引力奖券, IFE信息奖券, IFE结构奖券, IFE能量奖券, IFE电磁奖券];
        int[] matrixIds = [I宇宙矩阵, I引力矩阵, I信息矩阵, I结构矩阵, I能量矩阵, I电磁矩阵];

        if (GameMain.history == null) {
            return IFE电磁奖券;
        }

        for (int i = 0; i < ticketIds.Length; i++) {
            if (GameMain.history.ItemUnlocked(matrixIds[i])) {
                return ticketIds[i];
            }
        }

        return IFE电磁奖券;
    }

    private static int GetRandomEssenceId() {
        int[] essenceIds = [IFE速度精华, IFE产能精华, IFE节能精华, IFE增产精华];
        return essenceIds[GetRandInt(0, essenceIds.Length)];
    }

    private static long GetCurrentValue(int index) {
        return index switch {
            0 or 1 => totalFractionSuccesses,
            2 => TicketRaffle.totalDraws,
            3 => RuneManager.totalRuneUpgrades,
            4 => GetRecipesUnderMatrix(I宇宙矩阵).Sum(recipeList => recipeList.Count(r => r.Unlocked))
                 + GetRecipesByMatrix(I黑雾矩阵).Count(r => r.Unlocked),
            _ => 0
        };
    }

    private static long GetProgress(int index) {
        long progress = GetCurrentValue(index) - baselines[index];
        progress = Math.Max(progress, 0);
        return Math.Min(progress, targets[index]);
    }

    private static bool IsCompleted(int index) {
        return GetProgress(index) >= targets[index];
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
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Baselines", bw => {
                bw.Write(TaskCount);
                for (int i = 0; i < TaskCount; i++) {
                    bw.Write(baselines[i]);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(baselines, 0, baselines.Length);
    }

    #endregion
}
