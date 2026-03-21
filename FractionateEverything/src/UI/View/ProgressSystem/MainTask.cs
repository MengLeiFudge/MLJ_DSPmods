using System;
using System.Collections.Generic;
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

public static class MainTask {
    private static RectTransform window;
    private static RectTransform tab;

    private readonly struct TaskInfo(
        string nameKey,
        string descKey,
        string rewardKey,
        Func<bool> isCompleted,
        Func<string> progressText,
        Action grantReward,
        bool autoClaim = false) {
        public readonly string NameKey = nameKey;
        public readonly string DescKey = descKey;
        public readonly string RewardKey = rewardKey;
        public readonly Func<bool> IsCompleted = isCompleted;
        public readonly Func<string> ProgressText = progressText;
        public readonly Action GrantReward = grantReward;
        public readonly bool AutoClaim = autoClaim;
    }

    private static int currentStage = 0;
    private static bool rewardClaimed = true;

    private static Text txtTaskName;
    private static Text txtTaskDesc;
    private static Text txtTaskStatus;
    private static Text txtTaskCondition;
    private static Text txtTaskReward;
    private static Text txtTotalProgress;
    private static UIButton btnClaim;

    private static readonly TaskInfo[] Tasks = [
        new("分馏启示", "主线描述-分馏启示", "主线奖励-无", () => IsTechUnlocked(TFE分馏数据中心),
            () => GetTechProgressText(TFE分馏数据中心), () => { }, true),
        new("万物之始", "主线描述-万物之始", "主线奖励-残片500", () => totalFractionSuccesses >= 50,
            () => string.Format("分馏次数进度".Translate(), totalFractionSuccesses, 50),
            () => GrantItems((IFE残片, 500))),
        new("抽卡之乐", "主线描述-抽卡之乐", "主线奖励-配方核心1", () => TicketRaffle.totalDraws >= 10,
            () => string.Format("抽奖次数进度".Translate(), TicketRaffle.totalDraws, 10),
            () => GrantItems((IFE分馏配方核心, 1))),
        new("矿物新生", "主线描述-矿物新生", "主线奖励-矿物复制塔原胚10", () => IsTechUnlocked(TFE矿物复制),
            () => GetTechProgressText(TFE矿物复制), () => GrantItems((IFE矿物复制塔原胚, 10))),
        new("物品转化", "主线描述-物品转化", "主线奖励-转化塔原胚10", () => IsTechUnlocked(TFE物品转化),
            () => GetTechProgressText(TFE物品转化), () => GrantItems((IFE转化塔原胚, 10))),
        new("符文初识", "主线描述-符文初识", "主线奖励-残片1000", HasAnyEquippedRune,
            GetRuneProgressText, () => GrantItems((IFE残片, 1000))),
        new("产线优化", "主线描述-产线优化", "主线奖励-增幅芯片5", HasBuildingLevel6,
            GetBuildingProgressText, () => GrantItems((IFE分馏塔增幅芯片, 5))),
        new("配方精通", "主线描述-配方精通", "主线奖励-配方核心3", () => GetUnlockedRecipeCount() >= 30,
            () => string.Format("解锁配方进度".Translate(), GetUnlockedRecipeCount(), 30),
            () => GrantItems((IFE分馏配方核心, 3))),
        new("星际互联", "主线描述-星际互联", "主线奖励-星际物流交互站2", () => IsTechUnlocked(TFE星际物流交互),
            () => GetTechProgressText(TFE星际物流交互), () => GrantItems((IFE星际物流交互站, 2))),
        new("万物归一", "主线描述-万物归一", "主线奖励-残片2000", () => GetUnlockedRecipeCount() >= 100,
            () => string.Format("解锁配方进度".Translate(), GetUnlockedRecipeCount(), 100),
            () => GrantItems((IFE残片, 2000))),
    ];

    public static void AddTranslations() {
        Register("主线任务", "Main Task");

        Register("分馏启示", "Fractionation Revelation");
        Register("万物之始", "Start of All Things");
        Register("抽卡之乐", "Joy of Draws");
        Register("矿物新生", "Mineral Rebirth");
        Register("物品转化", "Item Conversion");
        Register("符文初识", "First Rune");
        Register("产线优化", "Production Optimization");
        Register("配方精通", "Recipe Mastery");
        Register("星际互联", "Interstellar Connectivity");
        Register("万物归一", "All Into One");

        Register("主线描述-分馏启示", "Unlock Fractionation Data Centre tech", "解锁分馏数据中心科技");
        Register("主线描述-万物之始", "Reach 50 successful fractionations", "累计完成 50 次分馏成功");
        Register("主线描述-抽卡之乐", "Complete 10 ticket draws", "累计完成 10 次奖券抽奖");
        Register("主线描述-矿物新生", "Unlock Mineral Replication tech", "解锁矿物复制科技");
        Register("主线描述-物品转化", "Unlock Item Conversion tech", "解锁物品转化科技");
        Register("主线描述-符文初识", "Equip any rune", "任意槽位装备一个符文");
        Register("主线描述-产线优化", "Upgrade any FE building to level 6", "任意万物分馏建筑等级达到 6");
        Register("主线描述-配方精通", "Unlock 30 fractionation recipes", "累计解锁 30 个分馏配方");
        Register("主线描述-星际互联", "Unlock Interstellar Interaction tech", "解锁星际物流交互科技");
        Register("主线描述-万物归一", "Unlock 100 fractionation recipes", "累计解锁 100 个分馏配方");

        Register("主线奖励-无", "No extra reward (auto completed)", "无额外奖励（自动完成）");
        Register("主线奖励-残片500", "Fragments x500", "残片 x500");
        Register("主线奖励-配方核心1", "Fractionation Recipe Core x1", "分馏配方核心 x1");
        Register("主线奖励-矿物复制塔原胚10", "Mineral Replication Proto x10", "矿物复制塔原胚 x10");
        Register("主线奖励-转化塔原胚10", "Conversion Tower Proto x10", "转化塔原胚 x10");
        Register("主线奖励-残片1000", "Fragments x1000", "残片 x1000");
        Register("主线奖励-增幅芯片5", "Fractionator Amplify Chip x5", "分馏塔增幅芯片 x5");
        Register("主线奖励-配方核心3", "Fractionation Recipe Core x3", "分馏配方核心 x3");
        Register("主线奖励-星际物流交互站2", "Interstellar Interaction Station x2", "星际物流交互站 x2");
        Register("主线奖励-残片2000", "Fragments x2000", "残片 x2000");

        Register("当前任务", "Current Task");
        Register("任务完成", "Task Complete");
        Register("全部完成", "All Completed");
        Register("领取奖励", "Claim Reward");
        Register("已领取", "Claimed");
        Register("进行中", "In Progress");
        Register("条件", "Condition");
        Register("奖励", "Reward");
        Register("科技解锁进度", "Tech unlocked: {0}", "科技解锁：{0}");
        Register("分馏次数进度", "Fraction successes: {0}/{1}", "分馏次数：{0}/{1}");
        Register("抽奖次数进度", "Draw count: {0}/{1}", "抽奖次数：{0}/{1}");
        Register("符文装备进度", "Rune equipped: {0}", "符文装备：{0}");
        Register("建筑等级进度", "Max building level: {0}/{1}", "建筑最高等级：{0}/{1}");
        Register("解锁配方进度", "Unlocked recipes: {0}/{1}", "解锁配方：{0}/{1}");
        Register("主线总进度", "Main progress: {0}/{1}", "主线进度：{0}/{1}");
        Register("主线任务已全部完成，恭喜！", "All main tasks completed. Congratulations!");
        Register("是", "Yes");
        Register("否", "No");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "主线任务");

        float x = 0f;
        float y = 18f + 7f;

        txtTaskName = wnd.AddText2(x, y, tab, "动态刷新");
        txtTaskName.supportRichText = true;

        y += 36f + 7f;
        txtTaskDesc = wnd.AddText2(x, y, tab, "动态刷新");
        txtTaskDesc.supportRichText = true;

        y += 36f + 7f;
        txtTaskStatus = wnd.AddText2(x, y, tab, "动态刷新");
        txtTaskStatus.supportRichText = true;

        y += 36f + 7f;
        txtTaskCondition = wnd.AddText2(x, y, tab, "动态刷新");
        txtTaskCondition.supportRichText = true;

        y += 36f + 7f;
        txtTaskReward = wnd.AddText2(x, y, tab, "动态刷新");
        txtTaskReward.supportRichText = true;

        y += 36f + 7f;
        btnClaim = wnd.AddButton(1, 3, y, tab, "领取奖励", 16, "btn-main-task-claim", ClaimCurrentReward);

        y += 36f + 7f;
        txtTotalProgress = wnd.AddText2(x, y, tab, "动态刷新");
        txtTotalProgress.supportRichText = true;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        AutoAdvanceStages();

        if (currentStage >= Tasks.Length) {
            txtTaskName.text = "全部完成".Translate().WithColor(Orange);
            txtTaskDesc.text = "主线任务已全部完成，恭喜！".Translate().WithColor(Orange);
            txtTaskStatus.text = "任务完成".Translate().WithColor(Orange);
            txtTaskCondition.text = $"{"条件".Translate()}：-";
            txtTaskReward.text = $"{"奖励".Translate()}：-";
            txtTotalProgress.text = string.Format("主线总进度".Translate(), Tasks.Length, Tasks.Length).WithColor(Orange);
            btnClaim.enabled = false;
            btnClaim.gameObject.SetActive(false);
            return;
        }

        TaskInfo task = Tasks[currentStage];
        bool completed = task.IsCompleted();

        string status = completed ? "任务完成".Translate().WithColor(Orange) : "进行中".Translate().WithColor(Blue);
        txtTaskName.text = $"{"当前任务".Translate()}：[{currentStage + 1}/{Tasks.Length}] {task.NameKey.Translate()}"
            .WithColor(completed ? Orange : Blue);
        txtTaskDesc.text = task.DescKey.Translate();
        txtTaskStatus.text = status;
        txtTaskCondition.text = $"{"条件".Translate()}：{task.ProgressText()}";
        txtTaskReward.text = $"{"奖励".Translate()}：{task.RewardKey.Translate()}";
        txtTotalProgress.text = string.Format("主线总进度".Translate(), currentStage, Tasks.Length);

        if (task.AutoClaim) {
            btnClaim.enabled = false;
            btnClaim.gameObject.SetActive(false);
            return;
        }

        btnClaim.gameObject.SetActive(true);
        bool canClaim = completed && !rewardClaimed;
        bool alreadyClaimed = completed && rewardClaimed;
        btnClaim.enabled = canClaim;
        btnClaim.button.interactable = canClaim;
        if (canClaim) {
            btnClaim.SetText("领取奖励".Translate());
        } else if (alreadyClaimed) {
            btnClaim.SetText("已领取".Translate());
        } else {
            btnClaim.SetText("进行中".Translate());
        }
    }

    private static void ClaimCurrentReward() {
        if (currentStage >= Tasks.Length) {
            return;
        }

        TaskInfo task = Tasks[currentStage];
        if (task.AutoClaim || rewardClaimed || !task.IsCompleted()) {
            return;
        }

        task.GrantReward?.Invoke();
        rewardClaimed = true;
        currentStage++;
        if (currentStage < Tasks.Length) {
            rewardClaimed = Tasks[currentStage].AutoClaim;
        }
    }

    private static void AutoAdvanceStages() {
        while (currentStage < Tasks.Length) {
            TaskInfo task = Tasks[currentStage];
            if (!task.IsCompleted()) {
                return;
            }

            if (task.AutoClaim) {
                task.GrantReward?.Invoke();
                currentStage++;
                rewardClaimed = currentStage < Tasks.Length && Tasks[currentStage].AutoClaim;
                continue;
            }

            if (rewardClaimed) {
                currentStage++;
                rewardClaimed = currentStage < Tasks.Length && Tasks[currentStage].AutoClaim;
                continue;
            }

            return;
        }
    }

    private static void GrantItems(params (int itemId, int count)[] rewards) {
        foreach ((int itemId, int count) in rewards) {
            AddItemToModData(itemId, count, 0, true);
            UIItemup.Up(itemId, count);
        }
    }

    private static bool IsTechUnlocked(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId);
    }

    private static int GetUnlockedRecipeCount() {
        return Enum.GetValues(typeof(ERecipe)).Cast<ERecipe>()
            .SelectMany(type => GetRecipesByType(type))
            .Count(recipe => recipe.Unlocked);
    }

    private static bool HasAnyEquippedRune() {
        return RuneManager.equippedRuneIds != null && RuneManager.equippedRuneIds.Any(id => id != 0);
    }

    private static string GetRuneProgressText() {
        return string.Format("符文装备进度".Translate(), HasAnyEquippedRune() ? "是".Translate() : "否".Translate());
    }

    private static bool HasBuildingLevel6() {
        return InteractionTower.Level >= 6 || MineralReplicationTower.Level >= 6 || PointAggregateTower.Level >= 6
            || ConversionTower.Level >= 6 || RectificationTower.Level >= 6;
    }

    private static string GetBuildingProgressText() {
        List<int> levels = [
            InteractionTower.Level,
            MineralReplicationTower.Level,
            PointAggregateTower.Level,
            ConversionTower.Level,
            RectificationTower.Level,
        ];
        int maxLevel = levels.Max();
        return string.Format("建筑等级进度".Translate(), maxLevel, 6);
    }

    private static string GetTechProgressText(int techId) {
        return string.Format("科技解锁进度".Translate(), IsTechUnlocked(techId) ? "是".Translate() : "否".Translate());
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("CurrentStage", br => currentStage = br.ReadInt32()),
            ("RewardClaimed", br => rewardClaimed = br.ReadBoolean())
        );

        currentStage = Math.Max(0, Math.Min(currentStage, Tasks.Length));
        if (currentStage >= Tasks.Length) {
            rewardClaimed = true;
        }
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("CurrentStage", bw => bw.Write(currentStage)),
            ("RewardClaimed", bw => bw.Write(rewardClaimed))
        );
    }

    public static void IntoOtherSave() {
        currentStage = 0;
        rewardClaimed = true;
    }

    #endregion
}
