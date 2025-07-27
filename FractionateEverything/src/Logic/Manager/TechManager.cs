using CommonAPI.Systems;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static class TechManager {
    public static void AddTranslations() {
        //todo: 英文翻译

        Register("T分馏数据中心", "-", "分馏数据中心");
        Register("分馏数据中心描述",
            "-",
            $"随着各个星区的不断探索，分馏科技有了极大的拓展，主脑特地下发一系列相关科技来帮助伊卡洛斯建设戴森球。同时，主脑提供了一种新的数据交互方式，按 {"Shift+F".WithColor(Orange)} 即可连接。");
        Register("分馏数据中心结果",
            "-",
            "你已经掌握了连接分馏数据中心的方法，现在可以连接到分馏数据中心了。");


        Register("T电磁奖券", "-", "电磁奖券");
        Register("电磁奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("电磁奖券结果",
            "-",
            "你已经掌握了制作电磁奖券的技术，可以通过自动化的方式制作它了。");

        Register("T能量奖券", "-", "能量奖券");
        Register("能量奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("能量奖券结果",
            "-",
            "你已经掌握了制作能量奖券的技术，可以通过自动化的方式制作它了。");

        Register("T结构奖券", "-", "结构奖券");
        Register("结构奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("结构奖券结果",
            "-",
            "你已经掌握了制作结构奖券的技术，可以通过自动化的方式制作它了。");

        Register("T信息奖券", "-", "信息奖券");
        Register("信息奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("信息奖券结果",
            "-",
            "你已经掌握了制作信息奖券的技术，可以通过自动化的方式制作它了。");

        Register("T引力奖券", "-", "引力奖券");
        Register("引力奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("引力奖券结果",
            "-",
            "你已经掌握了制作引力奖券的技术，可以通过自动化的方式制作它了。");

        Register("T宇宙奖券", "-", "宇宙奖券");
        Register("宇宙奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("宇宙奖券结果",
            "-",
            "你已经掌握了制作宇宙奖券的技术，可以通过自动化的方式制作它了。");

        Register("T黑雾奖券", "-", "黑雾奖券");
        Register("黑雾奖券描述",
            "-",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("黑雾奖券结果",
            "-",
            "你已经掌握了制作黑雾奖券的技术，可以通过自动化的方式制作它了。");


        Register("T首充1", "-", "首充1");
        Register("首充1描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张电磁矩阵的抽奖券，以及极其珍贵的通用核心、增幅芯片！你还在等什么？\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充1结果",
            "-",
            "电磁奖券x100，分馏配方通用核心x5，分馏塔增幅芯片x3 已到账。");

        Register("T首充2", "-", "首充2");
        Register("首充2描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张能量矩阵的抽奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充2结果",
            "-",
            "能量奖券x100 已到账。");

        Register("T首充3", "-", "首充3");
        Register("首充3描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张结构矩阵的抽奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充3结果",
            "-",
            "结构奖券x100 已到账。");

        Register("T首充4", "-", "首充4");
        Register("首充4描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张信息矩阵的抽奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充4结果",
            "-",
            "信息奖券x100 已到账。");

        Register("T首充5", "-", "首充5");
        Register("首充5描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张引力矩阵的抽奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充5结果",
            "-",
            "引力奖券x100 已到账。");

        Register("T首充6", "-", "首充6");
        Register("首充6描述",
            "-",
            "超值礼包！只要100电磁矩阵，就可以获取100张宇宙矩阵的抽奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("首充6结果",
            "-",
            "宇宙奖券x100 已到账。");


        Register("T分馏塔原胚", "-", "分馏塔原胚");
        Register("分馏塔原胚描述",
            "In the course of Icarus' exploration, the Mastermind discovered that some star zones were extremely resource-poor and unsustainable. In order to make it easier for Icarus to explore the barren star zones, the Mastermind specially researched and issued the Fractionation of Natural Resources technology. This technology can be used to replicate the vast majority of natural resources, avoiding situations where lack of resources prevents exploration.",
            $"此次提供的分馏科技中，新分馏塔将采用特殊方式获取——也就是抽奖与培养。你需要在分馏数据中心的抽奖页面使用各种奖券进行抽奖，在{"建筑卡池".WithColor(Orange)}可以获取到分馏塔原胚。运气好的话，还能直接获取新的分馏塔。原胚经过交互塔培养，即可产出各种分馏塔。同时，主脑提供了一种人工合成原胚的科技，合成得到的定向原胚可以直接培养为指定的建筑。");
        Register("分馏塔原胚结果",
            "You have mastered the Natural Resource Fractionation technology, which can be replicated indefinitely as long as you have a certain amount of natural resources.",
            "你已经了解了分馏塔原胚的相关信息，可以将不同品质的分馏塔原胚合成为定向分馏塔原胚了。");

        Register("T物品交互", "-", "物品交互");
        Register("物品交互描述",
            "-",
            "主脑开发了一种高级的信息传输技术，可以将输入交互塔的物品以数据的形式传递到分馏数据中心，便于后续使用。");
        Register("物品交互结果",
            "-",
            "你已经掌握了物品交互技术，可以用物品交互塔与产线交互了。");

        Register("T矿物复制", "Natural Resource Fractionation", "矿物复制");
        Register("矿物复制描述",
            "In the course of Icarus' exploration, the Mastermind discovered that some star zones were extremely resource-poor and unsustainable. In order to make it easier for Icarus to explore the barren star zones, the Mastermind specially researched and issued the Fractionation of Natural Resources technology. This technology can be used to replicate the vast majority of natural resources, avoiding situations where lack of resources prevents exploration.",
            "在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。为了让伊卡洛斯能更轻松地探索贫瘠的星区，主脑特意研究并下发了自然资源的分馏技术。这项技术可以用来复制绝大多数自然资源，避免出现缺乏资源无法探索的情形。");
        Register("矿物复制结果",
            "You have mastered the Natural Resource Fractionation technology, which can be replicated indefinitely as long as you have a certain amount of natural resources.",
            "你已经掌握了矿物复制技术，只要拥有一定量的自然资源，就能对其进行无限复制。");

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
            + $"{"Warning:".WithColor(Orange)} The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, {"Please research manually.".WithColor(Orange)}",
            "矿物复制和物品转化虽然强大，但这些技术只能用于处理特定物品。随着对黑雾研究的不断深入，似乎存在将这种复制模式扩展到宇宙万物的可能性。研究发现，如果物品的增产点数对分馏过程的影响从加速变为增产，并利用物质重组技术使产物与输入相同，就可以达到复制万物的效果。\n"
            + "显然，将增产效果与物质重组关联的研究过程高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
            + $"{"警告：".WithColor(Orange)}该科技的相关技术已被COSMO技术伦理委员会禁用，{"请手动研究。".WithColor(Orange)}");
        Register("量子复制结果",
            "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
            "你已经掌握了量子复制技术。现在，你真正拥有了无中生有的能力！");

        Register("T物品点金", "-", "物品点金");
        Register("物品点金描述",
            "-",
            "物品点金科技可以将任意的物品转换为各种矩阵。但是，如果一个物品本身是矩阵，或者一个物品的制作材料包含矩阵，或者它是建筑，那么这个物品不能被点金。");
        Register("物品点金结果",
            "-",
            "你已经掌握了物品点金技术，可以将物品转换为各种矩阵。");

        Register("T物品分解", "Trash Recycle", "物品分解");
        Register("物品分解描述",
            "Foundations and sand are an essential part of the exploration process. Trash pickup allows you to dispose of any item as foundation or sand, which is helpful for expanding into new terrain. However, this technology cannot be used in recycling buildings, and whether or not the waste is sprayed with proliferators does not affect the efficiency of the process.",
            "地基和沙土是探索过程中必不可少的一环。物品分解科技可以将任意的物品处理为地基或沙土，对新地盘的扩展很有帮助。不过，这项科技无法用于回收建筑，并且是否为垃圾喷涂增产剂不会影响处理效率。");
        Register("物品分解结果",
            "You have mastered the Trash Recycle technology and can recycle unwanted items, converting them into foundations or sand.",
            "你已经掌握了物品分解技术，可以回收不需要的物品，将其转换为地基或沙土。");

        Register("T物品转化", "Up-Downgrade Fractionation", "物品转化");
        Register("物品转化描述",
            "-",
            "为了方便伊卡洛斯的探索，主脑下发了部分物品的转化科技。转化科技可将物品转为其他物品。尽管如此，它依然是探索路上的强力援助。");
        Register("物品转化结果",
            "You have mastered the Up-Downgrade Fractionation technology and can now recycle process some items to copy them.",
            "你已经掌握了物品转化技术，可以用物品转化塔循环处理物品，从而实现物品的复制。");


        // Register("T分馏流动输出集装", "Fractionate Fluid Output Integrate", "分馏流动输出集装");
        // Register("分馏流动输出集装等级",
        //     " Integration count of fractionate fluid output",
        //     " 分馏流动输出集装数量");
        // Register("分馏流动输出集装描述",
        //     "Failed fractionated items will be integrated as much as possible in a cargo.",
        //     "分馏失败的原料将会尽可能以集装形式输出。");
        // Register("分馏流动输出集装结果",
        //     "All failed fractionated items will now be integrated as much as possible in a cargo.",
        //     "现在，所有分馏失败的原料都将尽可能集装后再输出。");
        //
        // Register("T分馏产物输出集装", "Fractionate Product Output Integrate", "分馏产物输出集装");
        // Register("分馏产物输出集装等级",
        //     " Integration count of fractionate product output",
        //     " 分馏产物输出集装数量");
        // Register("分馏产物输出集装描述1",
        //     "Successful fractionated items will be integrated as much as possible in a cargo.",
        //     "分馏成功的产物将会尽可能以集装形式输出。");
        // Register("分馏产物输出集装结果1",
        //     "All successful fractionated items will now be integrated as much as possible in a cargo.",
        //     "现在，所有分馏成功的产物都将尽可能集装后再输出。");
        // Register("分馏产物输出集装描述2",
        //     "Further increases the integration count of fractionate product in a cargo.",
        //     "进一步提高分馏产物的集装数量。");
        // Register("分馏产物输出集装结果2",
        //     "The integration count of fractionate product in a cargo was further improved.",
        //     "所有分馏产物的集装数量进一步提升了。");
        //
        // Register("T分馏永动", "Fractionate Forever", "分馏永动");
        // Register("分馏持续运行",
        //     "Make specific types of fractionators keep running",
        //     "使特定种类的分馏塔可以持续运行");
        // Register("分馏永动描述",
        //     "It has been found that when multiple fractionators form a loop, there is often a buildup of product from one fractionator, which causes all fractionators to stop working. To solve this problem, the Mastermind provides technology that can control the fractionation process. Any time the number of products reaches half of the internal storage limit, the fractionator will not fractionate any products, but only maintain the flow of raw materials, thus ensuring the normal operation of the other fractionators in the loop.",
        //     "研究发现，多个分馏塔形成环路时，经常出现某个分馏塔产物堆积，从而导致所有分馏塔停止工作的情况。为了解决这个问题，主脑提供了可以控制分馏过程的科技。任何产物数目达到内部存储上限一半时，分馏塔将不会分馏出任何产物，仅维持原料的流动，以此确保环路其他分馏塔的正常运行。");
        // Register("分馏永动结果",
        //     "Now, fractionators will keep running without product buildup.",
        //     "现在，分馏塔将会持续运行，不会出现产物堆积的情况了。");
    }

    /// <summary>
    /// 添加所有科技。对于科技的位置，x向右y向下，间距固定为4
    /// </summary>
    public static void AddTechs() {
        var tech分馏数据中心 = ProtoRegistry.RegisterTech(
            TFE分馏数据中心, "T分馏数据中心", "分馏数据中心描述", "分馏数据中心结果", "Assets/fe/tech分馏数据中心",
            GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
            [I铁矿, I铜矿], [100, 100], 3600,
            [],
            GetTechPos(1, 0)
        );


        var tech电磁奖券 = ProtoRegistry.RegisterTech(
            TFE电磁奖券, "T电磁奖券", "电磁奖券描述", "电磁奖券结果", "Assets/fe/tech电磁奖券",
            [tech分馏数据中心.ID],
            [I电磁矩阵], [200], 18000,
            [RFE电磁奖券],
            GetTechPos(0, 1)
        );
        tech电磁奖券.PreTechsImplicit = [T电磁矩阵];
        tech电磁奖券.AddItems = [IFE电磁奖券];
        tech电磁奖券.AddItemCounts = [10];

        var tech能量奖券 = ProtoRegistry.RegisterTech(
            TFE能量奖券, "T能量奖券", "能量奖券描述", "能量奖券结果", "Assets/fe/tech能量奖券",
            [tech电磁奖券.ID],
            [I能量矩阵], [400], 36000,
            [RFE能量奖券],
            GetTechPos(0, 2)
        );
        tech电磁奖券.PreTechsImplicit = [T能量矩阵];
        tech能量奖券.AddItems = [IFE能量奖券];
        tech能量奖券.AddItemCounts = [10];

        var tech结构奖券 = ProtoRegistry.RegisterTech(
            TFE结构奖券, "T结构奖券", "结构奖券描述", "结构奖券结果", "Assets/fe/tech结构奖券",
            [tech能量奖券.ID],
            [I结构矩阵], [600], 54000,
            [RFE结构奖券],
            GetTechPos(0, 3)
        );
        tech结构奖券.PreTechsImplicit = [T结构矩阵];
        tech结构奖券.AddItems = [IFE结构奖券];
        tech结构奖券.AddItemCounts = [10];

        var tech信息奖券 = ProtoRegistry.RegisterTech(
            TFE信息奖券, "T信息奖券", "信息奖券描述", "信息奖券结果", "Assets/fe/tech信息奖券",
            [tech结构奖券.ID],
            [I信息矩阵], [800], 72000,
            [RFE信息奖券],
            GetTechPos(0, 4)
        );
        tech信息奖券.PreTechsImplicit = [T信息矩阵];
        tech信息奖券.AddItems = [IFE信息奖券];
        tech信息奖券.AddItemCounts = [10];

        var tech引力奖券 = ProtoRegistry.RegisterTech(
            TFE引力奖券, "T引力奖券", "引力奖券描述", "引力奖券结果", "Assets/fe/tech引力奖券",
            [tech信息奖券.ID],
            [I引力矩阵], [1000], 90000,
            [RFE引力奖券],
            GetTechPos(0, 5)
        );
        tech引力奖券.PreTechsImplicit = [T引力矩阵];
        tech引力奖券.AddItems = [IFE引力奖券];
        tech引力奖券.AddItemCounts = [10];

        var tech宇宙奖券 = ProtoRegistry.RegisterTech(
            TFE宇宙奖券, "T宇宙奖券", "宇宙奖券描述", "宇宙奖券结果", "Assets/fe/tech宇宙奖券",
            [tech引力奖券.ID],
            [I宇宙矩阵], [1200], 108000,
            [RFE宇宙奖券],
            GetTechPos(0, 6)
        );
        tech宇宙奖券.PreTechsImplicit = [T宇宙矩阵];
        tech宇宙奖券.AddItems = [IFE宇宙奖券];
        tech宇宙奖券.AddItemCounts = [10];

        var tech黑雾奖券 = ProtoRegistry.RegisterTech(
            TFE黑雾奖券, "T黑雾奖券", "黑雾奖券描述", "黑雾奖券结果", "Assets/fe/tech黑雾奖券",
            [],
            [I黑雾矩阵], [10000], 300000,
            [RFE黑雾奖券],
            GetTechPos(0, 7)
        );
        tech黑雾奖券.IsHiddenTech = true;
        tech黑雾奖券.PreItem = [I黑雾矩阵];
        tech黑雾奖券.PreTechsImplicit = [TFE分馏数据中心];
        tech黑雾奖券.AddItems = [IFE黑雾奖券];
        tech黑雾奖券.AddItemCounts = [10];


        var tech首充1 = ProtoRegistry.RegisterTech(
            TFE首充1, "T首充1", "首充1描述", "首充1结果", "Assets/fe/tech首充1",
            [tech分馏数据中心.ID],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(1, 1)
        );
        tech首充1.PreTechsImplicit = [T电磁矩阵];
        tech首充1.AddItems = [IFE电磁奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech首充1.AddItemCounts = [100, 5, 3];

        var tech首充2 = ProtoRegistry.RegisterTech(
            TFE首充2, "T首充2", "首充2描述", "首充2结果", "Assets/fe/tech首充2",
            [tech首充1.ID],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(1, 2)
        );
        tech首充2.PreTechsImplicit = [T能量矩阵];
        tech首充2.AddItems = [IFE能量奖券];
        tech首充2.AddItemCounts = [100];

        var tech首充3 = ProtoRegistry.RegisterTech(
            TFE首充3, "T首充3", "首充3描述", "首充3结果", "Assets/fe/tech首充3",
            [tech首充1.ID],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(1, 3)
        );
        tech首充3.PreTechsImplicit = [T结构矩阵];
        tech首充3.AddItems = [IFE结构奖券];
        tech首充3.AddItemCounts = [100];

        var tech首充4 = ProtoRegistry.RegisterTech(
            TFE首充4, "T首充4", "首充4描述", "首充4结果", "Assets/fe/tech首充4",
            [tech首充1.ID],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(1, 4)
        );
        tech首充4.PreTechsImplicit = [T信息矩阵];
        tech首充4.AddItems = [IFE信息奖券];
        tech首充4.AddItemCounts = [100];

        var tech首充5 = ProtoRegistry.RegisterTech(
            TFE首充5, "T首充5", "首充5描述", "首充5结果", "Assets/fe/tech首充5",
            [tech首充1.ID],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(1, 5)
        );
        tech首充5.PreTechsImplicit = [T引力矩阵];
        tech首充5.AddItems = [IFE引力奖券];
        tech首充5.AddItemCounts = [100];

        var tech首充6 = ProtoRegistry.RegisterTech(
            TFE首充6, "T首充6", "首充6描述", "首充6结果", "Assets/fe/tech首充6",
            [tech首充1.ID],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(1, 6)
        );
        tech首充6.PreTechsImplicit = [T宇宙矩阵];
        tech首充6.AddItems = [IFE宇宙奖券];
        tech首充6.AddItemCounts = [100];


        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [tech分馏数据中心.ID],
            [IFE分馏塔原胚普通], [50], 3600,
            [RFE分馏塔原胚定向],
            GetTechPos(2, 1)
        );
        tech分馏塔原胚.AddItems = [IFE分馏塔原胚定向];
        tech分馏塔原胚.AddItemCounts = [10];

        var tech物品交互 = ProtoRegistry.RegisterTech(
            TFE物品交互, "T物品交互", "物品交互描述", "物品交互结果", "Assets/fe/tech物品交互",
            [tech分馏塔原胚.ID],
            [IFE交互塔], [10], 1800,
            [RFE交互塔],
            GetTechPos(2, 2)
        );
        tech物品交互.AddItems = [IFE交互塔];
        tech物品交互.AddItemCounts = [10];

        var tech矿物复制 = ProtoRegistry.RegisterTech(
            TFE矿物复制, "T矿物复制", "矿物复制描述", "矿物复制结果", "Assets/fe/tech矿物复制",
            [tech物品交互.ID],
            [IFE矿物复制塔], [10], 1800,
            [RFE矿物复制塔, RFE复制精华],
            GetTechPos(2, 3)
        );
        tech矿物复制.AddItems = [IFE矿物复制塔];
        tech矿物复制.AddItemCounts = [10];

        var tech增产点数聚集 = ProtoRegistry.RegisterTech(
            TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果", "Assets/fe/tech增产点数聚集",
            [tech矿物复制.ID],
            [IFE点数聚集塔], [10], 1800,
            [RFE点数聚集塔],
            GetTechPos(2, 4)
        );
        tech增产点数聚集.AddItems = [IFE点数聚集塔];
        tech增产点数聚集.AddItemCounts = [10];

        var tech量子复制 = ProtoRegistry.RegisterTech(
            TFE量子复制, "T量子复制", "量子复制描述", "量子复制结果", "Assets/fe/tech量子复制",
            [tech增产点数聚集.ID],
            [IFE量子复制塔, I黑雾矩阵], [10, 2000], 60000,
            [RFE量子复制塔],
            GetTechPos(2, 5)
        );
        tech量子复制.IsHiddenTech = true;
        //前置物品仅需物质重组器，只要掉落该物品，该科技就为可见状态
        tech量子复制.PreItem = [I物质重组器];
        tech量子复制.AddItems = [IFE量子复制塔];
        tech量子复制.AddItemCounts = [10];

        var tech物品点金 = ProtoRegistry.RegisterTech(
            TFE物品点金, "T物品点金", "物品点金描述", "物品点金结果", "Assets/fe/tech物品点金",
            [tech物品交互.ID],
            [IFE点金塔], [10], 1800,
            [RFE点金塔, RFE点金精华],
            GetTechPos(2, 6)
        );
        tech物品点金.AddItems = [IFE点金塔];
        tech物品点金.AddItemCounts = [10];

        var tech物品分解 = ProtoRegistry.RegisterTech(
            TFE物品分解, "T物品分解", "物品分解描述", "物品分解结果", "Assets/fe/tech物品分解",
            [tech物品点金.ID],
            [IFE分解塔], [10], 1800,
            [RFE分解塔, RFE分解精华],
            GetTechPos(2, 7)
        );
        tech物品分解.AddItems = [IFE分解塔];
        tech物品分解.AddItemCounts = [10];

        var tech物品转化 = ProtoRegistry.RegisterTech(
            TFE物品转化, "T物品转化", "物品转化描述", "物品转化结果", "Assets/fe/tech物品转化",
            [tech物品分解.ID],
            [IFE转化塔], [10], 1800,
            [RFE转化塔, RFE转化精华],
            GetTechPos(2, 8)
        );
        tech物品转化.AddItems = [IFE转化塔];
        tech物品转化.AddItemCounts = [10];

        //todo: 添加行星分馏塔

        // var tech3807 = ProtoRegistry.RegisterTech(TFE分馏流动输出集装,
        //     "T分馏流动输出集装", "分馏流动输出集装描述", "分馏流动输出集装结果",
        //     LDB.techs.Select(T运输站集装物流 + 2).IconPath,
        //     [],
        //     [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
        //     [], new(37, -31));
        // tech3807.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
        //
        // var tech3804 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装,
        //     "T分馏产物输出集装1", "分馏产物输出集装描述1", "分馏产物输出集装结果1",
        //     LDB.techs.Select(T运输站集装物流).IconPath,
        //     [],
        //     [I电磁矩阵, I能量矩阵, I结构矩阵], [8, 8, 8], 180000,
        //     [], new(37, -35));
        // tech3804.Name = "T分馏产物输出集装";
        // tech3804.PreTechsImplicit = GenesisBook.Enable ? [TGB集装物流系统] : [T集装物流系统_GB物品仓储];
        // tech3804.Level = 1;
        // tech3804.MaxLevel = 1;
        //
        // var tech3805 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装 + 1,
        //     "T分馏产物输出集装2", "分馏产物输出集装描述2", "分馏产物输出集装结果2",
        //     LDB.techs.Select(T运输站集装物流 + 1).IconPath,
        //     [tech3804.ID],
        //     [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵], [8, 8, 8, 8], 360000,
        //     [], new(41, -35));
        // tech3805.Name = "T分馏产物输出集装";
        // tech3805.Level = 2;
        // tech3805.MaxLevel = 2;
        //
        // var tech3806 = ProtoRegistry.RegisterTech(TFE分馏产物输出集装 + 2,
        //     "T分馏产物输出集装", "分馏产物输出集装描述2", "分馏产物输出集装结果2",
        //     LDB.techs.Select(T运输站集装物流 + 2).IconPath,
        //     [tech3805.ID],
        //     [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
        //     [], new(45, -35));
        // tech3806.Name = "T分馏产物输出集装";
        // tech3806.Level = 3;
        // tech3806.MaxLevel = 3;
        //
        // var tech3808 = ProtoRegistry.RegisterTech(TFE分馏永动,
        //     "T分馏永动", "分馏永动描述", "分馏永动结果",
        //     "Assets/fe/tech分馏永动",
        //     [],
        //     [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], [8, 8, 8, 8, 8], 720000,
        //     [], new(45, -31));
        // tech3808.Name = "T分馏永动";
        // tech3808.PreTechsImplicit = [tech3806.ID];
    }

    /// <summary>
    /// 根据输入的行列，生成科技所在位置。
    /// </summary>
    /// <param name="row">从0开始，数字越大越靠下</param>
    /// <param name="column">从0开始，数字越大越靠右</param>
    /// <returns></returns>
    private static Vector2 GetTechPos(int row, int column) {
        return GenesisBook.Enable
            ? new(9 + column * 4, -47 - row * 4)
            : new(13 + column * 4, -67 - row * 4);
    }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    // public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
    //     switch (__instance.ID) {
    //         case TFE分馏流动输出集装:
    //             __result = "+3" + "分馏流动输出集装等级".Translate() + "\r\n";
    //             return false;
    //         case >= TFE分馏产物输出集装 and <= TFE分馏产物输出集装 + 2:
    //             __result = "+1" + "分馏产物输出集装等级".Translate() + "\r\n";
    //             return false;
    //         case TFE分馏永动:
    //             __result = "分馏持续运行".Translate() + "\r\n";
    //             return false;
    //     }
    //     return true;
    // }

    #region 一键解锁

    /// <summary>
    /// 处于沙盒模式下时，在点击“解锁全部”按钮后额外执行的操作
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    public static void UITechTree_Do1KeyUnlock_Postfix() {
        RecipeManager.UnlockAllFracRecipes();
    }

    #endregion
}
