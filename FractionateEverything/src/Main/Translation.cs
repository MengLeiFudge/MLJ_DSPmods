using static FractionateEverything.Utils.TranslationUtils;

namespace FractionateEverything.Main {
    public static class Translation {
        public static void AddTranslations() {

            #region 加载完成弹窗提示

            Register("FE标题", "Fractionate Everything Mod Tips", "万物分馏提示");
            Register("FE信息",
                "Thank you for using Fractionation Everything! This mod adds 6 Fractionators, and a lot of fractionation recipes.\n"
                + $"If you are using this mod for the first time, it is highly recommended that you {"check out the mod introduction page".AddBlueLabel()} to get an idea of its contents and features.\n"
                + $"You can change mod options in {"Settings - Miscellaneous".AddBlueLabel()} to get the full experience.\n"
                + "Recommended for use with Genesis Book, They Come From Void, and More Mega Structure.\n"
                + $"If you have any issues or ideas about the mod, please feedback to {"Github Issue".AddBlueLabel()}.\n"
                + "Have fun with fractionation!".AddOrangeLabel(),
                "感谢你使用万物分馏！该MOD添加了6个分馏塔，以及大量的分馏配方。\n"
                + $"如果你是首次使用该模组，强烈建议你{"查看模组介绍页面".AddBlueLabel()}以了解其内容与特色。\n"
                + $"你可以在{"设置-杂项".AddBlueLabel()}中修改MOD配置，以获得完整体验。\n"
                + "推荐与创世之书（Genesis Book）、深空来敌（They Come From Void）、更多巨构（More Mega Structure）一起使用。\n"
                + $"如果你在游玩MOD时遇到了任何问题，或者有宝贵的意见或建议，欢迎加入{"创世之书MOD交流群".AddBlueLabel()}反馈。\n"
                + "尽情享受分馏的乐趣吧！".AddOrangeLabel());
            Register("FE交流群", "View on Github", "加入交流群");
            Register("FE交流群链接",
                "https://github.com/MengLeiFudge/MLJ_DSPmods",
                "https://jq.qq.com/?_wv=1027&k=5bnaDEp3");
            Register("FE日志", "Update Log", "更新日志");
            Register("FE日志链接",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/");

            Register("FEBlueprints", "FEBlueprints", "万物分馏蓝图");
            Register("141标题", "Fractionate Everything 1.4.1 Update", "万物分馏1.4.1版本更新");
            Register("141信息",
                "Even though Fractionate Everything is part of the cheat mods, it has enough restrictions that it should be balanced.\n"
                + "—until I try it out for myself and get through the game in one night, that's what I'm thinking.\n"
                + "After my experience with the Fractionate Everything mod, the \"planning doesn't even play games\" joke hit me like a boomerang.\n\n"
                + $"The idea of Fractionate Everything is great, but it's fatal flaw is {"fractionation skips a lot of yield lines".AddRedLabel()} (especially with matrix fractionation).\n"
                + "Without using proliferators, 3% destroy probability means you only get 25% of the product, like 10,000 blue matrix => 2,500 red matrix => 625 yellow matrix => ......\n"
                + "Imagine when players realize that \"there is a huge loss of blue matrix to green matrix\", will they start building yellow or purple matrix production lines as I envisioned?\n"
                + "The answer is not. Production enhancers reduce damage and new matrices are overly complex, so players tend to expand the production of lower level matrices and then fractionate them.\n"
                + "In the end there are only three things to do in the game: expand the blue or red matrix production line, unlock tech, and wait.\n"
                + "Oh my god I bet there's no worse gaming experience than this!\n"
                + "It's hard to believe that the matrix fractionation chain is so poorly designed that it looks like Aunt Susan's apple pie next door!\n\n"
                + $"Back on topic, this mod was originally designed to be {"fun".AddBlueLabel()}, but I've been working on making it a {"balanced".AddBlueLabel()} mod.\n"
                + "It is an obvious fact that if you want to cheat, why not try \"Multfunction_Mod\" or \"CheatEnabler\"?\n"
                + "In response to matrix fractionation, I solicited ideas for improvements from a large number of players. I gathered various ways to change things, such as lag unlocking, fractionation consuming sand,\n"
                + "increasing power consumption, fractionated matrices not being able to be fractionated again, and so on, but they weren't in the way I expected.\n\n"
                + $"However, I figured out one thing: {"Fractionation should provide convenience, but not skip game progression.".AddOrangeLabel()}\n"
                + "Obviously, the Building-HighSpeed Fractionator is the best building. It's more of an aid and doesn't affect the game experience too much.\n"
                + "So in 1.4.1, I added switches for matrix fractionation, split the Building-HighSpeed Fractionator into Upgrade and Downgrade Fractionator and adjusted the fractionate recipes.\n"
                + "Hopefully these changes will make the mod more balanced.\n\n"
                + $"PS1: You can click {"Update Log".AddBlueLabel()} for all the changes in 1.4.1.\n"
                + $"PS2: Don't forget to check out {"the new settings".AddBlueLabel()} added in {"Settings - Miscellaneous".AddBlueLabel()}!\n"
                + $"PS3: To celebrate this update, some {"Blueprints".AddBlueLabel()} for Fractionate Everything have been added to the Blueprints library. \n"
                + $"Thanks to everyone for using Fractionate Everything. {"Have fun with fractionation!".AddOrangeLabel()}",
                "“尽管万物分馏属于作弊模组，但它的限制已经够多了，它应该是平衡的。”\n"
                + "——在我亲自试玩并一个晚上通关游戏之前，我都是这样想的。\n"
                + "在我体验万物分馏mod之后，“策划根本就不玩游戏”这个玩笑就像回旋镖一样打在了我的身上。\n\n"
                + $"万物分馏的想法是很棒的，但它的致命缺陷在于{"分馏会跳过大量产线".AddRedLabel()}（尤其是矩阵分馏）。\n"
                + "不使用增产剂的情况下，3%的损毁概率意味着你只能获得25%的产物，也就是10000蓝糖=>2500红糖=>625黄糖=>……\n"
                + "试想一下，当玩家意识到“蓝糖分馏为绿糖会有巨大损耗”，他们会按照我的预想开始构建黄糖或紫糖产线吗？\n"
                + "答案是不会。增产剂可以降低损毁，新矩阵又过于复杂，所以玩家倾向于扩大低级矩阵的产量，然后分馏它们。\n"
                + "最后游戏只剩下三件事：扩大蓝糖或红糖的产线，解锁科技，以及等待。\n"
                + "哦天哪，我敢打赌，没有比这更糟糕的游戏体验了！\n"
                + "真是难以相信，矩阵分馏链的设计竟然如此糟糕，就像隔壁苏珊婶婶做的苹果派一样！\n\n"
                + $"回归正题，分馏设计之初是为了{"有趣".AddBlueLabel()}，但是我一直致力于把它打造成一个{"平衡".AddBlueLabel()}的mod。\n"
                + "一个显而易见的事实是，如果你想作弊，为何不试试“Multfunction_Mod”或者“CheatEnabler”？\n"
                + "针对矩阵分馏，我向大量玩家征集了改进意见。我收集到了各种改动方式，例如滞后解锁、分馏消耗沙土、加大耗电、\n"
                + "分馏出的矩阵不能再次分馏等等，但它们都不是我所期望的方式。\n\n"
                + $"不过，我想明白了一件事情：{"分馏应该提供便利，但是不能跳过游戏进程。".AddOrangeLabel()}\n"
                + "显然，建筑极速分馏塔是最优秀的建筑。它更像是一种辅助手段，并不会过多影响游戏体验。\n"
                + "所以在1.4.1中，我为矩阵分馏增加了开关，将建筑极速分馏塔拆分为升级、降级分馏塔并调整了分馏配方。\n"
                + "希望这次改动能让分馏更加平衡。\n\n"
                + $"PS1：你可以点击{"更新日志".AddBlueLabel()}，以了解1.4.1的所有改动。\n"
                + $"PS2：千万不要忘记查看{"设置-杂项".AddBlueLabel()}中{"新增的设置项".AddBlueLabel()}！\n"
                + $"PS3：为了庆祝本次更新，一些万物分馏的{"蓝图".AddBlueLabel()}已添加至蓝图库。\n"
                + $"感谢万物分馏的每一位玩家。{"尽情享受分馏的乐趣吧！".AddOrangeLabel()}");

            #endregion

            #region 设置页面

            Register("DisableMessageBox", "Disable message box when loaded", "禁用万物分馏提示信息");
            Register("DisableMessageBoxAdditionalText",
                "(Disable additional information about Fractionate Everything in a messagebox after the mod is loaded)",
                "（在MOD加载完成后禁用弹窗显示万物分馏的额外信息）");
            Register("IconVersion", "Fractionation recipe icon style", "分馏配方图标样式");
            Register("v1", "Original deuterium fractionate", "原版重氢分馏");
            Register("v2", "Slanting line segmentation", "斜线分割");
            Register("v3", "Circular segmentation", "圆弧分割");
            Register("IconVersionAdditionalText",
                "(Need reload save for full effect)",
                "（需要重新载入存档才能完全生效）");
            Register("EnableDestroy", "Enable fractionation destroy probability", "启用分馏损毁概率");
            Register("EnableDestroyAdditionalText",
                "(Recommended to be turned on, effect immediately, no need to reload the save)",
                "（建议开启，立即生效，无需重新载入存档）");
            Register("EnableFuelRodFrac", "Enable fuel rod fractionation", "启用燃料棒分馏");
            Register("EnableFuelRodFracAdditionalText",
                "(Immediate effect, no need to reload the save)",
                "（立即生效，无需重新载入存档）");
            Register("EnableMatrixFrac", "Enable matrix fractionation", "启用矩阵分馏");
            Register("EnableMatrixFracAdditionalText",
                "(Recommended to be turned off, effect immediately, no need to reload the save)",
                "（建议关闭，立即生效，无需重新载入存档）");
            Register("EnableBuildingAsTrash", "Allow to recycle buildings", "允许回收建筑");
            Register("EnableBuildingAsTrashAdditionalText",
                "(Recommended to be turned off, effect immediately, no need to reload the save)",
                "（建议关闭，立即生效，无需重新载入存档）");

            #endregion

            #region 游戏内切换&配方显示

            Register("分馏页面1", "Fractionate I", "分馏 I");
            Register("分馏页面2", "Fractionate II", "分馏 II");
            Register("流动", "Flow");
            Register("损毁", "Destroy");

            #endregion

            #region 建筑说明

            Register("自然资源分馏塔", "Natural Resource Fractionator");
            Register("I自然资源分馏塔",
                "It is possible to duplicate most natural resources, avoiding the situation of being unable to explore for lack of resources. Once you have unlocked a natural resource's corresponding fractionation recipe, you can fractionate the natural resource.",
                "可以复制绝大多数自然资源，避免出现缺乏资源无法探索的情形。解锁某个自然资源对应的分馏配方后，即可对其进行自然资源分馏操作。");

            Register("升级分馏塔", "Upgrade Fractionator");
            Register("I升级分馏塔",
                $"Converts low-level items into few high-level items. Once you have unlocked a new up-downgrade fractionation recipe, you can perform an elevated fractionation operation on its {"input".AddOrangeLabel()}.",
                $"将低级物品转换为更少的高级物品。解锁新的升降级分馏配方后，即可对该配方的{"原料".AddOrangeLabel()}进行升级分馏操作。\n与{"降级分馏塔".AddOrangeLabel()}配合使用，即可拥有取之不尽的物品。");

            Register("降级分馏塔", "Downgrade Fractionator");
            Register("I降级分馏塔",
                $"Converts high-level items into more low-level items. Once you have unlocked a new up-downgrade fractionation recipe, you can perform an elevated fractionation operation on its {"output".AddOrangeLabel()}.",
                $"将高级物品转换为更多的低级物品。解锁新的升降级分馏配方后，即可对该配方的{"产物".AddOrangeLabel()}进行降级分馏操作。\n与{"升级分馏塔".AddOrangeLabel()}配合使用，即可拥有取之不尽的物品。");

            Register("垃圾回收分馏塔", "Trash Recycle Fractionator");
            Register("I垃圾回收分馏塔",
                $"Converts unwanted items into foundation or sand. All connections can be input, but only {"the front one".AddOrangeLabel()} can be output.\nSand will be {"directly added to the backpack".AddOrangeLabel()} without any additional operation.\n{"All items except buildings are acceptable, and proliferator points does not take effect.".AddOrangeLabel()}",
                $"将不需要的物品转换为地基或沙土。所有连接口都可输入，但只有{"正面".AddOrangeLabel()}的连接口可以输出。\n转换得到的沙土会{"直接加入背包".AddOrangeLabel()}，无需额外操作。\n{"可接受除建筑外的所有物品，增产点数不起作用。".AddOrangeLabel()}");
            Register("I垃圾回收分馏塔2",
                $"Converts unwanted items into foundation or sand. All connections can be input, but only {"the front one".AddOrangeLabel()} can be output.\nSand will be {"directly added to the backpack".AddOrangeLabel()} without any additional operation.\n{"Any item will be accepted, and proliferator points does not take effect.".AddOrangeLabel()}",
                $"将不需要的物品转换为地基或沙土。所有连接口都可输入，但只有{"正面".AddOrangeLabel()}的连接口可以输出。\n转换得到的沙土会{"直接加入背包".AddOrangeLabel()}，无需额外操作。\n{"可接受任何物品，增产点数不起作用。".AddOrangeLabel()}");

            Register("点数聚集分馏塔", "Points Aggregate Fractionator");
            Register("I点数聚集分馏塔",
                $"Crafts an item with 10 proliferator points by concentrating the item's proliferator points on a portion of the item, breaking the upper limit of proliferator points.\n{"Any item will be accepted.".AddOrangeLabel()} Success rate is related to the input item proliferator points.",
                $"将物品的增产点数集中到一部分物品上，突破增产点数的上限，从而制作出10增产点数的物品。\n{"可接受任何物品。".AddOrangeLabel()}\n成功率与输入物品的增产点数有关。");

            Register("增产分馏塔", "Increase Production Fractionator");
            Register("I增产分馏塔",
                $"Take full advantage of the proliferator points' proliferator feature to reorganize and duplicate the input items. It can fractionate everything and truly create something from nothing.\n{"Any item will be accepted.".AddOrangeLabel()} Success rate is related to the input item proliferator points, and maximum rate is related to the input item value.",
                $"充分利用增产点数的增产特性，将输入的物品进行重组复制。它可以分馏万物，真正达到无中生有的效果。\n{"可接受任何物品。".AddOrangeLabel()}\n成功率与输入物品的增产点数有关，最大值与输入物品的价值有关。");

            #endregion

            #region 建筑解锁科技

            Register("T自然资源分馏", "Natural Resource Fractionation", "自然资源分馏");
            Register("自然资源分馏描述",
                "In the course of Icarus' exploration, the Mastermind discovered that some star zones were extremely resource-poor and unsustainable. In order to make it easier for Icarus to explore the barren star zones, the Mastermind specially researched and issued the Fractionation of Natural Resources technology. This technology can be used to replicate the vast majority of natural resources, avoiding situations where lack of resources prevents exploration.",
                "在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。为了让伊卡洛斯能更轻松地探索贫瘠的星区，主脑特意研究并下发了自然资源的分馏技术。这项技术可以用来复制绝大多数自然资源，避免出现缺乏资源无法探索的情形。");
            Register("自然资源分馏结果",
                "You have mastered the Natural Resource Fractionation technology, which can be replicated indefinitely as long as you have a certain amount of natural resources.",
                "你已经掌握了自然资源分馏技术，只要拥有一定量的自然资源，就能对其进行无限复制。");

            Register("T升降级分馏", "Up-Downgrade Fractionation", "升降级分馏");
            Register("升降级分馏描述",
                "To facilitate Icarus's exploration, the Mastermind has issued downgraded tech for some items. Upgrading the Fractionation tech converts low-level items into fewer high-level items, and downgrading the Fractionation tech converts high-level items into multiple low-level items. There is an inexhaustible supply of items in the cycle of upgrading and downgrading. This tech is well adapted to buildings, less so to non-buildings, and only some non-building items can be fractionated. Nonetheless, it is a powerful aid on the quest.",
                "为了方便伊卡洛斯的探索，主脑下发了部分物品的升降级科技。升级分馏科技可以将低级物品转为更少的高级物品，降级分馏科技可将高级物品转为多个低级物品，升降级的循环中存在着取之不尽的物品。这项科技对建筑的适配性良好，对非建筑的适配性则较差，只有部分非建筑物品可以进行升降级操作。尽管如此，它依然是探索路上的强力援助。");
            Register("升降级分馏结果",
                "You have mastered the Up-Downgrade Fractionation technology and can now recycle process some items to copy them.",
                "你已经掌握了升降级分馏技术，可以用升降级分馏塔循环处理物品，从而实现物品的复制。");

            Register("T垃圾回收", "Trash Recycle", "垃圾回收");
            Register("垃圾回收描述",
                "Foundations and sand are an essential part of the exploration process. Trash pickup allows you to dispose of any item as foundation or sand, which is helpful for expanding into new terrain. However, this technology cannot be used in recycling buildings, and whether or not the waste is sprayed with proliferators does not affect the efficiency of the process.",
                "地基和沙土是探索过程中必不可少的一环。垃圾回收科技可以将任意的物品处理为地基或沙土，对新地盘的扩展很有帮助。不过，这项科技无法用于回收建筑，并且是否为垃圾喷涂增产剂不会影响处理效率。");
            Register("垃圾回收结果",
                "You have mastered the Trash Recycle technology and can recycle unwanted items, converting them into foundations or sand.",
                "你已经掌握了垃圾回收技术，可以回收不需要的物品，将其转换为地基或沙土。");

            Register("T增产点数聚集", "Proliferator Points Aggregation", "增产点数聚集");
            Register("增产点数聚集描述",
                "Due to the limitations of material technology, the spawn line is unable to create more advanced proliferators, but fractionation technology can break through the limitations by concentrating the raw material's proliferator points into a certain number of items. It was found that the proliferator points of items could be stacked indefinitely, but the portion over 10 points did not work. Proliferate Point Aggregation technology can fractionate just the items with 10 proliferator points.",
                "由于材料技术的限制，产线无法制造更高级的增产剂，但分馏技术可以将原料的增产点数集中到某几个物品上，从而突破增产剂的点数限制。研究发现，物品的增产点数可以无限叠加，但超过10点的部分不起作用。增产点数聚集技术可以刚好分馏出10点增产点数的物品。");
            Register("增产点数聚集结果",
                "You have mastered the Proliferator Points Aggregation technology. The item's proliferator points can now be pushed to the limit, and production capacity has been greatly increased!",
                "你已经掌握了增产点数聚集技术。现在物品的增产点数可以达到极限，产能得到了极大的提升！");

            Register("T增产分馏", "Increase production fractionate", "增产分馏");
            Register("增产分馏描述",
                "Although Natural Resource Fractionation and Up-Downgrade Fractionation are powerful, these techniques can only be used to process specific items. As research of dark fog continued to deepen, it seemed that the possibility existed of expanding this mode of replication to everything in the universe. It was found that if the effect of the item's proliferator points on the fractionation process changes from accelerate to increase, and the material reorganization technique was used to make the product the same as the input, it would be possible to achieve the effect of duplicating everything.\n"
                + "It is clear that the research process of correlating yield-enhancing effects with material reorganization is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
                + $"{"Warning:".AddOrangeLabel()} The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, {"Please research manually.".AddOrangeLabel()}",
                "自然资源分馏和升降级分馏虽然强大，但这些技术只能用于处理特定物品。随着对黑雾研究的不断深入，似乎存在将这种复制模式扩展到宇宙万物的可能性。研究发现，如果物品的增产点数对分馏过程的影响从加速变为增产，并利用物质重组技术使产物与输入相同，就可以达到复制万物的效果。\n"
                + "显然，将增产效果与物质重组关联的研究过程高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
                + $"{"警告：".AddOrangeLabel()}该科技的相关技术已被COSMO技术伦理委员会禁用，{"请手动研究。".AddOrangeLabel()}");
            Register("增产分馏结果",
                "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
                "你已经掌握了增产分馏技术。现在，你真正拥有了无中生有的能力！");

            #endregion

            #region 分馏塔输出效果增强科技

            Register("T分馏流动输出集装", "Fractionate Fluid Output Integrate", "分馏流动输出集装");
            Register("分馏流动输出集装等级",
                " Integration count of fractionate fluid output",
                " 分馏流动输出集装数量");
            Register("分馏流动输出集装描述",
                "Failed fractionated items will be integrated as much as possible in a cargo.",
                "分馏失败的原料将会尽可能以集装形式输出。");
            Register("分馏流动输出集装结果",
                "All failed fractionated items will now be integrated as much as possible in a cargo.",
                "现在，所有分馏失败的原料都将尽可能集装后再输出。");

            Register("T分馏产物输出集装", "Fractionate Product Output Integrate", "分馏产物输出集装");
            Register("分馏产物输出集装等级",
                " Integration count of fractionate product output",
                " 分馏产物输出集装数量");
            Register("分馏产物输出集装描述1",
                "Successful fractionated items will be integrated as much as possible in a cargo.",
                "分馏成功的产物将会尽可能以集装形式输出。");
            Register("分馏产物输出集装结果1",
                "All successful fractionated items will now be integrated as much as possible in a cargo.",
                "现在，所有分馏成功的产物都将尽可能集装后再输出。");
            Register("分馏产物输出集装描述2",
                "Further increases the integration count of fractionate product in a cargo.",
                "进一步提高分馏产物的集装数量。");
            Register("分馏产物输出集装结果2",
                "The integration count of fractionate product in a cargo was further improved.",
                "所有分馏产物的集装数量进一步提升了。");

            Register("T分馏永动", "Fractionate Forever", "分馏永动");
            Register("分馏持续运行",
                "Make specific types of fractionators keep running",
                "使特定种类的分馏塔可以持续运行");
            Register("分馏永动描述",
                $"When fractionators in a loop, a buildup of product in one of the fractionators will cause the entire loop to stop. The Fractionate Forever tech was then created, so that when the product cache reaches half of the upper limit, the fractionator will no longer fractionate, and simply carry items from the flow input to the flow output, thus keeping the entire loop of fractionators in continuous operation.\nThis tech {"will NOT effect".AddOrangeLabel()} Up-Downgrade Fractionator and Trash Recycle Fractionator.",
                $"当分馏塔形成环路时，只要其中某个分馏塔产物堆积，就会导致整条环路的分馏塔都停止运行。分馏永动科技随之诞生，当产物缓存达到上限一半时，分馏塔将不再分馏，仅仅将物品从流动输入搬运到流动输出，从而让整条环路的分馏塔持续运行。\n该科技{"不会作用于".AddOrangeLabel()}升降级分馏塔和垃圾回收分馏塔。");
            Register("分馏永动结果",
                "Now, some fractionators will continue running without product buildup.",
                "现在，一些分馏塔将会持续运行，不会出现产物堆积的情况了。");

            #endregion

        }
    }
}
