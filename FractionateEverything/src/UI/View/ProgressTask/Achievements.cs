using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static class Achievements {
    private static RectTransform window;
    private static RectTransform tab;

    private readonly struct AchievementInfo(
        string categoryKey,
        string nameKey,
        string descKey,
        string rewardKey,
        ETier tier,
        Func<bool> condition,
        Action grantReward,
        float successRateBonus = 0f,
        float destroyReductionBonus = 0f,
        float doubleOutputBonus = 0f,
        float energyReductionBonus = 0f,
        float logisticsBonus = 0f,
        float powerStageBonus = 0f) {
        public readonly string CategoryKey = categoryKey;
        public readonly string NameKey = nameKey;
        public readonly string DescKey = descKey;
        public readonly string RewardKey = rewardKey;
        public readonly ETier Tier = tier;
        public readonly Func<bool> Condition = condition;
        public readonly Action GrantReward = grantReward;
        public readonly float SuccessRateBonus = successRateBonus;
        public readonly float DestroyReductionBonus = destroyReductionBonus;
        public readonly float DoubleOutputBonus = doubleOutputBonus;
        public readonly float EnergyReductionBonus = energyReductionBonus;
        public readonly float LogisticsBonus = logisticsBonus;
        public readonly float PowerStageBonus = powerStageBonus;
    }

    private enum ETier {
        Bronze,
        Silver,
        Gold,
        Platinum,
    }

    private static Text txtTitle;
    private static Text txtUnlockedSummary;
    private static Text txtHiddenSummary;
    private static Text txtBonusSummary;

    private static Text[] txtAchievementNames;
    private static Text[] txtAchievementDescs;
    private static Text[] txtAchievementRewards;
    private static Text[] txtAchievementStates;
    private static MyImageButton[] rewardIcons;
    private static UIButton[] btnClaims;

    private const int RowsPerPage = 8;
    private const float AchievementRowSpacing = 52f;
    private static int currentPage;
    private static UIButton btnPrevPage;
    private static UIButton btnNextPage;
    private static Text txtPageIndicator;
    private static float listStartY;
    private static float listNameX;
    private static float listNameW;
    private static float listDescX;
    private static float listDescW;
    private static float listRewardX;
    private static float listRewardTextX;
    private static float listRewardTextW;
    private static float listStateX;
    private static float listStateW;
    private static float listActionX;
    private static float listActionW;

    private static void SyncCurrentPageFromSharedState() {
        currentPage = Math.Max(0, MainWindow.SharedPanelState?.AchievementsCurrentPage ?? 0);
    }

    private static void SyncCurrentPageToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.AchievementsCurrentPage = currentPage;
        }
    }

    private static readonly AchievementInfo[] achievements = [
        new("成就分类-生产", "成就-千锤百炼", "成就描述-千锤百炼", "成就奖励-残片200", ETier.Silver,
            () => totalFractionSuccesses >= 1000,
            () => GrantItems((IFE残片, 200)), successRateBonus: 0.02f),
        new("成就分类-生产", "成就-万物皆可分馏", "成就描述-万物皆可分馏", "成就奖励-残片300", ETier.Gold,
            () => totalFractionSuccesses >= 10000,
            () => GrantItems((IFE残片, 300)), successRateBonus: 0.05f),
        new("成就分类-生产", "成就-分馏之王", "成就描述-分馏之王", "成就奖励-残片500", ETier.Gold,
            () => totalFractionSuccesses >= 100000,
            () => GrantItems((IFE残片, 500)), destroyReductionBonus: 0.01f),
        new("成就分类-生产", "成就-永不停歇", "成就描述-永不停歇", "成就奖励-残片2000", ETier.Platinum,
            () => totalFractionSuccesses >= 1000000,
            () => GrantItems((IFE残片, 2000)), doubleOutputBonus: 0.03f),

        new("成就分类-开线", "成就-开线先锋", "成就描述-开线先锋", "成就奖励-当前阶段矩阵2", ETier.Bronze,
            () => TicketRaffle.totalDraws >= 100,
            () => GrantItems((GetCurrentStageMatrixId(), 2)), successRateBonus: 0.005f),
        new("成就分类-开线", "成就-开线专家", "成就描述-开线专家", "成就奖励-当前阶段矩阵4", ETier.Silver,
            () => TicketRaffle.totalDraws >= 500,
            () => GrantItems((GetCurrentStageMatrixId(), 4)), doubleOutputBonus: 0.005f),

        new("成就分类-配方", "成就-配方入门", "成就描述-配方入门", "成就奖励-配方核心1", ETier.Bronze,
            () => GetUnlockedRecipeCount() >= 5,
            () => GrantItems((IFE分馏配方核心, 1)), successRateBonus: 0.005f),
        new("成就分类-配方", "成就-配方学者", "成就描述-配方学者", "成就奖励-配方核心3", ETier.Silver,
            () => GetUnlockedRecipeCount() >= 30,
            () => GrantItems((IFE分馏配方核心, 3)), destroyReductionBonus: 0.005f),
        new("成就分类-配方", "成就-配方专家", "成就描述-配方专家", "成就奖励-当前阶段矩阵8", ETier.Gold,
            () => GetUnlockedRecipeCount() >= 80,
            () => GrantItems((GetCurrentStageMatrixId(), 8)), doubleOutputBonus: 0.01f),
        new("成就分类-配方", "成就-万物百科", "成就描述-万物百科", "成就奖励-残片800", ETier.Platinum,
            () => GetUnlockedRecipeCount() >= 150,
            () => GrantItems((IFE残片, 800)), powerStageBonus: 0.02f),

        new("成就分类-成长", "成就-工艺优化", "成就描述-工艺优化", "成就奖励-残片500", ETier.Bronze,
            () => GetMaxBuildingLevel() >= 6,
            () => GrantItems((IFE残片, 500)), energyReductionBonus: 0.05f),
        new("成就分类-成长", "成就-工艺大师", "成就描述-工艺大师", "成就奖励-残片1000", ETier.Gold,
            () => GetMaxBuildingLevel() >= 12,
            () => GrantItems((IFE残片, 1000)), energyReductionBonus: 0.10f),

        new("成就分类-成长", "成就-任务自动化", "成就描述-任务自动化", "成就奖励-循环任务自动领取", ETier.Gold,
            () => RecurringTask.TotalClaimedCount >= 100,
            RecurringTask.UnlockAutoClaim, logisticsBonus: 0.02f),

        new("成就分类-原胚", "成就-原胚循环", "成就描述-原胚循环", "成就奖励-定向原胚1", ETier.Silver,
            () => GetProtoInventoryCount() >= 20,
            () => GrantItems((IFE分馏塔定向原胚, 1)), logisticsBonus: 0.02f),
        new("成就分类-原胚", "成就-星际整备", "成就描述-星际整备", "成就奖励-星际物流交互站1", ETier.Gold,
            () => IsTechUnlocked(TFE星际物流交互),
            () => GrantItems((IFE星际物流交互站, 1)), logisticsBonus: 0.05f),
        new("成就分类-原胚", "成就-精馏开路", "成就描述-精馏开路", "成就奖励-精馏塔原胚3", ETier.Gold,
            () => IsTechUnlocked(TFE物品精馏),
            () => GrantItems((IFE精馏塔原胚, 3)), powerStageBonus: 0.05f),

        new("成就分类-综合", "成就-万物归一", "成就描述-万物归一", "成就奖励-当前阶段矩阵16", ETier.Platinum,
            () => totalFractionSuccesses >= 50000
                  && GetUnlockedRecipeCount() >= 100
                  && GetMaxBuildingLevel() >= 10,
            () => GrantItems((GetCurrentStageMatrixId(), 16)), successRateBonus: 0.03f, doubleOutputBonus: 0.02f, powerStageBonus: 0.03f),
    ];

    private static bool[] unlocked = new bool[achievements.Length];
    private static bool[] claimed = new bool[achievements.Length];

    public static void AddTranslations() {
        Register("成就详情", "Achievements");
        Register("成就系统", "Achievement System");
        Register("成就", "Achievement");
        Register("成就分类-生产", "Production", "生产");
        Register("成就分类-开线", "Opening", "开线");
        Register("成就分类-配方", "Recipe", "配方");
        Register("成就分类-成长", "Growth", "成长");
        Register("成就分类-原胚", "Proto", "原胚");
        Register("成就分类-综合", "Mixed", "综合");
        Register("描述", "Description");
        Register("状态", "Status");
        Register("操作", "Action");
        Register("奖励", "Reward");

        Register("已解锁成就", "Unlocked: {0}/{1}", "已解锁：{0}/{1}");
        Register("隐藏未解锁", "Locked: {0}", "未解锁：{0}");
        Register("成就加成格式", "Success +{0}% / Destroy -{1}% / Double +{2}% / Energy -{3}% / Logistics +{4}% / Power +{5}%", "成功+{0}% / 损毁-{1}% / 翻倍+{2}% / 能耗-{3}% / 物流+{4}% / 发电+{5}%");

        Register("已解锁", "Unlocked");
        Register("未解锁", "Locked");
        Register("领取", "Claim");
        Register("已领取", "Claimed");
        Register("未领取", "Unclaimed");
        Register("上一页", "Prev page");
        Register("下一页", "Next page");
        Register("隐藏成就提示", "???", "???");
        Register("隐藏成就描述", "Hidden achievement", "未解锁");

        Register("成就品阶-青铜", "Bronze", "青铜");
        Register("成就品阶-白银", "Silver", "白银");
        Register("成就品阶-黄金", "Gold", "黄金");
        Register("成就品阶-白金", "Platinum", "白金");

        Register("成就-千锤百炼", "Tempered Through Trials");
        Register("成就描述-千锤百炼", "Complete 1000 successful fractionations", "累计完成 1000 次分馏成功");
        Register("成就奖励-残片200", "Fragments x200", "残片 x200");

        Register("成就-万物皆可分馏", "Fractionate Everything");
        Register("成就描述-万物皆可分馏", "Complete 10000 successful fractionations", "累计完成 10000 次分馏成功");
        Register("成就奖励-残片300", "Fragments x300", "残片 x300");

        Register("成就-分馏之王", "King of Fractionation");
        Register("成就描述-分馏之王", "Complete 100000 successful fractionations", "累计完成 100000 次分馏成功");
        Register("成就奖励-残片500", "Fragments x500", "残片 x500");

        Register("成就-永不停歇", "Never Stop");
        Register("成就描述-永不停歇", "Complete 1000000 successful fractionations", "累计完成 1000000 次分馏成功");
        Register("成就奖励-残片2000", "Fragments x2000", "残片 x2000");

        Register("成就-开线先锋", "Opening Pioneer");
        Register("成就描述-开线先锋", "Perform 100 opening-line draws", "累计完成 100 次开线抽取");
        Register("成就奖励-当前阶段矩阵2", "Current stage matrix x2", "当前阶段矩阵 x2");

        Register("成就-开线专家", "Opening Expert");
        Register("成就描述-开线专家", "Perform 500 opening-line draws", "累计完成 500 次开线抽取");
        Register("成就奖励-当前阶段矩阵4", "Current stage matrix x4", "当前阶段矩阵 x4");

        Register("成就-配方入门", "Recipe Beginner");
        Register("成就描述-配方入门", "Unlock 5 fractionation recipes", "累计解锁 5 个分馏配方");
        Register("成就奖励-配方核心1", "Fractionation Recipe Core x1", "分馏配方核心 x1");

        Register("成就-配方学者", "Recipe Scholar");
        Register("成就描述-配方学者", "Unlock 30 fractionation recipes", "累计解锁 30 个分馏配方");
        Register("成就奖励-配方核心3", "Fractionation Recipe Core x3", "分馏配方核心 x3");

        Register("成就-配方专家", "Recipe Expert");
        Register("成就描述-配方专家", "Unlock 80 fractionation recipes", "累计解锁 80 个分馏配方");
        Register("成就奖励-当前阶段矩阵8", "Current stage matrix x8", "当前阶段矩阵 x8");

        Register("成就-万物百科", "Everything Encyclopedia");
        Register("成就描述-万物百科", "Unlock 150 fractionation recipes", "累计解锁 150 个分馏配方");
        Register("成就奖励-残片800", "Fragments x800", "残片 x800");

        Register("成就-工艺优化", "Craft Optimization");
        Register("成就描述-工艺优化", "Reach level 6 on any FE building", "任意万物分馏建筑等级达到 6");
        Register("成就奖励-残片500", "Fragments x500", "残片 x500");

        Register("成就-工艺大师", "Craft Master");
        Register("成就描述-工艺大师", "Reach level 12 on any FE building", "任意万物分馏建筑等级达到 12");
        Register("成就奖励-残片1000", "Fragments x1000", "残片 x1000");

        Register("成就-任务自动化", "Task Automation");
        Register("成就描述-任务自动化", "Complete 100 recurring tasks", "累计完成 100 次循环任务");
        Register("成就奖励-循环任务自动领取", "Future recurring tasks auto-claim", "后续循环任务自动领取");

        Register("成就-原胚循环", "Proto Cycle");
        Register("成就描述-原胚循环", "Hold 20 tower protos in storage", "仓储中持有 20 个分馏塔原胚");
        Register("成就奖励-定向原胚1", "Directional Proto x1", "定向原胚 x1");

        Register("成就-星际整备", "Interstellar Readiness");
        Register("成就描述-星际整备", "Unlock interstellar interaction technology", "解锁星际物流交互科技");
        Register("成就奖励-星际物流交互站1", "Interstellar Interaction Station x1", "星际物流交互站 x1");

        Register("成就-精馏开路", "Rectification Opening");
        Register("成就描述-精馏开路", "Unlock item deconstruction technology", "解锁物品精馏科技");
        Register("成就奖励-精馏塔原胚3", "Rectification Tower Proto x3", "精馏塔原胚 x3");

        Register("成就-万物归一", "All Into One");
        Register("成就描述-万物归一", "Reach top milestones in production, recipes and buildings", "在生产、配方与建筑中同时达到高阶里程碑");
        Register("成就奖励-当前阶段矩阵16", "Current stage matrix x16", "当前阶段矩阵 x16");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        SyncCurrentPageFromSharedState();
        window = trans;
        tab = wnd.AddTab(trans, "成就详情");

        txtAchievementNames = new Text[achievements.Length];
        txtAchievementDescs = new Text[achievements.Length];
        txtAchievementRewards = new Text[achievements.Length];
        txtAchievementStates = new Text[achievements.Length];
        rewardIcons = new MyImageButton[achievements.Length];
        btnClaims = new UIButton[achievements.Length];

        float x = 0f;
        float y = 18f + 7f;

        txtTitle = wnd.AddText2(x, y, tab, "成就系统", 17, "txtAchievementTitle");
        txtTitle.supportRichText = true;

        txtUnlockedSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementUnlockedSummary");
        txtUnlockedSummary.supportRichText = true;

        y += 26f;
        txtHiddenSummary = wnd.AddText2(x, y, tab, "动态刷新", 14, "txtAchievementHiddenSummary");
        txtHiddenSummary.supportRichText = true;

        txtBonusSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementBonusSummary");
        txtBonusSummary.supportRichText = true;

        y += 30f;

        listNameX = 0f;
        listNameW = 220f;
        listDescX = 220f;
        listDescW = 430f;
        listRewardX = 660f;
        listRewardTextX = 692f;
        listRewardTextW = 120f;
        listStateX = 830f;
        listStateW = 90f;
        listActionX = 945f;
        listActionW = 110f;

        wnd.AddText2(listNameX, y, tab, "成就", 14, "txtAchievementHeaderName");
        wnd.AddText2(listDescX, y, tab, "描述", 14, "txtAchievementHeaderDesc");
        wnd.AddText2(listRewardX, y, tab, "奖励", 14, "txtAchievementHeaderReward");
        wnd.AddText2(listStateX, y, tab, "状态", 14, "txtAchievementHeaderState");
        wnd.AddText2(listActionX, y, tab, "操作", 14, "txtAchievementHeaderAction");

        y += 26f;
        listStartY = y;

        for (int i = 0; i < achievements.Length; i++) {
            int j = i;

            txtAchievementNames[j] = wnd.AddText2(listNameX + x, y, tab, "动态刷新", 13, $"txtAchievementName{j}");
            txtAchievementNames[j].supportRichText = true;
            txtAchievementNames[j].rectTransform.sizeDelta = new Vector2(listNameW, 40f);

            txtAchievementDescs[j] = wnd.AddText2(listDescX + x, y, tab, "动态刷新", 13, $"txtAchievementDesc{j}");
            txtAchievementDescs[j].supportRichText = true;
            txtAchievementDescs[j].alignment = TextAnchor.UpperLeft;
            txtAchievementDescs[j].rectTransform.sizeDelta = new Vector2(listDescW, 40f);

            rewardIcons[j] = wnd.AddImageButton(listRewardX + x, y, tab, null).WithSize(24f, 24f);
            txtAchievementRewards[j] = wnd.AddText2(listRewardTextX + x, y, tab, "动态刷新", 13, $"txtAchievementReward{j}");
            txtAchievementRewards[j].supportRichText = true;
            txtAchievementRewards[j].rectTransform.sizeDelta = new Vector2(listRewardTextW, 32f);

            txtAchievementStates[j] = wnd.AddText2(listStateX + x, y, tab, "动态刷新", 13, $"txtAchievementState{j}");
            txtAchievementStates[j].supportRichText = true;
            txtAchievementStates[j].rectTransform.sizeDelta = new Vector2(listStateW, 32f);

            btnClaims[j] = wnd.AddButton(listActionX + x, y, listActionW, tab, "领取", 13, $"btnAchievementClaim{j}",
                () => ClaimAchievementReward(j));

            y += AchievementRowSpacing;
        }

        float paginationY = listStartY + AchievementRowSpacing * RowsPerPage + 8f;
        btnPrevPage = wnd.AddButton(GetPosition(0, 3).Item1, paginationY, tab, "上一页", onClick: PrevPage);
        txtPageIndicator = wnd.AddText2(GetPosition(1, 3).Item1, paginationY + 6f, tab, "");
        txtPageIndicator.alignment = TextAnchor.MiddleCenter;
        txtPageIndicator.rectTransform.sizeDelta = new(200f, txtPageIndicator.rectTransform.sizeDelta.y);
        btnNextPage = wnd.AddButton(GetPosition(2, 3).Item1, paginationY, tab, "下一页", onClick: NextPage);
    }

    private static bool IsPageVisible() {
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.None) return false;
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.Analysis) {
            return tab != null && tab.gameObject.activeInHierarchy;
        }
        return tab != null && tab.gameObject.activeSelf;
    }

    public static void UpdateUI() {
        if (!IsPageVisible()) {
            return;
        }

        for (int i = 0; i < achievements.Length; i++) {
            if (!unlocked[i] && achievements[i].Condition()) {
                unlocked[i] = true;
            }
        }

        int unlockedCount = unlocked.Count(v => v);
        int hiddenLockedCount = achievements.Length - unlockedCount;

        txtUnlockedSummary.text = string.Format("已解锁成就".Translate(), unlockedCount, achievements.Length).WithColor(Orange);
        txtHiddenSummary.text = string.Format("隐藏未解锁".Translate(), hiddenLockedCount).WithColor(Blue);
        txtBonusSummary.text = string.Format("成就加成格式".Translate(),
            GetSuccessRateBonus() * 100f,
            GetDestroyReductionBonus() * 100f,
            GetDoubleOutputBonus() * 100f,
            GetEnergyReductionBonus() * 100f,
            GetLogisticsBonus() * 100f,
            GetPowerStageBonus() * 100f).WithColor(Green);

        int totalPages = Math.Max(1, (achievements.Length + RowsPerPage - 1) / RowsPerPage);
        if (currentPage >= totalPages) {
            currentPage = totalPages - 1;
            SyncCurrentPageToSharedState();
        }

        for (int i = 0; i < achievements.Length; i++) {
            txtAchievementNames[i].gameObject.SetActive(false);
            txtAchievementDescs[i].gameObject.SetActive(false);
            txtAchievementRewards[i].gameObject.SetActive(false);
            txtAchievementStates[i].gameObject.SetActive(false);
            rewardIcons[i].gameObject.SetActive(false);
            btnClaims[i].gameObject.SetActive(false);
        }

        int start = currentPage * RowsPerPage;
        int end = Math.Min(start + RowsPerPage, achievements.Length);
        for (int i = start; i < end; i++) {
            int slot = i - start;
            float rowY = listStartY + slot * AchievementRowSpacing;

            txtAchievementNames[i].SetPosition(listNameX, rowY);
            txtAchievementDescs[i].SetPosition(listDescX, rowY - 8f);
            NormalizeRectWithMidLeft(rewardIcons[i], listRewardX, rowY);
            txtAchievementRewards[i].SetPosition(listRewardTextX, rowY);
            txtAchievementStates[i].SetPosition(listStateX, rowY);
            NormalizeRectWithMidLeft(btnClaims[i], listActionX, rowY);
            btnClaims[i].GetComponent<RectTransform>().sizeDelta = new(listActionW, btnClaims[i].GetComponent<RectTransform>().sizeDelta.y);

            txtAchievementNames[i].gameObject.SetActive(true);
            txtAchievementDescs[i].gameObject.SetActive(true);
            txtAchievementRewards[i].gameObject.SetActive(true);
            txtAchievementStates[i].gameObject.SetActive(true);
            btnClaims[i].gameObject.SetActive(true);

            RefreshAchievementRow(i);
        }

        UpdatePagination(totalPages);
    }

    private static void PrevPage() {
        if (currentPage <= 0) {
            return;
        }
        currentPage--;
        SyncCurrentPageToSharedState();
        UpdateUI();
    }

    private static void NextPage() {
        int totalPages = Math.Max(1, (achievements.Length + RowsPerPage - 1) / RowsPerPage);
        if (currentPage >= totalPages - 1) {
            return;
        }
        currentPage++;
        SyncCurrentPageToSharedState();
        UpdateUI();
    }

    private static void UpdatePagination(int totalPages) {
        txtPageIndicator.text = $"{(currentPage + 1)}/{totalPages}";
        btnPrevPage.button.interactable = currentPage > 0;
        btnNextPage.button.interactable = currentPage < totalPages - 1;
    }

    public static float GetSuccessRateBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].SuccessRateBonus;
            }
        }
        return bonus;
    }

    public static float GetDestroyReductionBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].DestroyReductionBonus;
            }
        }
        return bonus;
    }

    public static float GetDoubleOutputBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].DoubleOutputBonus;
            }
        }
        return bonus;
    }

    public static float GetEnergyReductionBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].EnergyReductionBonus;
            }
        }
        return bonus;
    }

    public static float GetLogisticsBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].LogisticsBonus;
            }
        }
        return bonus;
    }

    public static float GetPowerStageBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].PowerStageBonus;
            }
        }
        return bonus;
    }

    private static void RefreshAchievementRow(int index) {
        if (!unlocked[index]) {
            txtAchievementNames[index].text = "隐藏成就提示".Translate().WithColor(Gray);
            txtAchievementDescs[index].text = "隐藏成就描述".Translate().WithColor(Gray);
            txtAchievementRewards[index].text = "";
            txtAchievementStates[index].text = "未解锁".Translate().WithColor(Gray);
            rewardIcons[index].gameObject.SetActive(false);
            btnClaims[index].button.interactable = false;
            btnClaims[index].SetText("隐藏成就提示");
            return;
        }

        AchievementInfo info = achievements[index];
        string tierTag = GetTierTag(info.Tier);
        Color tierColor = GetTierColor(info.Tier);
        bool hasRewardIcon = TryGetRewardIconInfo(info.RewardKey, out int rewardItemId, out int rewardCount);
        rewardIcons[index].gameObject.SetActive(hasRewardIcon);
        rewardIcons[index].Proto = hasRewardIcon ? LDB.items.Select(rewardItemId) : null;
        string rewardText = hasRewardIcon
            ? $"x{rewardCount}".WithColor(Blue)
            : info.RewardKey.Translate().WithColor(Blue);

        txtAchievementNames[index].text = $"{tierTag.WithColor(tierColor)} [{info.CategoryKey.Translate()}] {info.NameKey.Translate()}";
        txtAchievementDescs[index].text = info.DescKey.Translate();
        txtAchievementRewards[index].text = rewardText;
        rewardIcons[index].gameObject.SetActive(hasRewardIcon);

        bool alreadyClaimed = claimed[index];
        txtAchievementStates[index].text = alreadyClaimed
            ? "已领取".Translate().WithColor(Green)
            : "未领取".Translate().WithColor(Orange);

        bool canClaim = unlocked[index] && !alreadyClaimed;
        btnClaims[index].button.interactable = canClaim;
        btnClaims[index].SetText(canClaim ? "领取" : "已领取");
    }

    private static void ClaimAchievementReward(int index) {
        if (index < 0 || index >= achievements.Length) {
            return;
        }
        if (!unlocked[index] || claimed[index]) {
            return;
        }

        achievements[index].GrantReward?.Invoke();
        claimed[index] = true;
        RefreshAchievementRow(index);
    }

    private static string GetTierTag(ETier tier) {
        return tier switch {
            ETier.Bronze => "[铜]",
            ETier.Silver => "[银]",
            ETier.Gold => "[金]",
            ETier.Platinum => "[铂]",
            _ => "[?]",
        };
    }

    private static Color GetTierColor(ETier tier) {
        return tier switch {
            ETier.Bronze => Orange,
            ETier.Silver => White,
            ETier.Gold => Gold,
            ETier.Platinum => Blue,
            _ => White,
        };
    }

    private static bool TryGetRewardIconInfo(string rewardKey, out int itemId, out int count) {
        switch (rewardKey) {
            case "成就奖励-残片200":
                itemId = IFE残片;
                count = 200;
                return true;
            case "成就奖励-残片300":
                itemId = IFE残片;
                count = 300;
                return true;
            case "成就奖励-残片500":
                itemId = IFE残片;
                count = 500;
                return true;
            case "成就奖励-残片2000":
                itemId = IFE残片;
                count = 2000;
                return true;
            case "成就奖励-当前阶段矩阵2":
                itemId = GetCurrentStageMatrixId();
                count = 2;
                return true;
            case "成就奖励-当前阶段矩阵4":
                itemId = GetCurrentStageMatrixId();
                count = 4;
                return true;
            case "成就奖励-配方核心1":
                itemId = IFE分馏配方核心;
                count = 1;
                return true;
            case "成就奖励-配方核心3":
                itemId = IFE分馏配方核心;
                count = 3;
                return true;
            case "成就奖励-当前阶段矩阵8":
                itemId = GetCurrentStageMatrixId();
                count = 8;
                return true;
            case "成就奖励-残片800":
                itemId = IFE残片;
                count = 800;
                return true;
            case "成就奖励-残片1000":
                itemId = IFE残片;
                count = 1000;
                return true;
            case "成就奖励-定向原胚1":
                itemId = IFE分馏塔定向原胚;
                count = 1;
                return true;
            case "成就奖励-星际物流交互站1":
                itemId = IFE星际物流交互站;
                count = 1;
                return true;
            case "成就奖励-精馏塔原胚3":
                itemId = IFE精馏塔原胚;
                count = 3;
                return true;
            case "成就奖励-当前阶段矩阵16":
                itemId = GetCurrentStageMatrixId();
                count = 16;
                return true;
            default:
                itemId = 0;
                count = 0;
                return false;
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

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RectificationTower.Level))));
    }

    private static int GetProtoInventoryCount() {
        return (int)(GetItemTotalCount(IFE交互塔原胚)
                     + GetItemTotalCount(IFE矿物复制塔原胚)
                     + GetItemTotalCount(IFE点数聚集塔原胚)
                     + GetItemTotalCount(IFE转化塔原胚)
                     + GetItemTotalCount(IFE精馏塔原胚)
                     + GetItemTotalCount(IFE分馏塔定向原胚));
    }

    private static int GetCurrentStageMatrixId() {
        return GameMain.history != null && GameMain.history.TechUnlocked(T宇宙矩阵)
            ? I宇宙矩阵
            : GameMain.history != null && GameMain.history.TechUnlocked(T引力矩阵)
                ? I引力矩阵
                : GameMain.history != null && GameMain.history.TechUnlocked(T信息矩阵)
                    ? I信息矩阵
                    : GameMain.history != null && GameMain.history.TechUnlocked(T结构矩阵)
                        ? I结构矩阵
                        : GameMain.history != null && GameMain.history.TechUnlocked(T能量矩阵)
                            ? I能量矩阵
                            : I电磁矩阵;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        SyncCurrentPageFromSharedState();
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);

        r.ReadBlocks(
            ("UnlockedFlags", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, achievements.Length); i++) {
                    unlocked[i] = br.ReadBoolean();
                }
                for (int i = achievements.Length; i < count; i++) {
                    br.ReadBoolean();
                }
            }),
            ("ClaimedFlags", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, achievements.Length); i++) {
                    claimed[i] = br.ReadBoolean();
                }
                for (int i = achievements.Length; i < count; i++) {
                    br.ReadBoolean();
                }
            })
        );

        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i] && !unlocked[i]) {
                claimed[i] = false;
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("UnlockedFlags", bw => {
                bw.Write(achievements.Length);
                foreach (bool value in unlocked) {
                    bw.Write(value);
                }
            }),
            ("ClaimedFlags", bw => {
                bw.Write(achievements.Length);
                foreach (bool value in claimed) {
                    bw.Write(value);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        currentPage = 0;
        SyncCurrentPageToSharedState();
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);
    }

    #endregion
}
