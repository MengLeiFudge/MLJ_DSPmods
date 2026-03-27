using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.View.DrawGrowth;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.Archive;

public static class FracStatistic {
    private static RectTransform window;
    private static RectTransform tab;
    private static readonly Text[] statLines = new Text[10];
    private static readonly int[] trackedBuildingIds = [
        IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE精馏塔, IFE行星内物流交互站
    ];

    public static void AddTranslations() {
        Register("分馏统计", "Frac Statistic");
        Register("统计-分馏成功总数", "Total Fraction Successes", "分馏成功总数");
        Register("统计-抽取总次数", "Total Draw Count", "抽取总次数");
        Register("统计-配方解锁", "Unlocked Recipes", "配方解锁");
        Register("统计-最高建筑等级", "Max Building Level", "最高建筑等级");
        Register("统计-当前模式", "Current Mode", "当前模式");
        Register("统计-当前聚焦", "Current Focus", "当前聚焦");
        Register("统计-当前阶段矩阵", "Current Stage Matrix", "当前阶段矩阵");
        Register("统计-黑雾矩阵库存", "Dark Fog Matrix Stock", "黑雾矩阵库存");
        Register("统计-建筑成长经验", "Building Growth EXP", "建筑成长经验");
        Register("统计-原胚库存", "Proto Stock", "原胚库存");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "分馏统计");
        float x = 0f;
        float y = 18f;
        for (int i = 0; i < statLines.Length; i++) {
            statLines[i] = wnd.AddText2(x, y, tab, "", 14, $"txtFracStat{i}");
            statLines[i].supportRichText = true;
            statLines[i].rectTransform.sizeDelta = new Vector2(1020f, 24f);
            y += 28f;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        long drawCount = MainWindow.SharedPanelState?.TicketRaffleTotalDraws ?? TicketRaffle.totalDraws;
        int unlockedRecipes = AllRecipes.Count(recipe => recipe.Unlocked);
        int totalRecipes = AllRecipes.Count;
        int maxBuildingLevel = trackedBuildingIds
            .Select(id => LDB.items.Select(id)?.Level() ?? 0)
            .Max();
        string currentMatrixName = LDB.items.Select(ItemManager.GetCurrentProgressMatrixId())?.name
                                   ?? ItemManager.GetCurrentProgressMatrixId().ToString();
        string focusName = GetCurrentFocusName();
        string protoStock = $"{GetItemTotalCount(IFE交互塔原胚) + GetItemTotalCount(IFE矿物复制塔原胚) + GetItemTotalCount(IFE点数聚集塔原胚) + GetItemTotalCount(IFE转化塔原胚) + GetItemTotalCount(IFE精馏塔原胚)}";

        statLines[0].text = $"{ "统计-分馏成功总数".Translate() }：{totalFractionSuccesses}";
        statLines[1].text = $"{ "统计-抽取总次数".Translate() }：{drawCount}";
        statLines[2].text = $"{ "统计-配方解锁".Translate() }：{unlockedRecipes}/{totalRecipes}";
        statLines[3].text = $"{ "统计-最高建筑等级".Translate() }：{maxBuildingLevel}";
        statLines[4].text = $"{ "统计-当前模式".Translate() }：{GachaService.GetModeNameKey().Translate()}";
        statLines[5].text = $"{ "统计-当前聚焦".Translate() }：{focusName}";
        statLines[6].text = $"{ "统计-当前阶段矩阵".Translate() }：{currentMatrixName}";
        statLines[7].text = $"{ "统计-黑雾矩阵库存".Translate() }：{GetItemTotalCount(I黑雾矩阵)}";
        statLines[8].text = $"{ "统计-原胚库存".Translate() }：{protoStock}";
        statLines[9].text = $"{ "统计-建筑成长经验".Translate() }：{GetBuildingExpSummary()}";
    }

    private static string GetBuildingExpSummary() {
        string[] parts = trackedBuildingIds
            .Select(id => $"{LDB.items.Select(id)?.name}:{BuildingManager.GetBuildingExp(id)}")
            .ToArray();
        return string.Join(" / ", parts);
    }

    private static string GetCurrentFocusName() {
        foreach (var focus in GachaService.FocusDefinitions) {
            if (focus.FocusType == GachaManager.CurrentFocus) {
                return focus.NameKey.Translate();
            }
        }
        return GachaManager.CurrentFocus.ToString();
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
