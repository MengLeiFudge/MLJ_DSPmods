using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.Components;
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

public static partial class FracRecipeOperate {
    // ==================== 翻译注册 ====================

    public static void AddTranslations() {
        Register("分馏配方", "Fractionate Recipe");

        Register("当前物品", "Current item");
        Register("分馏配方提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("成功率", "Success Ratio");
        Register("损毁率", "Destroy Ratio");
        Register("产出", "Output");
        Register("随机", "Random");
        Register("单锁", "Single Lock");

        Register("配方已完全升级！", "Recipe has been completely upgraded!");
        Register("每个原料平均产出：", "Average output per raw material:");

        Register("建筑强化加成", "Building Enhancement Bonuses");
        Register("等级", "Level");
        Register("堆叠", "Stack");
        Register("能耗比", "Energy Ratio");
        Register("增产效率", "Proliferator Efficiency");
        Register("流体增强", "Fluid Enhancement");
        Register("成功率加成", "Success Boost");
        Register("已启用", "Enabled");
        Register("未启用", "Disabled");
        Register("牺牲特性", "Sacrifice Trait");
        Register("因果追踪", "Causal Tracing");
        Register("虚空喷射", "Void Spray");
        Register("双倍点数", "Double Points");
        Register("最大增产等级", "Max Inc Level");

        // 右列：等级信息
        Register("当前配方强化等级", "Current Recipe Enhancement Level");
        Register("当前配方等级", "Current Recipe Level");
        Register("配方未解锁", "Recipe Locked");
        Register("无通用加成", "No General Bonus");
        Register("不消耗原料", "No Consume");
        Register("翻倍产出", "Double Output");
        Register("解锁方式", "Unlock Method");
        Register("升级方式", "Upgrade Method");
        Register("成长进度", "Growth Progress");
        Register("通过开线抽取获取", "Obtain from Opening Pool");
        Register("通过开线抽取获取；部分前期配方也会随科技保底解锁",
            "Obtain from Opening Pool; some early recipes are also unlocked by tech baseline");
        Register("通过原胚闭环或成长规划获得；相关科技也会保底解锁",
            "Obtain from Proto Loop or Growth Planning; related tech also provides baseline unlock");
        Register("通过成长规划或固定入口获得；解锁后直接满级",
            "Obtain from Growth Planning or fixed entry; unlocking grants max level directly");
        Register("通过黑雾支线成长规划报价获得",
            "Obtain from Dark Fog branch Growth Planning offers");
        Register("通过科技保底解锁", "Unlocked by tech baseline");
        Register("首次获得对应黑雾物品后解锁", "Unlock after obtaining the related Dark Fog item once");
        Register("重复抽到该配方即可升级", "Upgrade by drawing the same recipe again");
        Register("处理对应原胚获取经验，重复获得时也会直接提升",
            "Gain EXP by processing matching proto; duplicate rewards also level it up");
        Register("处理对应原胚获取经验", "Gain EXP by processing the matching proto");
        Register("处理对应黑雾物品获取经验，也可通过成长规划补差",
            "Gain EXP by processing the matching Dark Fog item, or catch up through Growth Planning");
        Register("处理对应矩阵获取保底进度", "Build pity progress by processing matching matrices");
        Register("解锁后", "After unlocking");
        Register("直接满级", "be granted at max level immediately");
        Register("已完全升级，无需继续成长", "Fully upgraded; no further growth needed");
    }
}
