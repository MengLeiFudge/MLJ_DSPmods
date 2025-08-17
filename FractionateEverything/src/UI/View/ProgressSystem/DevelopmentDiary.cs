using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressSystem;

public static class DevelopmentDiary {
    private static RectTransform window;
    private static RectTransform tab;

    private static Dictionary<string, int> programmingEvents;

    public static void AddTranslations() {
        Register("开发日记", "Development Diary");

        programmingEvents = new() {
            { "FE1.0", 3 },
            { "FE1.1", 5 }
        };

        Register("FE1.0-1",
            "",
            "2023年11月"
            + $"戴森球计划真好玩啊，十分甚至九分的好玩！\n"
            + $"加了Mod更好玩了！像是创世之书，简直变成另一个游戏了，好厉害！\n"
            + $"\n"
            + $"忽然，一个念头在脑海中一闪而过——\n"
            + $"我，能不能也写一个Mod？\n"
            + $"听起来不错，但我对编写戴森球计划的Mod这件事一窍不通，完全不知道从哪里入手。\n"
            + $"要不，去查查看有没有什么教程？");

        Register("FE1.0-2",
            "",
            "2023年11月"
            + $"我还真找到了一些相关的教程，比如：\n"
            + $"宵夜大佬的“【戴森球计划】LDBTool Mod教程”视频，讲了LDBTool的用法；\n"
            + $"宵夜大佬的“【游戏Mod开发教程】”视频，讲了Unity游戏Mod的常用工具——BepInex和Harmony；\n"
            + $"宵夜大佬的“Unity游戏Mod/插件制作教程”文集，详细讲了Harmony的各种功能；\n"
            + $"宵夜大佬的……\n"
            + $"……\n"
            + $"等等，这是不是有点不太对？\n"
            + $"不是哥们，人类进化怎么没带上我啊？\n"
            + $"宵夜大佬（xiaoye97）就是LDBTool的作者？？？而且他还写了好多其他Unity游戏的Mod？？？\n"
            + $"我也是97年的，为什么我们一样大，但是我跟个废物一样啥也不会啊？呜呜呜");

        Register("FE1.0-3",
            "",
            "2023年11月"
            + $"不管怎么说，我还是含泪看完了宵夜大佬的Mod教学。\n"
            + $"此时，我的内心仿佛有两个小人在打架：\n"
            + $"其中一个说：“这看起来还行啊，也不难！这不搞一个？”\n"
            + $"另一个说：“对呀对呀！”\n"
            + $"就这样，我踏上了这条不归路。\n"
            + $"\n"
            + $"{"这就是故事的开端，充满梦想的色彩。".WithColor(Orange)}");

        Register("FE1.0-4",
            "",
            "2023年12月"
            + $"虽说下定决心要写Mod，但是到底写什么呢？\n"
            + $"这段时间我也看了很多Mod，比如："
            + $"改一下矿机速度；"
            + $"改一下油井速度；"
            + $"改一下制作台速度；"
            + $"改一下电线杆范围；"
            + $"……"
            + $"呃，好吧，这好像有点不对。\n"
            + $"确实，这也是Mod。但是我知道，这样的Mod并不是我想要的。\n"
            + $"我，想写一个{"有趣".WithColor(Blue)}的Mod。");

        Register("FE1.0-5",
            "",
            "2023年12月"
            + $"{"分馏宇宙（Fractionate Universe）".WithColor(Orange)}……这是什么Mod？\n"
            + $"虽然这个Mod第一次见，但这个模组的作者Maerd我可是相当了解——"
            + $"没错，就是编写“更多巨构（More Mega Structure）”、维护“深空来敌（They Come From Void）”的大佬！\n"
            + $"说时迟那时快，我的手啪的一下，很快啊，直接就点开了这个模组的简介。\n"
            + $"\n"
            + $"电动机可以分馏出电磁涡轮？\n"
            + $"粒子容器可以分馏出奇异物质？\n"
            + $"石墨烯可以分馏出碳纳米管？\n"
            + $"而且，好像不只是材料，建筑也可以分馏！\n"
            + $"低速传送带可以分馏出高速传送带？\n"
            + $"电弧熔炉可以分馏出位面熔炉？\n"
            + $"这……这也太{"有趣".WithColor(Blue)}了叭！！！\n"
            + $"\n"
            + $"不得不说，在看到这个Mod的时候，我就被它深深地吸引了。\n"
            + $"这还等什么？我迫不及待地查看这个Mod的源码。");

        Register("FE1.0-6",
            "",
            "2023年12月"
            + $"哇，一个654行的方法AddFracRecipes()，添加了所有的分馏配方？\n"
            + $"18行代码一个配方，都是复制粘贴之后一个个手动改的ID、描述。\n"
            + $"这工作量也太大了叭！0.0\n"
            + $"\n"
            + $"看完之后，我仔细想了想，它应该有很大的优化空间。"
            + $"比如添加一些新的分馏配方，修改一下图标的样式……\n"
            + $"如果我把它改造一下……嘿嘿嘿。\n"
            + $"不过，新项目叫什么名字呢？肯定不能跟之前的一样。\n"
            + $"嗯——有了！不如，就叫它{"万物分馏（Fractionate Everything）".WithColor(Orange)}吧！");

        Register("FE1.0-6",
            "",
            "2023年12月"
            + $"我新建了一个项目\n"
            + $"每个配方都是十多行代码，看起来是粘贴，然后一个个改的啊！\n"
            + $"没有用一个通用的方法包装一下，这工作量也太大了吧0.0\n"
            + $"嗯，代码看起来有很大的优化空间啊！\n"
            + $"那么——万物分馏（Fractionate Everything），启动！");

        Register("FE1.0-7",
            "",
            "2023年12月26日，周二"
            + $"有一个好消息和一个坏消息。\n"
            + $"坏消息是，我昨天一个苹果都没收到。\n"
            + $"好消息是，万物分馏1.0版本已经找亲（mian）爱（fei）的（xiao）群（bai）友（shu）测试过了。\n"
            + $"那么，是时候了——\n"
            + $"{"万物分馏1.0，发布！".WithColor(Orange)}\n"
            + $"没错，我萌泪之所以能有今天的成就，全靠我自己的勤奋刻苦……Ctrl+C，Ctrl+V！");

        Register("FE1.0-8",
            "",
            "2024年3月1日"
            + $"时间过的好快啊，转眼间两个月就过去了。\n"
            + $"前些日子我把代码发布在了Github，这样我就能看到外国的小伙伴提出的意见了！\n"
            + $"让我看看他们在Issue里面说了些什么——\n"
            + $"\n"
            + $"看不到图标？呃，好吧，我得承认这是我的问题。\n"
            + $"我之前重构了代码，用一个通用的方法添加所有配方。但是图标我不会制作啊！所以就没管这部分的内容。\n"
            + $"嗯，这个确实比较紧急，那就先用产物图标代替一下，起码能看到再说。\n"
            + $"\n"
            + $"分馏图标错位，与其他Mod冲突？\n"
            + $"这个好像有点麻烦啊。"
            + $"没想到，真的有人反馈了一些Bug，还有一些改进建议。\n"
            + $"有的人说看不到图标"
            + $"在这段日子里，我的Github居然有外国友人 "
            + $"戴森球前几天添加了集装分拣器，瞬时堆叠，好强啊！\n"
            + $"我也得跟上官方的脚步，赶快为这个物品添加对应的升级配方。\n"
            + $"让我看看……好像还有干扰胶囊、压制胶囊、近程电浆塔，也一起加了吧！\n"
            + $"v1.0.1，发布！");

        Register("FE1.1-1",
            "",
            "2024年3月"
            + $"我感觉万物分馏还有挺大的优化空间。\n"
            + $"比如说，现在所有分馏配方都在开局直接解锁。要不改一下，改成随着科技逐渐解锁！\n"
            + $"再比如说\n"
            + $"v1.0.1，发布！");


        Register("IK-1",
            "",
            $"今天是开荒新星系的第一天。\n"
            + $"嗯，环境看起来跟之前也没什么区别，就跟我居住的星球差不多。\n"
            + $"尤其是降落点附近的6铁6铜，感觉就像是主脑故意要降落在这里……\n"
            + $"算了，完成任务要紧，希望主脑没听到我刚才的吐槽。\n"
            + $"不过说起来，虽然我已经开荒了挺多星系，伊卡洛斯也控制的很熟练了，还是能感受到这个机甲的强力。\n"
            + $"说起来，我又想吐槽一下主脑了，明明开辟新星域也很危险的，为什么不直接把机甲参数拉满？\n"
            + $"还非要上传数据才能解封机甲的性能……真是辛苦我自己了。\n"
            + $""
            + $"比如说，现在所有分馏配方都在开局直接解锁。要不改一下，改成随着科技逐渐解锁！\n"
            + $"再比如说\n"
            + $"v1.0.1，发布！");


        Register("141标题", "Fractionate Everything 1.4.1 Update", "万物分馏1.4.1版本更新");
        Register("141信息",
            "Even though Fractionate Everything is part of the cheat mods, it has enough restrictions that it should be balanced.\n"
            + "—until I try it out for myself and get through the game in one night, that's what I'm thinking.\n"
            + "After my experience with the Fractionate Everything mod, the \"planning doesn't even play games\" joke hit me like a boomerang.\n\n"
            + $"The idea of Fractionate Everything is great, but it's fatal flaw is {"fractionation skips a lot of yield lines".WithColor(Red)} (especially with matrix fractionation).\n"
            + "Without using proliferators, 3% destroy probability means you only get 25% of the product, like 10,000 blue matrix => 2,500 red matrix => 625 yellow matrix => ......\n"
            + "Imagine when players realize that \"there is a huge loss of blue matrix to green matrix\", will they start building yellow or purple matrix production lines as I envisioned?\n"
            + "The answer is not. Production enhancers reduce damage and new matrices are overly complex, so players tend to expand the production of lower level matrices and then fractionate them.\n"
            + "In the end there are only three things to do in the game: expand the blue or red matrix production line, unlock tech, and wait.\n"
            + "Oh my god I bet there's no worse gaming experience than this!\n"
            + "It's hard to believe that the matrix fractionation chain is so poorly designed that it looks like Aunt Susan's apple pie next door!\n\n"
            + $"Back on topic, this mod was originally designed to be {"fun".WithColor(Blue)}, but I've been working on making it a {"balanced".WithColor(Blue)} mod.\n"
            + "It is an obvious fact that if you want to cheat, why not try \"Multfunction_Mod\" or \"CheatEnabler\"?\n"
            + "In response to matrix fractionation, I solicited ideas for improvements from a large number of players. I gathered various ways to change things, such as lag unlocking, fractionation consuming sand,\n"
            + "increasing power consumption, fractionated matrices not being able to be fractionated again, and so on, but they weren't in the way I expected.\n\n"
            + $"However, I figured out one thing: {"Fractionation should provide convenience, but not skip game progression.".WithColor(Orange)}\n"
            + "Obviously, the Building-HighSpeed Fractionator is the best building. It's more of an aid and doesn't affect the game experience too much.\n"
            + "So in 1.4.1, I added switches for matrix fractionation, split the Building-HighSpeed Fractionator into Upgrade and Downgrade Fractionator and adjusted the fractionate recipes.\n"
            + "Hopefully these changes will make the mod more balanced.\n\n"
            + $"PS1: You can click {"Update Log".WithColor(Blue)} for all the changes in 1.4.1.\n"
            + $"PS2: Don't forget to check out {"the new settings".WithColor(Blue)} added in {"Settings - Miscellaneous".WithColor(Blue)}!\n"
            + $"PS3: To celebrate this update, some {"Blueprints".WithColor(Blue)} for Fractionate Everything have been added to the Blueprints library. \n"
            + $"Thanks to everyone for using Fractionate Everything. {"Have fun with fractionation!".WithColor(Orange)}",
            "“尽管万物分馏属于作弊模组，但它的限制已经够多了，它应该是平衡的。”\n"
            + "——在我亲自试玩并一个晚上通关游戏之前，我都是这样想的。\n"
            + "在我体验万物分馏mod之后，“策划根本就不玩游戏”这个玩笑就像回旋镖一样打在了我的身上。\n\n"
            + $"万物分馏的想法是很棒的，但它的致命缺陷在于{"分馏会跳过大量产线".WithColor(Red)}（尤其是矩阵分馏）。\n"
            + "不使用增产剂的情况下，3%的损毁概率意味着你只能获得25%的产物，也就是10000蓝糖=>2500红糖=>625黄糖=>……\n"
            + "试想一下，当玩家意识到“蓝糖分馏为绿糖会有巨大损耗”，他们会按照我的预想开始构建黄糖或紫糖产线吗？\n"
            + "答案是不会。增产剂可以降低损毁，新矩阵又过于复杂，所以玩家倾向于扩大低级矩阵的产量，然后分馏它们。\n"
            + "最后游戏只剩下三件事：扩大蓝糖或红糖的产线，解锁科技，以及等待。\n"
            + "哦天哪，我敢打赌，没有比这更糟糕的游戏体验了！\n"
            + "真是难以相信，矩阵分馏链的设计竟然如此糟糕，就像隔壁苏珊婶婶做的苹果派一样！\n\n"
            + $"回归正题，分馏设计之初是为了{"有趣".WithColor(Blue)}，但是我一直致力于把它打造成一个{"平衡".WithColor(Blue)}的mod。\n"
            + "一个显而易见的事实是，如果你想作弊，为何不试试“Multfunction_Mod”或者“CheatEnabler”？\n"
            + "针对矩阵分馏，我向大量玩家征集了改进意见。我收集到了各种改动方式，例如滞后解锁、分馏消耗沙土、加大耗电、\n"
            + "分馏出的矩阵不能再次分馏等等，但它们都不是我所期望的方式。\n\n"
            + $"不过，我想明白了一件事情：{"分馏应该提供便利，但是不能跳过游戏进程。".WithColor(Orange)}\n"
            + "显然，建筑极速分馏塔是最优秀的建筑。它更像是一种辅助手段，并不会过多影响游戏体验。\n"
            + "所以在1.4.1中，我为矩阵分馏增加了开关，将建筑极速分馏塔拆分为升级、转化塔并调整了分馏配方。\n"
            + "希望这次改动能让分馏更加平衡。\n\n"
            + $"PS1：你可以点击{"更新日志".WithColor(Blue)}，以了解1.4.1的所有改动。\n"
            + $"PS2：千万不要忘记查看{"设置-杂项".WithColor(Blue)}中{"新增的设置项".WithColor(Blue)}！\n"
            + $"PS3：为了庆祝本次更新，一些万物分馏的{"蓝图".WithColor(Blue)}已添加至蓝图库。\n"
            + $"感谢万物分馏的每一位玩家。{"尽情享受分馏的乐趣吧！".WithColor(Orange)}");

        Register("142标题", "Fractionate Everything 1.4.2 Update", "万物分馏1.4.2版本更新");
        Register("142信息",
            "Haven't seen you for a long time, and I miss you very much. It's been another month and a half after last update. How are you?\n\n"
            + $"Remember in version 1.4.1, I said I had a couple {"Blueprints".WithColor(Blue)} for you guys? That was indeed true, and it wasn't an April Fool's joke. \n"
            + $"-- Just {"R2".WithColor(Blue)} took my blueprints folder {"deleted".WithColor(Red)}!Σ(っ\u00b0Д\u00b0;)っ\n"
            + "As for why I didn't update the version after discovering this problem that day, it's because the folder is gone but the blueprints are still there...(O_o)??\n"
            + "Yes it's strange but the folder is gone and the blueprints are out there!\n"
            + $"{"By the way, it's definitely NOT I'm lazy to update!".WithColor(Orange)}\n"
            + "I'm curious if anyone has actually gone through the folder where the mod is and found those blueprints. (If so be sure to let me know www)\n"
            + "Of course, this is a small problem for me. Since the files are unusable, I'll just shove them right inside the code!\n"
            + "Shakespeare once said: there's nothing that can't be solved by one string. If there is, then there are four! (three blueprints + intro)\n"
            + $"If all goes well, you should see the blueprints this time. Be sure to {"recheck the blueprint library!".WithColor(Blue)}\n\n"
            + "In addition, this update fixes an issue with the settings page reporting errors.\n\n"
            + "The main reason there hasn't been an update lately is the lack of inspiration, and I really can't think of anything else to optimize.\n"
            + "I'm sure you can see that the idea of using fractionation as the core actually greatly limits the functionality of the mod.\n"
            + $"However, after talking with the group the other day, I've determined the general direction of the MOD afterward - that is {"Draw".WithColor(Orange)}.\n"
            + "\"Big company with a billion dollar market cap will only make card draw games, but a team of only five people can make Dyson Sphere Program\", I'm sure you've all heard of it.\n"
            + $"{"But why couldn't the Dyson Sphere Program be a card draw game, right?".WithColor(Orange)} I'm coming now!\n\n"
            + "The next update will be centered around randomness and card draw. The following information can be revealed:\n"
            + "1.There will be new fractionators for getting currency dedicated to card draw.\n"
            + "2.Fractionation recipes can be obtained through technology, raffle, and redemption.\n"
            + "3.The same fractionation recipe has different qualities, the higher the quality the harder it is to obtain.\n"
            + "4.I hope to complete the initial version before the end of September. Welcome to join the group to experience the latest beta version and give your opinion!\n\n"
            + $"And finally, thanks for your support! {"Have fun with fractionation!".WithColor(Orange)}",
            "许久不见，甚是想念。时间过得真快啊，转眼又是一个半月。大家过的怎么样啊？\n\n"
            + $"还记得在1.4.1版本中，我说过为你们准备了几个{"蓝图".WithColor(Blue)}吗？那确实是真的，它并不是一个愚人节玩笑。\n"
            + $"——只不过{"R2".WithColor(Blue)}将我的蓝图文件夹{"删掉了".WithColor(Red)}！Σ(っ\u00b0Д\u00b0;)っ\n"
            + "至于我为什么当天发现这个问题之后，却并没有更新版本，是因为文件夹没了但是蓝图还在……(O_o)??\n"
            + "事实正是如此，文件夹没了，蓝图跑外面了！\n"
            + $"{"顺带一提，绝对不是因为我懒才不更新的！".WithColor(Orange)}）\n"
            + "我很好奇到底有没有人去翻翻MOD所在的文件夹，把那几个蓝图找出来。（如果有的话务必告诉我哈哈）\n"
            + "当然，这点小小的问题是难不倒我的。既然文件无法使用，那我就把它们直接塞到代码里面！\n"
            + "鲁迅曾经说过：没有什么是一个字符串解决不了的。如果有，那就四个！（三个蓝图+简介）\n"
            + $"如果一切顺利的话，这次应该能看到蓝图了。请务必{"重新检查一下蓝图库！".WithColor(Blue)}\n\n"
            + "除此之外，此次更新修复了设置页面报错的问题。\n\n"
            + "近期一直没有更新的主要原因是缺失灵感，我确实想不出有什么可以优化的地方了。\n"
            + "想必大家也能看出来，以分馏作为核心的思路其实大大限制了MOD的功能。\n"
            + $"不过，前几天与群友交流之后，我确定了MOD之后的大致方向——那就是{"抽奖".WithColor(Orange)}。\n"
            + $"“百亿大厂十连抽，五人团队戴森球”，想必大家都听过。嘿嘿嘿，{"谁说戴森球不能十连抽？".WithColor(Orange)}我踏马莱纳！\n\n"
            + "接下来的更新将主要以“随机性与抽奖”作为核心。可以透露的信息如下：\n"
            + "1.会有新的分馏塔，用于获取专用于抽奖的货币。\n"
            + "2.分馏配方可通过科技、抽奖、兑换等方式获取。\n"
            + "3.同一个分馏配方有不同品质，越高品质越难获取。\n"
            + "4.希望能在9月底之前完成初版。欢迎加群体验最新测试版，并提出你的看法！\n\n"
            + $"一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".WithColor(Orange)}");

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
            + $"The above is all that's included in this update. As always, thank you for your support! {"Have fun with fractionation!".WithColor(Orange)}",
            "这次是一个小更新，修复了原版分馏塔无法将氢分馏为重氢的问题。\n"
            + "感谢starfi5h大佬对此bug出现原因的探究（我确实没有复现此bug，所以一直无法修复）。\n\n"
            + "打个广告：如果你想量化产线，推荐使用网页版量化计算器【https://dsp-calc.pro/】\n\n"
            + "1.5.0版本的抽奖还在制作中，比我预想的要麻烦的多。\n"
            + "目前设计部分基本完工，正处于编写代码阶段，不过UI方面可能是个难题。\n"
            + "在新的版本中，将会增加一个与垃圾回收分馏塔类似的分馏塔，用于解锁分馏配方、向商店提供货币。\n"
            + "配方具有“星级”与“稀有度”这两个属性，分别表示配方的效率与稀有程度。\n"
            + "除了固定的分馏配方，还有可自定义输出的自选分馏配方，可以在前期将物品随意重组！\n"
            + $"以上就是本次更新的全部内容。一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".WithColor(Orange)}");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "开发日记");
        float x = 0f;
        float y = 20f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
