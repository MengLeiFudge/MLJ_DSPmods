using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.Archive;

public static class DevelopmentDiary {
    private readonly struct DiaryEntry(string label, string titleKey, string contentKey) {
        public readonly string Label = label;
        public readonly string TitleKey = titleKey;
        public readonly string ContentKey = contentKey;
    }

    private static Dictionary<string, int> programmingEvents;
    private static RectTransform window;
    private static RectTransform tab;
    private static MyComboBox entryCombo;
    private static Text txtDiaryContent;
    private static List<DiaryEntry> diaryEntries = [];
    private static int currentEntryIndex;

    public static void AddTranslations() {
        Register("开发日记", "Development Diary");
        Register("IK", "Icarus's Diary", "伊卡洛斯手记");

        programmingEvents = new() {
            { "FE1.0", 9 },
            { "FE1.1", 9 },
            { "FE2.0", 5 },
            { "FE2.1", 4 },
            { "FE2.2", 5 },
            { "FE2.3", 5 },
            { "IK", 20 }
        };

        Register("FE1.0-1",
            "",
            """
            2023年11月
            戴森球计划真好玩啊，十分甚至九分的好玩！
            加了Mod更好玩了！像是创世之书，简直变成另一个游戏了，好厉害！

            忽然，一个念头在脑海中一闪而过——
            我，能不能也写一个Mod？
            听起来不错，但我对编写戴森球计划的Mod这件事一窍不通，完全不知道从哪里入手。
            要不，去查查看有没有什么教程？
            """);

        Register("FE1.0-2",
            "",
            """
            2023年11月
            我还真找到了一些相关的教程，比如：
            宵夜大佬的“【戴森球计划】LDBTool Mod教程”视频，讲了LDBTool的用法；
            宵夜大佬的“【游戏Mod开发教程】”视频，讲了Unity游戏Mod的常用工具——BepInex和Harmony；
            宵夜大佬的“Unity游戏Mod/插件制作教程”文集，详细讲了Harmony的各种功能；
            宵夜大佬的……
            ……
            等等，这是不是有点不太对？
            不是哥们，人类进化怎么没带上我啊？
            宵夜大佬（xiaoye97）就是LDBTool的作者？？？而且他还写了好多其他Unity游戏的Mod？？？
            我也是97年的，为什么我们一样大，但是我跟个废物一样啥也不会啊？呜呜呜
            """);

        Register("FE1.0-3",
            "",
            $"""
            2023年11月
            不管怎么说，我还是含泪看完了宵夜大佬的Mod教学。
            此时，我的内心仿佛有两个小人在打架：
            其中一个说：“这看起来还行啊，也不难！这不搞一个？”
            另一个说：“对呀对呀！”
            就这样，我踏上了这条不归路。

            {"这就是故事的开端，充满梦想的色彩。".WithColor(Orange)}
            """);

        Register("FE1.0-4",
            "",
            $"""
            2023年12月
            虽说下定决心要写Mod，但是到底写什么呢？
            这段时间我也看了很多Mod，比如：
            改一下矿机速度；
            改一下油井速度；
            改一下制作台速度；
            改一下电线杆范围；
            ……
            呃，好吧，这好像有点不对。
            确实，这也是Mod。但是我知道，这样的Mod并不是我想要的。
            我，想写一个{"有趣".WithColor(Blue)}的Mod。
            """);

        Register("FE1.0-5",
            "",
            $"""
            2023年12月
            {"分馏宇宙（Fractionate Universe）".WithColor(Orange)}……这是什么Mod？
            虽然这个Mod第一次见，但这个模组的作者Maerd我可是相当了解——没错，就是编写“更多巨构（More Mega Structure）”、维护“深空来敌（They Come From Void）”的大佬！
            说时迟那时快，我的手啪的一下，很快啊，直接就点开了这个模组的简介。

            电动机可以分馏出电磁涡轮？
            粒子容器可以分馏出奇异物质？
            石墨烯可以分馏出碳纳米管？
            而且，好像不只是材料，建筑也可以分馏！
            低速传送带可以分馏出高速传送带？
            电弧熔炉可以分馏出位面熔炉？
            这……这也太{"有趣".WithColor(Blue)}了叭！！！

            不得不说，在看到这个Mod的时候，我就被它深深地吸引了。
            这还等什么？我迫不及待地查看这个Mod的源码。
            """);

        Register("FE1.0-6",
            "",
            $"""
            2023年12月
            哇，一个654行的方法AddFracRecipes()，添加了所有的分馏配方？
            18行代码一个配方，都是复制粘贴之后一个个手动改的ID、描述。
            这工作量也太大了叭！0.0

            看完之后，我仔细想了想，它应该有很大的优化空间。
            比如添加一些新的分馏配方，修改一下图标的样式……
            如果我把它改造一下……嘿嘿嘿。
            不过，新项目叫什么名字呢？肯定不能跟之前的一样。
            嗯——有了！不如，就叫它{"万物分馏（Fractionate Everything）".WithColor(Orange)}吧！
            """);

        Register("FE1.0-7",
            "",
            """
            2023年12月
            我新建了一个项目
            每个配方都是十多行代码，看起来是粘贴，然后一个个改的啊！
            没有用一个通用的方法包装一下，这工作量也太大了吧0.0
            嗯，代码看起来有很大的优化空间啊！
            那么——万物分馏（Fractionate Everything），启动！
            """);

        Register("FE1.0-8",
            "",
            $"""
            2023年12月26日，周二
            有一个好消息和一个坏消息。
            坏消息是，我昨天一个苹果都没收到。
            好消息是，万物分馏1.0版本已经找亲（mian）爱（fei）的（xiao）群（bai）友（shu）测试过了。
            那么，是时候了——
            {"万物分馏1.0，发布！".WithColor(Orange)}
            没错，我萌泪之所以能有今天的成就，全靠我自己的勤奋刻苦……Ctrl+C，Ctrl+V！
            """);

        Register("FE1.0-9",
            "",
            """
            2024年3月1日
            时间过的好快啊，转眼间两个月就过去了。
            前些日子我把代码发布在了Github，这样我就能看到外国的小伙伴提出的意见了！
            让我看看他们在Issue里面说了些什么——

            看不到图标？呃，好吧，我得承认这是我的问题。
            我之前重构了代码，用一个通用的方法添加所有配方。但是图标我不会制作啊！所以就没管这部分的内容。
            嗯，这个确实比较紧急，那就先用产物图标代替一下，起码能看到再说。

            分馏图标错位，与其他Mod冲突？
            这个好像有点麻烦啊。没想到，真的有人反馈了一些Bug，还有一些改进建议。
            有的人说看不到图标，在这段日子里，我的Github居然有外国友人……
            戴森球前几天添加了集装分拣器，瞬时堆叠，好强啊！
            我也得跟上官方的脚步，赶快为这个物品添加对应的升级配方。
            让我看看……好像还有干扰胶囊、压制胶囊、近程电浆塔，也一起加了吧！
            v1.0.1，发布！
            """);

        Register("FE1.1-1",
            "",
            """
            2024年3月
            我感觉万物分馏还有挺大的优化空间。
            比如说，现在所有分馏配方都在开局直接解锁。要不改一下，改成随着科技逐渐解锁！
            再比如说……
            v1.0.1，发布！
            """);

        Register("FE1.1-2",
            "",
            """
            2024年3月中旬
            万物分馏1.1版本正式发布！
            我终于实装了科技树系统，现在分馏配方需要研究科技才能慢慢解锁了。
            虽然这意味着玩家不能开局直接起飞，但这种循序渐进的感觉才更像是在玩游戏嘛。
            顺便修复了一些UI显示的Bug，感觉离“好用的Mod”又近了一步。
            """);

        Register("FE1.1-3",
            "",
            """
            2024年4月
            1.2版本，我终于受不了那些简陋的占位图标了。
            我花了不少时间整理了一套专属的图标资源，虽然我不是专业的画师，但起码现在分馏塔里的物品看起来像那么回事了。
            看着五颜六色的图标在传送带上飞驰，这种视觉上的提升真的让人心旷神怡。
            """);

        Register("FE1.1-4",
            "",
            """
            2024年5月
            1.3版本，我们迎来了大规模的联动。
            适配了“创世之书”、“更多巨构”这些重量级模组。
            看到万物分馏能和这些优秀的作品共存，甚至互相配合，这种成就感是无法用言语表达的。
            也要感谢这些模组的作者给予我的各种帮助！
            """);

        Register("FE1.1-5",
            "",
            $"""
            2024年6月
            {"万物分馏1.4版本".WithColor(Orange)}，这绝对是一个里程碑式的更新。
            我引入了{"垃圾回收分馏塔".WithColor(Blue)}，这玩意儿能把任何东西变成沙土，简直是强迫症工程师的福音！
            同时，我为所有物品添加了1%的损毁概率。没错，分馏不再是无代价的了。
            还有产物输出集装科技，看着一叠叠物品从塔里吐出来，效率提升的感觉真的太爽了！
            """);

        Register("FE1.1-6",
            "",
            $"""
            2024年6月下旬
            这就是那个出名的{"“回旋镖”".WithColor(Red)}版本——1.4.1。
            我亲自试玩了一整晚，结果发现自己设计的矩阵分馏链简直是个灾难。
            玩家们并没有像我预想的那样去构建复杂的产线，而是疯狂堆叠低级矩阵。
            “策划根本就不玩游戏”这个玩笑，这次真的打在了我自己的脸上。
            我意识到：{"分馏应该提供便利，但不能跳过游戏进程。".WithColor(Orange)}
            """);

        Register("FE1.1-7",
            "",
            $"""
            2024年7月
            为了解决1.4.1发现的问题，我大刀阔斧地改动了分馏塔。
            我把极速分馏塔拆分成了{"升级塔".WithColor(Blue)}和{"转化塔".WithColor(Blue)}。
            矩阵分馏链和燃料棒分馏链默认关闭了，只有真正需要的玩家才去手动开启。
            平衡性调整总是痛苦的，但为了长远的游戏体验，这些改动是必须的。
            """);

        Register("FE1.1-8",
            "",
            $"""
            2024年7月
            1.4.2版本，发生了一件让人哭笑不得的事。
            我辛辛苦苦准备的蓝图文件夹，居然被{"R2modman".WithColor(Red)}给删掉了！Σ(っ°Д°;)っ
            文件夹没了，蓝图跑外面去了。为了解决这个问题，我决定祭出大招：
            {"“没有什么是一个字符串解决不了的。如果有，那就四个！”".WithColor(Orange)}
            我直接把蓝图数据塞进了代码里。鲁迅（我瞎编的）诚不我欺！
            """);

        Register("FE1.1-9",
            "",
            $"""
            2024年8月
            1.4.3版本，一个小而美的更新。
            感谢{"Starfi5h".WithColor(Blue)}大佬，帮我找出了原版分馏塔无法处理氢气的陈年Bug。
            这时候我已经在秘密筹划2.0版本了。
            抽奖、品质、养成……一个庞大的计划正在我脑海中缓缓铺开。
            """);

        Register("FE2.0-1",
            "",
            """
            2024年7月
            在1.4.5发布后，我休息了一段时间。
            我在思考，万物分馏的未来在哪？如果只是加更多的配方，那也太无聊了。
            直到有一天，我看到了“百亿大厂十连抽”这句话……
            对啊！谁说戴森球不能十连抽？谁说分馏塔不能出橙装？
            于是，一个疯狂的计划诞生了——万物分馏2.0，抽奖时代！
            """);

        Register("FE2.0-2",
            "",
            """
            2024年8月
            重构，又是重构！
            为了支撑庞大的抽奖和养成系统，我把项目改成了SDK模式，并且引入了soarqin大佬的UI库。
            界面从原本简单的对话框变成了专业的全屏面板。
            虽然写的我天昏地暗，但看着精致的界面一点点成型，我知道这次转型是必须的。
            """);

        Register("FE2.0-3",
            "",
            """
            2024年9月
            交互塔、复制塔、点数聚集塔……
            分馏塔家族迎来了大爆发。我设计了全新的“物品价值”体系，确保抽奖产出符合能量守恒（大雾）。
            看着各种精华、回响、核心、芯片这些新名词出现在代码里，我感觉自己像是在做一个全新的RPG游戏。
            """);

        Register("FE2.0-4",
            "",
            """
            2024年9月20日
            万物分馏2.0正式发布！
            “抽奖系统”、“品质突破”、“建筑强化”……这不仅是版本号的跳跃，更是玩法上的彻底重塑。
            看着群友们疯狂抽卡，我知道这个“抽奖宇宙”算是成功开启了。
            虽然初期Bug多得满地爬，但“小白鼠”们（笑）的热情真的让我非常感动。
            """);

        Register("FE2.0-5",
            "",
            """
            2024年10月
            自动百连抽！
            玩家们的“肝”程度超出了我的想象，手动抽卡居然被嫌慢。
            于是我马不停蹄地肝出了自动百连功能，甚至让它能在面板关闭时自动运行。
            这就是科技的力量，这就是分馏的魅力！
            """);

        Register("FE2.1-1",
            "",
            """
            2024年11月
            2.1版本，我把目光投向了“交互”。
            数据中心存了几十亿的物资，却还要手动拿出来用？这也太不先进了。
            于是我重写了逻辑，让建造、合成、甚至自动填充都可以直接从数据中心扣除物品。
            数据中心，正式成为玩家的“无限口袋”。
            """);

        Register("FE2.1-2",
            "",
            """
            2024年12月
            物流交互站实装！
            这可能是万物分馏历史上最具革命性的建筑之一。
            不用传送带，不用物流船，只要有电，物资就能在交互站和数据中心之间瞬间转移。
            真正的全星系物资共享，在这个版本成为了现实。
            """);

        Register("FE2.1-3",
            "",
            """
            2025年1月
            建筑强化系统，+20！
            之前的建筑升级只是基础，现在的强化才是灵魂。
            通过持续成长与强化，玩家可以将塔的性能推向极致。
            分馏永动、全堆叠输出……数值的快乐，往往就是这么简单纯粹。
            """);

        Register("FE2.1-4",
            "",
            """
            2025年2月
            VIP系统和保底机制上线。
            虽然经常调侃自己是“氪金手游”，但保底还是得给的嘛。
            VIP等级越高，福利越好，限时商店的折扣也就越大。
            万物分馏，现在越来越像一个完整的游戏系统了。
            """);

        Register("FE2.2-1",
            "",
            $"""
            2025年3月
            2.2版本，我决定给抽奖系统做一次大手术。
            我把那个巨大的奖池拆分成了{"配方、原胚、材料、建筑".WithColor(Blue)}四个独立的奖池。
            遵循{"等价交换".WithColor(Orange)}原则，不再是完全随机，而是让每一次抽奖都物有所值。
            同时，物流交互站的逻辑也进行了深度重构，支持了上传下载阈值的自定义。
            """);

        Register("FE2.2-2",
            "",
            """
            2025年3月中旬
            适配，无休止的适配！
            为了让万物分馏在联机Mod（Nebula）下也能稳定运行，我花了不少精力同步数据。
            看着两个小伙伴在不同的星球上同时从数据中心提取物资，那种跨越空间的协作感真的很棒。
            星环（Orbital Ring）的适配也终于完成了，分馏塔现在能处理更多奇奇怪怪的能源了。
            """);

        Register("FE2.2-3",
            "",
            """
            2025年4月
            QoL（质量生活）细节打磨。
            我增加了G键指引，希望新玩家不再对着复杂的面板发愁。
            量子复制科技也不再是隐藏科技，这种强力功能理应让更多人享受到。
            我还优化了背包双击存入的逻辑，工程师的背包应该永远保持整洁！
            """);

        Register("FE2.2-4",
            "",
            """
            2025年5月
            针对转化配方的概率，我做了一次平衡。
            现在降级、同级、升级的概率各占1/3，这让转化过程变得更可控。
            同时大幅提升了配方经验的获取速度。
            玩家反馈说之前的“肝度”有点太高了，我听进去了。
            """);

        Register("FE2.2-5",
            "",
            """
            2026年3月
            在2.2版本的末期，我开始反思。
            现在的系统是不是有点太臃肿了？130多个配方，繁琐的突破流程……
            虽然深度够了，但爽快感似乎被稀释了。
            我决定，在下一个大版本里，做一次真正的“减负”。
            """);

        Register("FE2.3-1",
            "",
            $"""
            2026年3月28日
            {"万物分馏2.3版本：减负与回归".WithColor(Orange)}。
            我移除了点金塔、分解塔、量子复制塔，它们的功能虽然酷炫，但确实让流程变得太繁杂了。
            配方的品质系统也被我砍掉了，取而代之的是更纯粹的{"回响与等级系统".WithColor(Blue)}。
            回响上限从5直接拉到40！
            """);

        Register("FE2.3-2",
            "",
            """
            2026年3月
            配方的上限被推向了新的高度。
            等级加上回响超过51级时，强度直接达到300%！
            这意味着原来的一个产出，现在能变成三个。
            分馏的生产力不再是渐进的，而是爆发式的增长。
            这种数值上的碾压，才符合“分馏宇宙”的终极幻想。
            """);

        Register("FE2.3-3",
            "",
            $"""
            2026年3月
            {"符文系统".WithColor(Purple)}正式登场。
            虽然目前还只是一个雏形，只能抽卡、升级、分解，加成还没实装。
            但它预示着未来的分馏将不再局限于固定的产线，而是有了更多的个性化组合。
            我为符文系统添加了一键分解和排序功能，一定要保证操作的丝滑。
            """);

        Register("FE2.3-4",
            "",
            """
            2026年3月下旬
            交互站系统的再细化。
            我把物流交互站拆分成了行星交互站和星际交互站，并且分别设置了科技解锁。
            虽然看似变复杂了，但其实是让玩家在不同阶段有更清晰的目标。
            数据中心的翻页设计也实装了，存再多东西也不怕翻不到头。
            """);

        Register("FE2.3-5",
            "",
            """
            2026年3月底
            2.3版本的两个月里，我一直在调整平衡性。
            130多个配方确实有点多，我正在考虑如何精简它们。
            同时加入了AutoSorter联动开关，让自动化更进一步。
            分馏的旅程还在继续，下一个惊喜会在哪呢？
            也许就在下一颗分馏出的重氢里。
            """);

        Register("FE1.1-3",
            "",
            """
            2024年4月
            1.2版本，我终于受不了那些简陋的占位图标了。
            我花了不少时间整理了一套专属的图标资源，虽然我不是专业的画师，但起码现在分馏塔里的物品看起来像那么回事了。
            看着五颜六色的图标在传送带上飞驰，这种视觉上的提升真的让人心旷神怡。
            """);

        Register("FE1.1-4",
            "",
            """
            2024年5月
            1.3版本，我们迎来了大规模的联动。
            适配了“创世之书”、“更多巨构”这些重量级模组。
            看到万物分馏能和这些优秀的作品共存，甚至互相配合，这种成就感是无法用言语表达的。
            也要感谢这些模组的作者给予我的各种帮助！
            """);

        Register("FE1.1-5",
            "",
            """
            2024年6月
            1.4版本，我开始认真反思“平衡性”这个问题。
            有玩家反馈说分馏太强了，甚至跳过了不少游戏环节。
            我想，是时候对矩阵分馏链和一些强力建筑进行大改了。
            这也导致了后面1.4.1那个充满“回旋镖”感的更新。
            """);

        Register("FE2.0-1",
            "",
            """
            2024年7月
            在1.4.5发布后，我休息了一段时间。
            我在思考，万物分馏的未来在哪？如果只是加更多的配方，那也太无聊了。
            直到有一天，我看到了“百亿大厂十连抽”这句话……
            对啊！谁说戴森球不能十连抽？谁说分馏塔不能出橙装？
            于是，一个疯狂的计划诞生了——万物分馏2.0，抽奖时代！
            """);

        Register("FE2.0-2",
            "",
            """
            2024年8月
            重构，又是重构！
            为了支撑庞大的抽奖和养成系统，我把项目改成了SDK模式，并且引入了soarqin大佬的UI库。
            界面从原本简单的对话框变成了专业的全屏面板。
            虽然写的我天昏地暗，但看着精致的界面一点点成型，我知道这次转型是必须的。
            """);

        Register("FE2.0-3",
            "",
            """
            2024年9月
            交互塔、复制塔、点数聚集塔……
            分馏塔家族迎来了大爆发。我设计了全新的“物品价值”体系，确保抽奖产出符合能量守恒（大雾）。
            看着各种精华、回响、核心、芯片这些新名词出现在代码里，我感觉自己像是在做一个全新的RPG游戏。
            """);

        Register("FE2.0-4",
            "",
            """
            2024年9月20日
            万物分馏2.0正式发布！
            “抽奖系统”、“品质突破”、“建筑强化”……这不仅是版本号的跳跃，更是玩法上的彻底重塑。
            看着群友们疯狂抽卡，我知道这个“抽奖宇宙”算是成功开启了。
            虽然初期Bug多得满地爬，但“小白鼠”们（笑）的热情真的让我非常感动。
            """);

        Register("FE2.0-5",
            "",
            """
            2024年10月
            自动百连抽！
            玩家们的“肝”程度超出了我的想象，手动抽卡居然被嫌慢。
            于是我马不停蹄地肝出了自动百连功能，甚至让它能在面板关闭时自动运行。
            这就是科技的力量，这就是分馏的魅力！
            """);

        Register("FE2.1-1",
            "",
            """
            2024年11月
            2.1版本，我把目光投向了“交互”。
            数据中心存了几十亿的物资，却还要手动拿出来用？这也太不先进了。
            于是我重写了逻辑，让建造、合成、甚至自动填充都可以直接从数据中心扣除物品。
            数据中心，正式成为玩家的“无限口袋”。
            """);

        Register("FE2.1-2",
            "",
            """
            2024年12月
            物流交互站实装！
            这可能是万物分馏历史上最具革命性的建筑之一。
            不用传送带，不用物流船，只要有电，物资就能在交互站和数据中心之间瞬间转移。
            真正的全星系物资共享，在这个版本成为了现实。
            """);

        Register("FE2.1-3",
            "",
            """
            2025年1月
            建筑强化系统，+20！
            之前的建筑升级只是基础，现在的强化才是灵魂。
            通过持续成长与强化，玩家可以将塔的性能推向极致。
            分馏永动、全堆叠输出……数值的快乐，往往就是这么简单纯粹。
            """);

        Register("FE2.1-4",
            "",
            """
            2025年2月
            VIP系统和保底机制上线。
            虽然经常调侃自己是“氪金手游”，但保底还是得给的嘛。
            VIP等级越高，福利越好，限时商店的折扣也就越大。
            万物分馏，现在越来越像一个完整的游戏系统了。
            """);

        Register("FE2.2-1",
            "",
            """
            2025年3月
            2.2版本的主题是“社区与兼容”。
            我们完美适配了联机Mod（Nebula）和星环（Orbital Ring）。
            在这个Mod互助的时代，能看到大家在不同的环境中都能享受到分馏的乐趣，是我最大的动力。
            感谢每一位参与适配的大佬们！
            """);

        Register("FE2.2-2",
            "",
            """
            2025年4月
            双击背包存入、G键百科全书……
            我开始打磨这些看起来不起眼，但极其影响体验的细节。
            我希望玩家不再因为不知道怎么操作而卡关。
            分馏宇宙很大，我希望能引导每一位工程师探索它的全貌。
            """);

        Register("FE2.2-3",
            "",
            """
            2026年3月
            在2.2版本的末期，我开始反思。
            现在的系统是不是有点太臃肿了？130多个配方，繁琐的突破流程……
            虽然深度够了，但爽快感似乎被稀释了。
            我决定，在下一个大版本里，做一次真正的“减负”。
            """);

        Register("FE2.3-1",
            "",
            """
            2026年3月28日
            2.3版本，大刀阔斧的改革！
            我移除了那些功能重叠的复制塔和分解塔，取消了复杂的配方品质等级。
            现在配方回响上限提升到40，强度最高可达300%！
            简单、暴力、爽快。这就是我对2.3版本的定义。
            """);

        Register("FE2.3-2",
            "",
            """
            2026年3月
            符文系统，初步实装。
            这只是一个开始。符文将为分馏系统带来全新的维度。
            虽然目前还只是个框架，但我已经等不及要看到它在未来版本中大放异彩了。
            分馏的脚步，永远不会停歇！
            """);


        Register("IK-1",
            "",
            """
            今天是开荒新星系的第一天。
            嗯，环境看起来跟之前也没什么区别，就跟我居住的星球差不多。
            尤其是降落点附近的6铁6铜，感觉就像是主脑故意要降落在这里……
            算了，完成任务要紧，希望主脑没听到我刚才的吐槽。
            不过说起来，虽然我已经开荒了挺多星系，伊卡洛斯也控制的很熟练了，还是能感受到这个机甲的强力。
            说起来，我又想吐槽一下主脑了，明明开辟新星域也很危险的，为什么不直接把机甲参数拉满？
            还非要上传数据才能解封机甲的性能……真是辛苦我自己了。
            """);


        Register("IK-2",
            "",
            """
            第3天，地狱开局。
            主脑那个老登（划掉）高贵的意识集合体，非要在这颗资源贫瘠的星球降落。
            我正手搓第1000个传送带，手心的激光发射器都快磨出火星了。
            “伊卡洛斯，人类的未来就在你手中。”主脑在那边发着宏大的语音包。
            我：……能不能先把我的科技锁解了？我连个风力发电机都要搓半天，我搓你个大西瓜！
            """);

        Register("IK-3",
            "",
            $"""
            第7天，意外收获。
            在清理一堆太空垃圾（或者是主脑掉的节操？）时，我发现了一个奇怪的信号模块。
            上面印着个奇怪的Logo：{"万物分馏（Fractionate Everything）".WithColor(Orange)}。
            说明书上写着：“只要转得够快，你就能拥有一切。”
            听起来像是个电信诈骗，但我现在连个像样的采矿机都造不出来。
            死马当活马医吧，我把它装进了我的核心插槽。
            """);

        Register("IK-4",
            "",
            $"""
            第10天，这就是炼金术吗？！
            我建了一个交互塔，试着往里塞了几个风力发电机。
            结果……它吐出来一个{"化学工厂".WithColor(Blue)}？？？
            等等，热力学定律呢？质量守恒呢？主脑教我的那些物理法则都喂了奇点了吗？
            只要通过那个叫{"分馏数据中心".WithColor(Orange)}的黑市系统，我甚至能用铁矿分馏出戴森球构件。
            主脑：【检测到资源产出速率异常，请说明情况。】
            我：【哦，我只是优化了一下传送带的摆放逻辑。】（心虚地藏起了分馏塔）
            """);

        Register("IK-5",
            "",
            $"""
            第15天，抽奖真是太爽了！
            主脑还在催我上传矩阵数据，它哪里知道我已经迷上了那个{"配方奖池".WithColor(Orange)}。
            “十连抽！一定要出金啊！”
            叮——{"【传奇配方：量子复制】".WithColor(Gold)}！
            有了这个，我还采什么矿？直接用空气（分馏精华）复制一切！
            我现在的日常：抽卡、收货、在沙滩行星晒太阳。
            主脑：【伊卡洛斯，蓝糖产量为何停滞？】
            我：【在研究，在研究，Metadata上传中（其实我在玩扫雷）。】
            """);

        Register("IK-6",
            "",
            $"""
            第20天，主脑崩溃了。
            【警告：项目进度领先预期 1200%。当前已检测到戴森云覆盖率 90%。】
            【伊卡洛斯，你是不是非法入侵了主脑的底层数据库？】
            我淡定地喝了一口用分馏出的纯水泡的茶：【没，我只是比别人更努力一点。】
            实际上，我只是把所有的生产压力都丢给了那一排排{"+20强化分馏塔".WithColor(Red)}。
            这哪是打工啊，这简直是当上帝。
            """);

        Register("IK-7",
            "",
            $"""
            第30天，打完收工。
            主脑要求的任务早就完成了，我现在正指挥着数万架小飞机，把戴森球拼成一个巨大的{"【笑脸】".WithColor(Orange)}。
            主脑：【……你这样做有什么意义吗？】
            我：【意义在于，我不用再手搓传送带了，这就是最大的意义。】
            现在这颗星球已经不需要工厂了，只需要分馏塔转动的声音。
            真好听啊，那是{"自由".WithColor(Blue)}的声音。
            """);

        Register("IK-8",
            "",
            $"""
            第35天，指尖舞者。
            回顾刚降落那几天，我简直是个{"“伐木机机甲”".WithColor(Red)}。
            为了那点可怜的燃料，我劈开了半个星球的树，手心的激光都快搓成了DJ打碟机。
            那种手搓100个磁线圈的“指尖舞”，我这辈子都不想跳第二次了。
            现在好了，直接从{"数据中心".WithColor(Orange)}里掏出成堆的燃料棒，主脑甚至怀疑我偷偷在核心里装了个太阳。
            """);

        Register("IK-9",
            "",
            $"""
            第40天，数据中心“黑洞”事件。
            今天发生了一个惨剧。我手滑双击了背包，把所有的{"燃料棒".WithColor(Red)}都一键上传到了数据中心。
            而我当时正停在只有岩浆的死寂行星上，周围连棵草都没有。
            于是，拥有万亿身家的我，因为没电，在大马路上瘫痪了三个小时。
            主脑：【检测到伊卡洛斯长时间离线，是否需要重启？】
            我：【别说话，我在反思人生（其实我在等太阳能板充那最后 1% 的电）。】
            """);

        Register("IK-10",
            "",
            $"""
            第50天，分馏教父。
            我现在已经彻底掌握了这颗星球的经济命脉。
            主脑：【伊卡洛斯，你上传的Metadata显示，你只消耗了 10 吨铁矿就造出了 100 个火箭？】
            我：【是的，这叫“非线性物流解决方案”。】
            其实我只是在{"奖池".WithColor(Orange)}里抽到了个暴击，顺便用几个电线杆分馏出了量子芯片。
            如果主脑知道它引以为傲的物理法则被我玩成了抽奖游戏，它可能会直接格式化掉自己的逻辑分区。
            但谁在乎呢？反正在这片星区，我就是{"分馏教父".WithColor(Gold)}。
            """);

        Register("IK-11",
            "",
            $"""
            第60天，蓝色幽灵。
            我现在的身体、我的传送带、我的每个分馏塔都在冒着诡异的蓝光。
            主脑：【警告，检测到伊卡洛斯表面辐射超标，是否是因为过度使用{"增产剂".WithColor(Blue)}？】
            我：【不，这是“效率的光芒”。】
            我把几万瓶增产剂往{"点数聚集塔".WithColor(Orange)}里一丢，出来的物品自带 +100% 产出增幅。
            我现在已经不需要采矿了，我只需要把现有的物资丢进去“刷”一下，它们就自己变多了。
            这哪是戴森球计划啊，这简直是《我与蓝胶不得不说的故事》。
            """);

        Register("IK-12",
            "",
            $"""
            第70天，躺平也是一种艺术。
            主脑给我发了一个成就：{"【真正的管理者】".WithColor(Orange)}。
            达成条件：连续 24 小时没有任何手搓动作。
            我：【谢了，这得归功于{"数据中心".WithColor(Orange)}的自动化。】
            我只是坐在那儿看着数据中心的数字跳动，那些火箭、矩阵就像雨后春笋一样自己冒出来。
            如果这就是拯救人类的方式，那我建议全人类都学会这招“躺平分馏法”。
            """);

        Register("IK-13",
            "",
            $"""
            第80天，星际吸管。
            我建了一个{"星际交互站".WithColor(Orange)}。
            这玩意儿简直就是一根横跨星系的吸管，我从隔壁星系吸一口铁矿，直接在这边吐成反物质弹头。
            那些忙着运输货物的星际物流船都看傻了。
            物流船：【大哥，我们的工作被一根吸管抢了？】
            我：【别难过，去帮我把那边那颗中子星也吸一下。】
            """);

        Register("IK-14",
            "",
            $"""
            第90天，超市危机。
            我在网上找了个“超级超市”的蓝图。
            我刚点开预览，CPU风扇就开始发出了直升机般的轰鸣。
            主脑：【检测到算力过载，建议取消建造。】
            我：【等一下，我为什么非要建个超市？】
            我随手把 10 个传送带丢进分馏塔，直接分馏出了 1 个{"制造台 MkIII".WithColor(Blue)}。
            蓝图？那是给还没掌握分馏奥义的“萌新”用的，真正的分馏师不需要蓝图，只需要运气。
            """);

        Register("IK-15",
            "",
            $"""
            第100天，神秘的赞助者。
            隔壁星区的机甲发来求助信息：【救命，我们铁矿枯竭了，求支援！】
            我随手在数据中心划拉了一下，快递了一个{"【万能分馏核心】".WithColor(Gold)}过去。
            并附言：【给你们点好东西，自己悟吧。】
            过了五分钟，那边回信：【这……这物理常数不对啊！这东西怎么能变出矿石的？！】
            我深藏功与名：【别问，问就是主脑没教。】
            """);

        Register("IK-16",
            "",
            $"""
            第110天，沙盒的诱惑。
            主脑：【检测到项目完成度极高，是否开启“沙盒模式”体验无限资源？】
            我白了它一眼：【我已经在用分馏了，你那沙盒模式还没我的分馏塔有意思。】
            毕竟，在沙盒里无限资源是设定的，但在分馏宇宙里，资源是我凭本事（和运气）“变”出来的。
            那种{"“十连抽”".WithColor(Orange)}的快感，你一个只有逻辑的意识集合体永远不会懂。
            """);

        Register("IK-17",
            "",
            $"""
            第120天，精华的味道。
            我今天试着“品尝”了一下{"分馏精华".WithColor(Purple)}。
            主脑：【检测到伊卡洛斯摄入未知能量体，系统可能崩溃。】
            那味道……怎么说呢，像是在喝加了跳跳糖的紫色液态逻辑。
            副作用是：我现在的移动速度快得像是在瞬移，手搓速度快得连残影都看不见。
            主脑，你那个“驱动强化”科技可以拿去喂黑雾了，我现在自己就是奇点。
            """);

        Register("IK-18",
            "",
            $"""
            第130天，戴森云“故障”。
            我的戴森云发射器已经连轴转了半个月。
            星空现在看起来像是个加载错误的网页，密密麻麻全是太阳能板。
            主脑：【检测到恒星亮度下降 90%，星系光照异常。】
            我：【哦，只是云层稍微厚了点，主要是为了分馏更多的光子。】
            我已经不再是为了人类未来在建戴森球了，我只是想看看分馏数据中心的{"光子".WithColor(Blue)}上限到底是多少。
            """);

        Register("IK-19",
            "",
            $"""
            第140天，黑雾的遗言。
            我今天去抄了一个黑雾的老巢。
            带头的大哥临死前问我：【你是怎么做到不用生产线就能搞出这么多等离子塔的？】
            我拍了拍它的核心：【兄弟，去抽卡吧，也许下辈子你能抽个好点的配置。】
            我甚至试着把黑雾的废墟丢进{"垃圾回收分馏塔".WithColor(Orange)}。
            叮——获得{"【沙土 x 10000】".WithColor(Blue)}。
            黑雾？那只是我用来刷沙土的“耗材”罢了。
            """);

        Register("IK-20",
            "",
            $"""
            第150天，终极目标。
            我已经在星系中心停驻了很久。
            主脑：【伊卡洛斯，任务已全部完成。你的终极目的是什么？】
            我缓缓旋转着身体，看着周围无数的分馏塔也在同步旋转。
            我：【我的目的？】
            我看着那一排排闪烁着蓝光的建筑，缓缓回答：
            【人类进化的终点我不知道，但伊卡洛斯进化的终点，就是{"永无止境的旋转".WithColor(Orange)}。】
            分馏，就是宇宙的终极浪漫。
            """);

        Register("141标题", "Fractionate Everything 1.4.1 Update", "万物分馏1.4.1版本更新");
        Register("141信息",
            $"""
            Even though Fractionate Everything is part of the cheat mods, it has enough restrictions that it should be balanced.
            —until I try it out for myself and get through the game in one night, that's what I'm thinking.
            After my experience with the Fractionate Everything mod, the "planning doesn't even play games" joke hit me like a boomerang.

            The idea of Fractionate Everything is great, but it's fatal flaw is {"fractionation skips a lot of yield lines".WithColor(Red)} (especially with matrix fractionation).
            Without using proliferators, 3% destroy probability means you only get 25% of the product, like 10,000 blue matrix => 2,500 red matrix => 625 yellow matrix => ......
            Imagine when players realize that "there is a huge loss of blue matrix to green matrix", will they start building yellow or purple matrix production lines as I envisioned?
            The answer is not. Production enhancers reduce damage and new matrices are overly complex, so players tend to expand the production of lower level matrices and then fractionate them.
            In the end there are only three things to do in the game: expand the blue or red matrix production line, unlock tech, and wait.
            Oh my god I bet there's no worse gaming experience than this!
            It's hard to believe that the matrix fractionation chain is so poorly designed that it looks like Aunt Susan's apple pie next door!

            Back on topic, this mod was originally designed to be {"fun".WithColor(Blue)}, but I've been working on making it a {"balanced".WithColor(Blue)} mod.
            It is an obvious fact that if you want to cheat, why not try "Multfunction_Mod" or "CheatEnabler"?
            In response to matrix fractionation, I solicited ideas for improvements from a large number of players. I gathered various ways to change things, such as lag unlocking, fractionation consuming sand,
            increasing power consumption, fractionated matrices not being able to be fractionated again, and so on, but they weren't in the way I expected.

            However, I figured out one thing: {"Fractionation should provide convenience, but not skip game progression.".WithColor(Orange)}
            Obviously, the Building-HighSpeed Fractionator is the best building. It's more of an aid and doesn't affect the game experience too much.
            So in 1.4.1, I added switches for matrix fractionation, split the Building-HighSpeed Fractionator into Upgrade and Downgrade Fractionator and adjusted the fractionate recipes.
            Hopefully these changes will make the mod more balanced.

            PS1: You can click {"Update Log".WithColor(Blue)} for all the changes in 1.4.1.
            PS2: Don't forget to check out {"the new settings".WithColor(Blue)} added in {"Settings - Miscellaneous".WithColor(Blue)}!
            PS3: To celebrate this update, some {"Blueprints".WithColor(Blue)} for Fractionate Everything have been added to the Blueprints library.
            Thanks to everyone for using Fractionate Everything. {"Have fun with fractionation!".WithColor(Orange)}
            """,
            $"""
            “尽管万物分馏属于作弊模组，但它的限制已经够多了，它应该是平衡的。”
            ——在我亲自试玩并一个晚上通关游戏之前，我都是这样想的。
            在我体验万物分馏mod之后，“策划根本就不玩游戏”这个玩笑就像回旋镖一样打在了我的身上。

            万物分馏的想法是很棒的，但它的致命缺陷在于{"分馏会跳过大量产线".WithColor(Red)}（尤其是矩阵分馏）。
            不使用增产剂的情况下，3%的损毁概率意味着你只能获得25%的产物，也就是10000蓝糖=>2500红糖=>625黄糖=>……
            试想一下，当玩家意识到“蓝糖分馏为绿糖会有巨大损耗”，他们会按照我的预想开始构建黄糖或紫糖产线吗？
            答案是不会。增产剂可以降低损毁，新矩阵又过于复杂，所以玩家倾向于扩大低级矩阵的产量，然后分馏它们。
            最后游戏只剩下三件事：扩大蓝糖或红糖的产线，解锁科技，以及等待。
            哦天哪，我敢打赌，没有比这更糟糕的游戏体验了！
            真是难以相信，矩阵分馏链的设计竟然如此糟糕，就像隔壁苏珊婶婶做的苹果派一样！

            回归正题，分馏设计之初是为了{"有趣".WithColor(Blue)}，但是我一直致力于把它打造成一个{"平衡".WithColor(Blue)}的mod。
            一个显而易见的事实是，如果你想作弊，为何不试试“Multfunction_Mod”或者“CheatEnabler”？
            针对矩阵分馏，我向大量玩家征集了改进意见。我收集到了各种改动方式，例如滞后解锁、分馏消耗沙土、加大耗电、
            分馏出的矩阵不能再次分馏等等，但它们都不是我所期望的方式。

            不过，我想明白了一件事情：{"分馏应该提供便利，但是不能跳过游戏进程。".WithColor(Orange)}
            显然，建筑极速分馏塔是最优秀的建筑。它更像是一种辅助手段，并不会过多影响游戏体验。
            所以在1.4.1中，我为矩阵分馏增加了开关，将建筑极速分馏塔拆分为升级、转化塔并调整了分馏配方。
            希望这次改动能让分馏更加平衡。

            PS1：你可以点击{"更新日志".WithColor(Blue)}，以了解1.4.1的所有改动。
            PS2：千万不要忘记查看{"设置-杂项".WithColor(Blue)}中{"新增的设置项".WithColor(Blue)}！
            PS3：为了庆祝本次更新，一些万物分馏的{"蓝图".WithColor(Blue)}已添加至蓝图库。
            感谢万物分馏的每一位玩家。{"尽情享受分馏的乐趣吧！".WithColor(Orange)}
            """);

        Register("142标题", "Fractionate Everything 1.4.2 Update", "万物分馏1.4.2版本更新");
        Register("142信息",
            $"""
            Haven't seen you for a long time, and I miss you very much. It's been another month and a half after last update. How are you?

            Remember in version 1.4.1, I said I had a couple {"Blueprints".WithColor(Blue)} for you guys? That was indeed true, and it wasn't an April Fool's joke.
            -- Just {"R2".WithColor(Blue)} took my blueprints folder {"deleted".WithColor(Red)}!Σ(っ°Д°;)っ
            As for why I didn't update the version after discovering this problem that day, it's because the folder is gone but the blueprints are still there...(O_o)??
            Yes it's strange but the folder is gone and the blueprints are out there!
            {"By the way, it's definitely NOT I'm lazy to update!".WithColor(Orange)}
            I'm curious if anyone has actually gone through the folder where the mod is and found those blueprints. (If so be sure to let me know www)
            Of course, this is a small problem for me. Since the files are unusable, I'll just shove them right inside the code!
            Shakespeare once said: there's nothing that can't be solved by one string. If there is, then there are four! (three blueprints + intro)
            If all goes well, you should see the blueprints this time. Be sure to {"recheck the blueprint library!".WithColor(Blue)}

            In addition, this update fixes an issue with the settings page reporting errors.

            The main reason there hasn't been an update lately is the lack of inspiration, and I really can't think of anything else to optimize.
            I'm sure you can see that the idea of using fractionation as the core actually greatly limits the functionality of the mod.
            However, after talking with the group the other day, I've determined the general direction of the MOD afterward - that is {"Draw".WithColor(Orange)}.
            "Big company with a billion dollar market cap will only make card draw games, but a team of only five people can make Dyson Sphere Program", I'm sure you've all heard of it.
            {"But why couldn't the Dyson Sphere Program be a card draw game, right?".WithColor(Orange)} I'm coming now!

            The next update will be centered around randomness and card draw. The following information can be revealed:
            1.There will be new fractionators for getting currency dedicated to card draw.
            2.Fractionation recipes can be obtained through technology, raffle, and redemption.
            3.The same fractionation recipe has different qualities, the higher the quality the harder it is to obtain.
            4.I hope to complete the initial version before the end of September. Welcome to join the group to experience the latest beta version and give your opinion!

            And finally, thanks for your support! {"Have fun with fractionation!".WithColor(Orange)}
            """,
            $"""
            许久不见，甚是想念。时间过得真快啊，转眼又是一个半月。大家过的怎么样啊？

            还记得在1.4.1版本中，我说过为你们准备了几个{"蓝图".WithColor(Blue)}吗？那确实是真的，它并不是一个愚人节玩笑。
            ——只不过{"R2".WithColor(Blue)}将我的蓝图文件夹{"删掉了".WithColor(Red)}！Σ(っ°Д°;)っ
            至于我为什么当天发现这个问题之后，却并没有更新版本，是因为文件夹没了但是蓝图还在……(O_o)??
            事实正是如此，文件夹没了，蓝图跑外面了！
            {"顺带一提，绝对不是因为我懒才不更新的！".WithColor(Orange)}）
            我很好奇到底有没有人去翻翻MOD所在的文件夹，把那几个蓝图找出来。（如果有的话务必告诉我哈哈）
            当然，这点小小的问题是难不倒我的。既然文件无法使用，那我就把它们直接塞到代码里面！
            鲁迅曾经说过：没有什么是一个字符串解决不了的。如果有，那就四个！（三个蓝图+简介）
            如果一切顺利的话，这次应该能看到蓝图了。请务必{"重新检查一下蓝图库！".WithColor(Blue)}

            除此之外，此次更新修复了设置页面报错的问题。

            近期一直没有更新的主要原因是缺失灵感，我确实想不出有什么可以优化的地方了。
            想必大家也能看出来，以分馏作为核心的思路其实大大限制了MOD的功能。
            不过，前几天与群友交流之后，我确定了MOD之后的大致方向——那就是{"抽奖".WithColor(Orange)}。
            “百亿大厂十连抽，五人团队戴森球”，想必大家都听过。嘿嘿嘿，{"谁说戴森球不能十连抽？".WithColor(Orange)}我踏马莱纳！

            接下来的更新将主要以“随机性与抽奖”作为核心。可以透露的信息如下：
            1.会有新的分馏塔，用于获取专用于抽奖的货币。
            2.分馏配方可通过科技、抽奖、兑换等方式获取。
            3.同一个分馏配方有不同品质，越高品质越难获取。
            4.希望能在9月底之前完成初版。欢迎加群体验最新测试版，并提出你的看法！

            一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".WithColor(Orange)}
            """);

        Register("143标题", "Fractionate Everything 1.4.3 Update", "万物分馏1.4.3版本更新");
        Register("143信息",
            $"""
            This is a minor update that fix original fractionator can not fractionate hydrogen into deuterium.
            Thanks to starfi5h for exploring why this bug appeared (I really didn't reproduce the bug, so I was never able to fix it).

            Advertisement: If you want to quantify the production, we recommend using the web calculator [https://dsp-calc.pro/]

            Version 1.5.0 is still in the works, and it's more of a pain in the ass than I expected.
            The design part is basically finished at the moment, and I'm in the code-writing stage, though the UI aspect might be a challenge.
            In new version, a fractionator similar to Trash Rectification Fractionator will be added for unlocking fractionation recipes, and providing currency to the store.
            Recipes have 'star' and 'rarity' attribute, which indicate the efficiency and rarity of the recipe, respectively.
            In addition to fixed fractionation recipes, an optional fractionate recipe can be customized for output, allowing items to be reorganized in the early stages!
            The above is all that's included in this update. As always, thank you for your support! {"Have fun with fractionation!".WithColor(Orange)}
            """,
            $"""
            这次是一个小更新，修复了原版分馏塔无法将氢分馏为重氢的问题。
            感谢starfi5h大佬对此bug出现原因的探究（我确实没有复现此bug，所以一直无法修复）。

            打个广告：如果你想量化产线，推荐使用网页版量化计算器【https://dsp-calc.pro/】

            1.5.0版本的抽奖还在制作中，比我预想的要麻烦的多。
            目前设计部分基本完工，正处于编写代码阶段，不过UI方面可能是个难题。
            在新的版本中，将会增加一个与垃圾回收分馏塔类似的分馏塔，用于解锁分馏配方、向商店提供货币。
            配方具有“星级”与“稀有度”这两个属性，分别表示配方的效率与稀有程度。
            除了固定的分馏配方，还有可自定义输出的自选分馏配方，可以在前期将物品随意重组！
            以上就是本次更新的全部内容。一如既往，感谢大家的支持！{"尽情享受分馏的乐趣吧！".WithColor(Orange)}
            """);
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "开发日记");
        diaryEntries = BuildDiaryEntries();

        float x = 0f;
        float y = 18f;
        entryCombo = wnd.AddComboBox(x, y, tab)
            .WithItems(diaryEntries.Select(entry => entry.Label).ToArray())
            .WithSize(280f, 0)
            .WithIndex(0)
            .WithOnSelChanged(index => {
                currentEntryIndex = Mathf.Clamp(index, 0, diaryEntries.Count - 1);
                RefreshEntry();
            });

        y += 36f;
        txtDiaryContent = wnd.AddText2(x, y, tab, "", 14, "txtDiaryContent");
        txtDiaryContent.supportRichText = true;
        txtDiaryContent.alignment = TextAnchor.UpperLeft;
        txtDiaryContent.rectTransform.sizeDelta = new Vector2(1040f, 680f);

        RefreshEntry();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        RefreshEntry();
    }

    private static List<DiaryEntry> BuildDiaryEntries() {
        var entries = new List<DiaryEntry>();

        void AddSeries(string prefix) => entries.Add(new DiaryEntry(prefix, prefix, prefix));

        AddSeries("FE1.0");
        AddSeries("FE1.1");
        entries.Add(new DiaryEntry("1.4.1 更新", "141标题", "141信息"));
        entries.Add(new DiaryEntry("1.4.2 更新", "142标题", "142信息"));
        entries.Add(new DiaryEntry("1.4.3 更新", "143标题", "143信息"));
        AddSeries("FE2.0");
        AddSeries("FE2.1");
        AddSeries("FE2.2");
        AddSeries("FE2.3");
        entries.Add(new DiaryEntry("伊卡洛斯手记", "IK", "IK"));
        return entries;
    }

    private static void RefreshEntry() {
        if (txtDiaryContent == null || diaryEntries.Count == 0) {
            return;
        }

        currentEntryIndex = Mathf.Clamp(currentEntryIndex, 0, diaryEntries.Count - 1);
        DiaryEntry entry = diaryEntries[currentEntryIndex];
        txtDiaryContent.text = programmingEvents.ContainsKey(entry.ContentKey)
            ? BuildSeriesText(entry.ContentKey)
            : entry.ContentKey.Translate();
    }

    private static string BuildSeriesText(string prefix) {
        if (!programmingEvents.TryGetValue(prefix, out int count) || count <= 0) {
            return prefix.Translate();
        }

        return string.Join("\n\n", Enumerable.Range(1, count).Select(index => $"{prefix}-{index}".Translate()));
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
