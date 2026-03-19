using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketRaffle {
    public static long totalDraws;

    public static float[] RecipeValues => [
        (float)System.Math.Sqrt(itemValue[IFE电磁奖券] * 33.614f),
        (float)System.Math.Sqrt(itemValue[IFE能量奖券] * 48.02f),
        (float)System.Math.Sqrt(itemValue[IFE结构奖券] * 68.6f),
        (float)System.Math.Sqrt(itemValue[IFE信息奖券] * 98f),
        (float)System.Math.Sqrt(itemValue[IFE引力奖券] * 140f),
        (float)System.Math.Sqrt(itemValue[IFE宇宙奖券] * 200f),
        (float)System.Math.Sqrt(itemValue[IFE黑雾奖券] * 100f),
    ];

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");

        Register("配方奖池", "Recipe pool");
        Register("配方奖池说明",
            "Various fractionate recipes and Fractionate Recipe Core can be drawn.\n"
            + "Higher tier tickets can also yield recipes for lower technological tiers.\n"
            + "The Quantum Replication recipes can only be drawn after all the other recipes are full of echoes.",
            "可以抽取各种分馏配方，以及分馏配方核心。\n"
            + "高等级奖券也可以抽到低层次科技的相关配方。\n"
            + "其他配方全部满回响后，才能抽取到量子复制配方。");

        Register("当前奖券", "Current ticket");
        Register("奖券数目", "Ticket count");
        Register("：", ": ");

        Register("抽奖", "Draw");
        Register("自动百连", "Auto hundred draws");

        Register("原胚奖池", "Fractionator Proto pool");
        Register("原胚奖池说明",
            "Various fractionator prototypes and Fractionator Increase Chip can be drawn.",
            "可以抽取各种分馏塔原胚，以及分馏塔增幅芯片。");

        Register("材料奖池", "Material pool");
        Register("材料奖池说明",
            "Various materials can be drawn.\n"
            + "Only materials that have been unlocked can be drawn.\n"
            + "Unable to draw Matrix cards (except Dark Fog Matrix) or lottery tickets.",
            "可以抽取各种材料。\n"
            + "只能抽到已解锁的材料。\n"
            + "无法抽到矩阵（黑雾矩阵除外）、奖券。");

        Register("建筑奖池", "Building pool");
        Register("建筑奖池说明",
            "Various buildings can be drawn.\n"
            + "Only buildings that have been unlocked can be drawn.\n"
            + "Unable to draw the newly added fractionator or logistic interaction station.",
            "可以抽取各种建筑。\n"
            + "只能抽到已解锁的建筑。\n"
            + "无法抽到新增的分馏塔、物流交互站。");

        Register("符文奖池", "Rune pool");
        Register("符文奖池说明",
            "Various Runes and Fractionation Essences can be drawn.\n"
            + "Rune star level depends on the type of ticket used.\n"
            + "Universe Ticket provides guaranteed sub-stats.",
            "可以抽取各种符文，以及分馏精华。\n"
            + "符文星级取决于使用的奖券种类。\n"
            + "宇宙奖券提供保底子词条。");

        Register("抽奖结果", "Raffle results");
        Register("获得了以下物品", "Obtained the following items");
        Register("谢谢惠顾喵", "Thank you meow");
        Register("已解锁", "unlocked");
        Register("已转为同名回响提示",
            "has been converted to a homonym echo (currently holding {0} homonym echoes)",
            "已转为同名回响（当前持有 {0} 同名回响）");
        Register("所有奖励已存储至分馏数据中心。", "All rewards have been stored in the fractionation data centre.");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) { }

    public static void UpdateUI() { }

    public static void FreshPool(int poolId) { }

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() {
        totalDraws = 0;
    }
}
