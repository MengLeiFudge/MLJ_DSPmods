using static FE.Utils.I18NUtils;

namespace FE.Logic;

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
            + $"如果你在游玩MOD时遇到了任何问题，或者有宝贵的意见或建议，欢迎加入{"万物分馏MOD交流群".AddBlueLabel()}反馈。\n"
            + "尽情享受分馏的乐趣吧！".AddOrangeLabel());
        Register("FE交流群", "View on Github", "加入交流群");
        Register("FE交流群链接",
            "https://github.com/MengLeiFudge/MLJ_DSPmods",
            "https://qm.qq.com/q/zzicz6j9zW");
        Register("FE日志", "Update Log", "更新日志");
        Register("FE日志链接",
            "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/",
            "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/");

        #region 旧的版本更新说明（做成彩蛋）

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
            + "所以在1.4.1中，我为矩阵分馏增加了开关，将建筑极速分馏塔拆分为升级、转化塔并调整了分馏配方。\n"
            + "希望这次改动能让分馏更加平衡。\n\n"
            + $"PS1：你可以点击{"更新日志".AddBlueLabel()}，以了解1.4.1的所有改动。\n"
            + $"PS2：千万不要忘记查看{"设置-杂项".AddBlueLabel()}中{"新增的设置项".AddBlueLabel()}！\n"
            + $"PS3：为了庆祝本次更新，一些万物分馏的{"蓝图".AddBlueLabel()}已添加至蓝图库。\n"
            + $"感谢万物分馏的每一位玩家。{"尽情享受分馏的乐趣吧！".AddOrangeLabel()}");

        Register("142标题", "Fractionate Everything 1.4.2 Update", "万物分馏1.4.2版本更新");
        Register("142信息",
            "Haven't seen you for a long time, and I miss you very much. It's been another month and a half after last update. How are you?\n\n"
            + $"Remember in version 1.4.1, I said I had a couple {"Blueprints".AddBlueLabel()} for you guys? That was indeed true, and it wasn't an April Fool's joke. \n"
            + $"-- Just {"R2".AddBlueLabel()} took my blueprints folder {"deleted".AddRedLabel()}!Σ(っ\u00b0Д\u00b0;)っ\n"
            + "As for why I didn't update the version after discovering this problem that day, it's because the folder is gone but the blueprints are still there...(O_o)??\n"
            + "Yes it's strange but the folder is gone and the blueprints are out there!\n"
            + $"{"By the way, it's definitely NOT I'm lazy to update!".AddOrangeLabel()}\n"
            + "I'm curious if anyone has actually gone through the folder where the mod is and found those blueprints. (If so be sure to let me know www)\n"
            + "Of course, this is a small problem for me. Since the files are unusable, I'll just shove them right inside the code!\n"
            + "Shakespeare once said: there's nothing that can't be solved by one string. If there is, then there are four! (three blueprints + intro)\n"
            + $"If all goes well, you should see the blueprints this time. Be sure to {"recheck the blueprint library!".AddBlueLabel()}\n\n"
            + "In addition, this update fixes an issue with the settings page reporting errors.\n\n"
            + "The main reason there hasn't been an update lately is the lack of inspiration, and I really can't think of anything else to optimize.\n"
            + "I'm sure you can see that the idea of using fractionation as the core actually greatly limits the functionality of the mod.\n"
            + $"However, after talking with the group the other day, I've determined the general direction of the MOD afterward - that is {"Draw".AddOrangeLabel()}.\n"
            + "\"Big company with a billion dollar market cap will only make card draw games, but a team of only five people can make Dyson Sphere Program\", I'm sure you've all heard of it.\n"
            + $"{"But why couldn't the Dyson Sphere Program be a card draw game, right?".AddOrangeLabel()} I'm coming now!\n\n"
            + "The next update will be centered around randomness and card draw. The following information can be revealed:\n"
            + "1.There will be new fractionators for getting currency dedicated to card draw.\n"
            + "2.Fractionation recipes can be obtained through technology, raffle, and redemption.\n"
            + "3.The same fractionation recipe has different qualities, the higher the quality the harder it is to obtain.\n"
            + "4.I hope to complete the initial version before the end of September. Welcome to join the group to experience the latest beta version and give your opinion!\n\n"
            + $"And finally, thanks for your support! {"Have fun with fractionation!".AddOrangeLabel()}",
            "许久不见，甚是想念。时间过得真快啊，转眼又是一个半月。大家过的怎么样啊？\n\n"
            + $"还记得在1.4.1版本中，我说过为你们准备了几个{"蓝图".AddBlueLabel()}吗？那确实是真的，它并不是一个愚人节玩笑。\n"
            + $"——只不过{"R2".AddBlueLabel()}将我的蓝图文件夹{"删掉了".AddRedLabel()}！Σ(っ\u00b0Д\u00b0;)っ\n"
            + "至于我为什么当天发现这个问题之后，却并没有更新版本，是因为文件夹没了但是蓝图还在……(O_o)??\n"
            + "事实正是如此，文件夹没了，蓝图跑外面了！\n"
            + $"{"顺带一提，绝对不是因为我懒才不更新的！".AddOrangeLabel()}）\n"
            + "我很好奇到底有没有人去翻翻MOD所在的文件夹，把那几个蓝图找出来。（如果有的话务必告诉我哈哈）\n"
            + "当然，这点小小的问题是难不倒我的。既然文件无法使用，那我就把它们直接塞到代码里面！\n"
            + "鲁迅曾经说过：没有什么是一个字符串解决不了的。如果有，那就四个！（三个蓝图+简介）\n"
            + $"如果一切顺利的话，这次应该能看到蓝图了。请务必{"重新检查一下蓝图库！".AddBlueLabel()}\n\n"
            + "除此之外，此次更新修复了设置页面报错的问题。\n\n"
            + "近期一直没有更新的主要原因是缺失灵感，我确实想不出有什么可以优化的地方了。\n"
            + "想必大家也能看出来，以分馏作为核心的思路其实大大限制了MOD的功能。\n"
            + $"不过，前几天与群友交流之后，我确定了MOD之后的大致方向——那就是{"抽奖".AddOrangeLabel()}。\n"
            + $"“百亿大厂十连抽，五人团队戴森球”，想必大家都听过。嘿嘿嘿，{"谁说戴森球不能十连抽？".AddOrangeLabel()}我踏马莱纳！\n\n"
            + "接下来的更新将主要以“随机性与抽卡”作为核心。可以透露的信息如下：\n"
            + "1.会有新的分馏塔，用于获取专用于抽卡的货币。\n"
            + "2.分馏配方可通过科技、抽奖、兑换等方式获取。\n"
            + "3.同一个分馏配方有不同品质，越高品质越难获取。\n"
            + "4.希望能在9月底之前完成初版。欢迎加群体验最新测试版，并提出你的看法！\n\n"
            + $"一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".AddOrangeLabel()}");

        #endregion

        Register("143标题", "Fractionate Everything 1.4.3 Update", "万物分馏1.4.3版本更新");
        Register("143信息",
            "This is a minor update that fix original fractionator can not fractionate hydrogen into deuterium.\n"
            + "Thanks to starfi5h for exploring why this bug appeared (I really didn't reproduce the bug, so I was never able to fix it).\n\n"
            + "Advertisement: If you want to quantify the production, we recommend using the web calculator [https://dsp-calc.pro/]\n\n"
            + "Version 1.5.0 is still in the works, and it's more of a pain in the ass than I expected.\n"
            + "The design part is basically finished at the moment, and I'm in the code-writing stage, though the UI aspect might be a challenge.\n"
            + "In new version, a fractionator similar to Trash Recycle Fractionator will be added for unlocking fractionation recipes, and providing currency to the store.\n"
            + "Recipes have 'star' and 'rarity' attribute, which indicate the efficiency and rarity of the recipe, respectively.\n"
            + "In addition to fixed fractionation recipes, an optional fractionate recipe can be customized for output, allowing items to be reorganized in the early stages!\n"
            + $"The above is all that's included in this update. As always, thank you for your support! {"Have fun with fractionation!".AddOrangeLabel()}",
            "这次是一个小更新，修复了原版分馏塔无法将氢分馏为重氢的问题。\n"
            + "感谢starfi5h大佬对此bug出现原因的探究（我确实没有复现此bug，所以一直无法修复）。\n\n"
            + "打个广告：如果你想量化产线，推荐使用网页版量化计算器【https://dsp-calc.pro/】\n\n"
            + "1.5.0版本的抽卡还在制作中，比我预想的要麻烦的多。\n"
            + "目前设计部分基本完工，正处于编写代码阶段，不过UI方面可能是个难题。\n"
            + "在新的版本中，将会增加一个与垃圾回收分馏塔类似的分馏塔，用于解锁分馏配方、向商店提供货币。\n"
            + "配方具有“星级”与“稀有度”这两个属性，分别表示配方的效率与稀有程度。\n"
            + "除了固定的分馏配方，还有可自定义输出的自选分馏配方，可以在前期将物品随意重组！\n"
            + $"以上就是本次更新的全部内容。一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".AddOrangeLabel()}");

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

        Register("分馏页面", "Fractionate", "分馏");
        Register("无配方", "NoRecipe");
        Register("未解锁", "NotUnlock");
        Register("永动", "Forever");
        Register("流动", "Flow");
        Register("损毁", "Destroy");

        #endregion

        #region 建筑说明

        Register("矿物复制塔", "Natural Resource Fractionator");
        Register("I矿物复制塔",
            "It is possible to duplicate most natural resources, avoiding the situation of being unable to explore for lack of resources. Once you have unlocked a natural resource's corresponding fractionation recipe, you can fractionate the natural resource.",
            "可以复制绝大多数自然资源，避免出现缺乏资源无法探索的情形。解锁某个自然资源对应的分馏配方后，即可对其进行自然资源分馏操作。");

        Register("转化塔", "Upgrade Fractionator");
        Register("I转化塔",
            $"Converts low-level items into few high-level items. Once you have unlocked a new up-downgrade fractionation recipe, you can perform an elevated fractionation operation on its {"input".AddOrangeLabel()}.",
            $"将低级物品转换为更少的高级物品。解锁新的升转化配方后，即可对该配方的{"原料".AddOrangeLabel()}进行升级分馏操作。\n与{"转化塔".AddOrangeLabel()}配合使用，即可拥有取之不尽的物品。");

        Register("转化塔", "Downgrade Fractionator");
        Register("I转化塔",
            $"Converts high-level items into more low-level items. Once you have unlocked a new up-downgrade fractionation recipe, you can perform an elevated fractionation operation on its {"output".AddOrangeLabel()}.",
            $"将高级物品转换为更多的低级物品。解锁新的升转化配方后，即可对该配方的{"产物".AddOrangeLabel()}进行转化操作。\n与{"转化塔".AddOrangeLabel()}配合使用，即可拥有取之不尽的物品。");

        Register("垃圾回收分馏塔", "Trash Recycle Fractionator");
        Register("I垃圾回收分馏塔",
            $"Converts unwanted items into foundation or sand. All connections can be input, but only {"the front one".AddOrangeLabel()} can be output.\nSand will be {"directly added to the backpack".AddOrangeLabel()} without any additional operation.\n{"All items except buildings are acceptable, and proliferator points does not take effect.".AddOrangeLabel()}",
            $"将不需要的物品转换为地基或沙土。所有连接口都可输入，但只有{"正面".AddOrangeLabel()}的连接口可以输出。\n转换得到的沙土会{"直接加入背包".AddOrangeLabel()}，无需额外操作。\n{"可接受除建筑外的所有物品，增产点数不起作用。".AddOrangeLabel()}");
        Register("I垃圾回收分馏塔2",
            $"Converts unwanted items into foundation or sand. All connections can be input, but only {"the front one".AddOrangeLabel()} can be output.\nSand will be {"directly added to the backpack".AddOrangeLabel()} without any additional operation.\n{"Any item will be accepted, and proliferator points does not take effect.".AddOrangeLabel()}",
            $"将不需要的物品转换为地基或沙土。所有连接口都可输入，但只有{"正面".AddOrangeLabel()}的连接口可以输出。\n转换得到的沙土会{"直接加入背包".AddOrangeLabel()}，无需额外操作。\n{"可接受任何物品，增产点数不起作用。".AddOrangeLabel()}");

        Register("点数聚集塔", "Points Aggregate Fractionator");
        Register("I点数聚集塔",
            $"Crafts an item with 10 proliferator points by concentrating the item's proliferator points on a portion of the item, breaking the upper limit of proliferator points.\n{"Any item will be accepted.".AddOrangeLabel()} Success rate is related to the input item proliferator points.",
            $"将物品的增产点数集中到一部分物品上，突破增产点数的上限，从而制作出10增产点数的物品。\n{"可接受任何物品。".AddOrangeLabel()}\n成功率与输入物品的增产点数有关。");

        Register("量子复制塔", "Increase Production Fractionator");
        Register("I量子复制塔",
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

        Register("T升转化", "Up-Downgrade Fractionation", "升转化");
        Register("升转化描述",
            "To facilitate Icarus's exploration, the Mastermind has issued downgraded tech for some items. Upgrading the Fractionation tech converts low-level items into fewer high-level items, and downgrading the Fractionation tech converts high-level items into multiple low-level items. There is an inexhaustible supply of items in the cycle of upgrading and downgrading. This tech is well adapted to buildings, less so to non-buildings, and only some non-building items can be fractionated. Nonetheless, it is a powerful aid on the quest.",
            "为了方便伊卡洛斯的探索，主脑下发了部分物品的升降级科技。升级分馏科技可以将低级物品转为更少的高级物品，转化科技可将高级物品转为多个低级物品，升降级的循环中存在着取之不尽的物品。这项科技对建筑的适配性良好，对非建筑的适配性则较差，只有部分非建筑物品可以进行升降级操作。尽管如此，它依然是探索路上的强力援助。");
        Register("升转化结果",
            "You have mastered the Up-Downgrade Fractionation technology and can now recycle process some items to copy them.",
            "你已经掌握了升转化技术，可以用升转化塔循环处理物品，从而实现物品的复制。");

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

        Register("T量子复制", "Increase production fractionate", "量子复制");
        Register("量子复制描述",
            "Although Natural Resource Fractionation and Up-Downgrade Fractionation are powerful, these techniques can only be used to process specific items. As research of dark fog continued to deepen, it seemed that the possibility existed of expanding this mode of replication to everything in the universe. It was found that if the effect of the item's proliferator points on the fractionation process changes from accelerate to increase, and the material reorganization technique was used to make the product the same as the input, it would be possible to achieve the effect of duplicating everything.\n"
            + "It is clear that the research process of correlating yield-enhancing effects with material reorganization is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
            + $"{"Warning:".AddOrangeLabel()} The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, {"Please research manually.".AddOrangeLabel()}",
            "自然资源分馏和升转化虽然强大，但这些技术只能用于处理特定物品。随着对黑雾研究的不断深入，似乎存在将这种复制模式扩展到宇宙万物的可能性。研究发现，如果物品的增产点数对分馏过程的影响从加速变为增产，并利用物质重组技术使产物与输入相同，就可以达到复制万物的效果。\n"
            + "显然，将增产效果与物质重组关联的研究过程高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
            + $"{"警告：".AddOrangeLabel()}该科技的相关技术已被COSMO技术伦理委员会禁用，{"请手动研究。".AddOrangeLabel()}");
        Register("量子复制结果",
            "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
            "你已经掌握了量子复制技术。现在，你真正拥有了无中生有的能力！");

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
            "It has been found that when multiple fractionators form a loop, there is often a buildup of product from one fractionator, which causes all fractionators to stop working. To solve this problem, the Mastermind provides technology that can control the fractionation process. Any time the number of products reaches half of the internal storage limit, the fractionator will not fractionate any products, but only maintain the flow of raw materials, thus ensuring the normal operation of the other fractionators in the loop.",
            "研究发现，多个分馏塔形成环路时，经常出现某个分馏塔产物堆积，从而导致所有分馏塔停止工作的情况。为了解决这个问题，主脑提供了可以控制分馏过程的科技。任何产物数目达到内部存储上限一半时，分馏塔将不会分馏出任何产物，仅维持原料的流动，以此确保环路其他分馏塔的正常运行。");
        Register("分馏永动结果",
            "Now, fractionators will keep running without product buildup.",
            "现在，分馏塔将会持续运行，不会出现产物堆积的情况了。");

        #endregion

    }
}
