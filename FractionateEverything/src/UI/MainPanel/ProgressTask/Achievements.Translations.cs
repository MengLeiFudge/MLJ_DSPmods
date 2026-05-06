using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.MainPanel;
using FE.UI.MainPanel.Archive;
using FE.UI.MainPanel.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.GachaManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ProgressTask;

public static partial class Achievements {
    public static void AddTranslations() {
        Register("成就详情", "Achievements");
        Register("成就系统", "Achievement System");
        Register("成就", "Achievement");
        Register("成就分类-生产", "Production", "生产");
        Register("成就分类-开线", "Opening", "开线");
        Register("成就分类-配方", "Recipe", "配方");
        Register("成就分类-成长", "Growth", "成长");
        Register("成就分类-循环", "Recurring", "循环");
        Register("成就分类-黑雾", "Dark Fog", "黑雾");
        Register("成就分类-挑战", "Challenge", "挑战");
        Register("描述", "Description");

        Register("已获得成就", "Obtained: {0}/{1}", "已获得：{0}/{1}");
        Register("隐藏未解锁", "Locked: {0}", "未解锁：{0}");
        Register("成就加成格式",
            "Success +{0}% / Destroy -{1}% / Double +{2}% / Energy -{3}% / Logistics +{4}% / Power +{5}%",
            "成功+{0}% / 损毁-{1}% / 翻倍+{2}% / 能耗-{3}% / 物流+{4}% / 发电+{5}%");

        Register("已获得", "Obtained", "已获得");
        Register("未解锁", "Locked");
        Register("无额外功能奖励", "No extra functional bonus", "无额外功能奖励");
        Register("功能加成过多", "Multiple passive bonuses", "多项被动加成");
        Register("功能奖励-成功", "Success +{0}%", "成功 +{0}%");
        Register("功能奖励-损毁", "Destroy -{0}%", "损毁 -{0}%");
        Register("功能奖励-翻倍", "Double +{0}%", "翻倍 +{0}%");
        Register("功能奖励-能耗", "Energy -{0}%", "能耗 -{0}%");
        Register("功能奖励-物流", "Logistics +{0}%", "物流 +{0}%");
        Register("功能奖励-发电", "Power +{0}%", "发电 +{0}%");
        Register("隐藏成就提示", "???", "???");
        Register("隐藏成就描述", "Hidden achievement", "未解锁");
        Register("成就获得提示", "Achievement unlocked: {0}", "获得成就：{0}");

        Register("成就品阶-青铜", "Bronze", "青铜");
        Register("成就品阶-白银", "Silver", "白银");
        Register("成就品阶-黄金", "Gold", "黄金");
        Register("成就品阶-白金", "Platinum", "白金");

        Register("成就奖励-残片200", "Fragments x200", "残片 x200");
        Register("成就奖励-残片300", "Fragments x300", "残片 x300");
        Register("成就奖励-残片500", "Fragments x500", "残片 x500");
        Register("成就奖励-残片800", "Fragments x800", "残片 x800");
        Register("成就奖励-残片1000", "Fragments x1000", "残片 x1000");
        Register("成就奖励-残片2000", "Fragments x2000", "残片 x2000");
        Register("成就奖励-当前阶段矩阵2", "Current stage matrix x2", "当前阶段矩阵 x2");
        Register("成就奖励-当前阶段矩阵4", "Current stage matrix x4", "当前阶段矩阵 x4");
        Register("成就奖励-当前阶段矩阵8", "Current stage matrix x8", "当前阶段矩阵 x8");
        Register("成就奖励-当前阶段矩阵16", "Current stage matrix x16", "当前阶段矩阵 x16");
        Register("成就奖励-配方核心1", "Fragments x500", "残片 x500");
        Register("成就奖励-配方核心3", "Fragments x1000", "残片 x1000");
        Register("成就奖励-定向原胚1", "Directional Proto x1", "定向原胚 x1");
        Register("成就奖励-星际物流交互站1", "Interstellar Interaction Station x1", "星际物流交互站 x1");
        Register("成就奖励-精馏塔原胚3", "Rectification Tower Proto x3", "精馏塔原胚 x3");
        Register("成就奖励-循环任务自动领取", "Recurring task auto-claim", "循环任务自动领取");

        Register("分馏星河", "Fractionation Galaxy", "分馏星河");
        Register("分馏星海", "Fractionation Starsea", "分馏星海");
        Register("分馏宇宙", "Fractionation Universe", "分馏宇宙");
        Register("带速成型", "Throughput Online", "带速成型");
        Register("满带洪流", "Full-Belt Torrent", "满带洪流");
        Register("星河带速", "Galactic Throughput", "星河带速");
        Register("成就-任务自动化", "Task Automation");
        Register("成就-开线先锋", "Opening Pioneer");
        Register("开线统筹", "Opening Coordination", "开线统筹");
        Register("开线传说", "Opening Legend", "开线传说");
        Register("任务推进", "Task Momentum", "任务推进");
        Register("任务永动", "Task Perpetual", "任务永动");
        Register("成就-配方入门", "Recipe Beginner");
        Register("成就-配方学者", "Recipe Scholar");
        Register("成就-配方专家", "Recipe Expert");
        Register("成就-万物百科", "Everything Encyclopedia");
        Register("成就-工艺优化", "Craft Optimization");
        Register("成就-工艺大师", "Craft Master");
        Register("成就-万物归一", "All Into One");
        Register("黑雾信标", "Dark Fog Signal", "黑雾信标");
        Register("蜂巢猎场", "Hive Hunt", "蜂巢猎场");
    }
}
