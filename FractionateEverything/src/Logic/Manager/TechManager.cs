using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Recipe;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static class TechManager {
    public static void AddTranslations() {
        Register("T分馏数据中心", "Fractionation Data Center", "分馏数据中心");
        Register("分馏数据中心描述",
            $"With the exploration of more and more star regions, fractionation technology has greatly expanded. Press {"Shift + F".WithColor(Orange)} to connect to the fractionation data center, which is a new way of data interaction and the main carrier of new fractionation technology. At the same time, in recognition of Icarus' spirit of exploration, some initial fractionation formulas will also be unlocked.",
            $"随着探索的星区越来越多，分馏科技有了极大的拓展。按 {"Shift + F".WithColor(Orange)} 即可连接到分馏数据中心，这是全新的数据交互方式，也是新分馏科技的主要载体。同时，为了嘉奖伊卡洛斯的探索精神，一些初期的分馏配方也会随之解锁。");
        Register("分馏数据中心结果",
            "You have mastered the method of connecting to the fractionation data center, and now you can connect to the fractionation data center.",
            "你已经掌握了连接分馏数据中心的方法，现在可以连接到分馏数据中心了。部分分馏配方已解锁。");
        Register("允许连接到分馏数据中心", "Allow connection to fractionation data center");
        Register("解锁全部建筑培养配方", "Unlock all building train recipes");
        Register("解锁非珍奇矿物复制配方", "Unlock non rare mineral copy recipes");


        Register("T超值礼包1", "Super Value Gift Pack 1", "超值礼包1");
        Register("超值礼包1描述",
            "Super Value Gift Pack! As long as you have 100 electromagnetic matrices, you can obtain 100 electromagnetic lottery tickets and other useful items! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100电磁矩阵，就可以获取100张电磁奖券，以及其他实用物品！你还在等什么？\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包1结果",
            "Electromagnetic ticket x100, fractionation formula universal core x5, and fractionator amplification chip x3 have been received.",
            "电磁奖券x100，分馏配方通用核心x5，分馏塔增幅芯片x3 已到账。");
        Register("一个物超所值的礼包", "A great value package deal");

        Register("T超值礼包2", "Super Value Gift Pack 2", "超值礼包2");
        Register("超值礼包2描述",
            "Super Value Gift Pack! As long as you have 100 energy matrices, you can get 100 energy tickets! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100能量矩阵，就可以获取100张能量奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包2结果",
            "Energy ticket x100 have been received.",
            "能量奖券x100 已到账。");

        Register("T超值礼包3", "Super Value Gift Pack 3", "超值礼包3");
        Register("超值礼包3描述",
            "Super Value Gift Pack! As long as you have 100 structure matrices, you can get 100 structure tickets! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100结构矩阵，就可以获取100张结构奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包3结果",
            "Structure ticket x100 have been received.",
            "结构奖券x100 已到账。");

        Register("T超值礼包4", "Super Value Gift Pack 4", "超值礼包4");
        Register("超值礼包4描述",
            "Super Value Gift Pack! As long as you have 100 information matrices, you can get 100 information tickets! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100信息矩阵，就可以获取100张信息奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包4结果",
            "Information ticket x100 have been received.",
            "信息奖券x100 已到账。");

        Register("T超值礼包5", "Super Value Gift Pack 5", "超值礼包5");
        Register("超值礼包5描述",
            "Super Value Gift Pack! As long as you have 100 gravity matrices, you can get 100 gravity tickets! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100引力矩阵，就可以获取100张引力奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包5结果",
            "Gravity ticket x100 have been received.",
            "引力奖券x100 已到账。");

        Register("T超值礼包6", "Super Value Gift Pack 6", "超值礼包6");
        Register("超值礼包6描述",
            "Super Value Gift Pack! As long as you have 100 universe matrices, you can get 100 universe tickets! What are you waiting for? \n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100宇宙矩阵，就可以获取100张宇宙奖券！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包6结果",
            "Universe ticket x100 have been received.",
            "宇宙奖券x100 已到账。");


        Register("T电磁奖券", "Electromagnetic Ticket", "电磁奖券");
        Register("电磁奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("电磁奖券结果",
            "You have mastered the technology of making electromagnetic tickets and can now produce them through automation.",
            "你已经掌握了制作电磁奖券的技术，可以通过自动化的方式制作它了。");

        Register("T能量奖券", "Energy Ticket", "能量奖券");
        Register("能量奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("能量奖券结果",
            "You have mastered the technology of making energy tickets and can now produce them through automation.",
            "你已经掌握了制作能量奖券的技术，可以通过自动化的方式制作它了。");

        Register("T结构奖券", "Structure Ticket", "结构奖券");
        Register("结构奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("结构奖券结果",
            "You have mastered the technology of making structure tickets and can now produce them through automation.",
            "你已经掌握了制作结构奖券的技术，可以通过自动化的方式制作它了。");

        Register("T信息奖券", "Information Ticket", "信息奖券");
        Register("信息奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("信息奖券结果",
            "You have mastered the technology of making information tickets and can now produce them through automation.",
            "你已经掌握了制作信息奖券的技术，可以通过自动化的方式制作它了。");

        Register("T引力奖券", "Gravity Ticket", "引力奖券");
        Register("引力奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("引力奖券结果",
            "You have mastered the technology of making gravity tickets and can now produce them through automation.",
            "你已经掌握了制作引力奖券的技术，可以通过自动化的方式制作它了。");

        Register("T宇宙奖券", "Universe Ticket", "宇宙奖券");
        Register("宇宙奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("宇宙奖券结果",
            "You have mastered the technology of making universe tickets and can now produce them through automation.",
            "你已经掌握了制作宇宙奖券的技术，可以通过自动化的方式制作它了。");

        Register("T黑雾奖券", "Dark Fog Ticket", "黑雾奖券");
        Register("黑雾奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data center to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("黑雾奖券结果",
            "You have mastered the technology of making dark fog tickets and can now produce them through automation.",
            "你已经掌握了制作黑雾奖券的技术，可以通过自动化的方式制作它了。");


        Register("T分馏塔原胚", "Fractionator Proto", "分馏塔原胚");
        Register("分馏塔原胚描述",
            $"In the fractionation technology provided this time, the new fractionator will be obtained through a special method - namely lottery and cultivation. You need to use various lottery tickets on the lottery page of the fractionation data center to participate in the lottery, and you can obtain the raw material of the fractionator in the {"building card pool".WithColor(Orange)}. If lucky, you can also directly obtain a new fractionator. Through interactive tower culture, various fractionators can be produced from the original embryo. At the same time, the COSMO provides a technology for artificially synthesizing embryos, and the synthesized directed embryos can be directly cultured into designated buildings.",
            $"此次提供的分馏科技中，新分馏塔将采用特殊方式获取——也就是抽奖与培养。你需要在分馏数据中心的抽奖页面使用各种奖券进行抽奖，在{"建筑卡池".WithColor(Orange)}可以获取到分馏塔原胚。运气好的话，还能直接获取新的分馏塔。原胚经过交互塔培养，即可产出各种分馏塔。同时，主脑提供了一种人工合成原胚的科技，合成得到的定向原胚可以直接培养为指定的建筑。");
        Register("分馏塔原胚结果",
            "You have learned about the relevant information of the distillation tower precursor, and can combine different qualities of distillation tower precursor into directional distillation tower precursor.",
            "你已经了解了分馏塔原胚的相关信息，可以将不同品质的分馏塔原胚合成为定向分馏塔原胚了。");

        Register("T物品交互", "Item Interaction", "物品交互");
        Register("物品交互描述",
            "The COSMO has developed an advanced information transmission technology that can transmit the items input into the interactive tower in the form of data to the fractionation data center for subsequent use.",
            "主脑开发了一种高级的信息传输技术，可以将输入交互塔的物品以数据的形式传递到分馏数据中心，便于后续使用。");
        Register("物品交互结果",
            "You have mastered the Item Interaction technology and can now use the item interaction tower to interact with the production line.",
            "你已经掌握了物品交互技术，可以用物品交互塔与产线交互了。");

        Register("T矿物复制", "Mineral Copy", "矿物复制");
        Register("矿物复制描述",
            "During Icarus' exploration, the COSMO discovered that some star regions had extremely scarce resources that were difficult to sustain. In order to make it easier for Icarus to explore the barren star regions, the COSMO specially researched and issued the technology of natural resource fractionation. This technology can be used to replicate the vast majority of natural resources, avoiding situations where there is a lack of resources to explore.",
            "在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。为了让伊卡洛斯能更轻松地探索贫瘠的星区，主脑特意研究并下发了自然资源的分馏技术。这项技术可以用来复制绝大多数自然资源，避免出现缺乏资源无法探索的情形。");
        Register("矿物复制结果",
            "You have mastered the Mineral Copy technology, and as long as you have a certain amount of natural resources, you can replicate them infinitely.",
            "你已经掌握了矿物复制技术，只要拥有一定量的自然资源，就能对其进行无限复制。");

        Register("T增产点数聚集", "Proliferator Points Aggregate", "增产点数聚集");
        Register("增产点数聚集描述",
            "Due to the limitations of material technology, the spawn line is unable to create more advanced proliferators, but fractionation technology can break through the limitations by concentrating the raw material's proliferator points into a certain number of items. It was found that the proliferator points of items could be stacked indefinitely, but the portion over 10 points did not work. Proliferate Point Aggregate technology can fractionate just the items with 10 proliferator points.",
            "由于材料技术的限制，产线无法制造更高级的增产剂，但分馏技术可以将原料的增产点数集中到某几个物品上，从而突破增产剂的点数限制。研究发现，物品的增产点数可以无限叠加，但超过10点的部分不起作用。增产点数聚集技术可以刚好分馏出10点增产点数的物品。");
        Register("增产点数聚集结果",
            "You have mastered the Proliferator Points Aggregate technology. The item's proliferator points can now be pushed to the limit, and production capacity has been greatly increased!",
            "你已经掌握了增产点数聚集技术。现在物品的增产点数可以达到极限，产能得到了极大的提升！");

        Register("T量子复制", "Quantum Copy", "量子复制");
        Register("量子复制描述",
            "Although Mineral Copy is powerful, it can only be used to process specific items. As research of dark fog continued to deepen, it seemed that the possibility existed of expanding this mode of replication to everything in the universe. It was found that if the effect of the item's proliferator points on the fractionation process changes from accelerate to increase, and the material reorganization technique was used to make the product the same as the input, it would be possible to achieve the effect of duplicating everything.\n"
            + "It is clear that the research process of correlating yield-enhancing effects with material reorganization is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
            + $"{"Warning:".WithColor(Orange)} The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, {"Please research manually.".WithColor(Orange)}",
            "矿物复制和物品转化虽然强大，但这些技术只能用于处理特定物品。随着对黑雾研究的不断深入，似乎存在将这种复制模式扩展到宇宙万物的可能性。研究发现，如果物品的增产点数对分馏过程的影响从加速变为增产，并利用物质重组技术使产物与输入相同，就可以达到复制万物的效果。\n"
            + "显然，将增产效果与物质重组关联的研究过程高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
            + $"{"警告：".WithColor(Orange)}该科技的相关技术已被COSMO技术伦理委员会禁用，{"请手动研究。".WithColor(Orange)}");
        Register("量子复制结果",
            "You have unlocked the Quantum Copy technology. Now you truly have the ability to create something from nothing!",
            "你已经掌握了量子复制技术。现在，你真正拥有了无中生有的能力！");

        Register("T物品点金", "Item Alchemy", "物品点金");
        Register("物品点金描述",
            "Item alchemy technology can convert any item into various matrices. However, if an item itself is a matrix, or if the material used to make the item contains a matrix, or if it is a building, then the item cannot be minted.",
            "物品点金科技可以将任意的物品点金为各种矩阵。但是，如果一个物品本身是矩阵，或者一个物品的制作材料包含矩阵，或者它是建筑，那么这个物品不能被点金。");
        Register("物品点金结果",
            "You have mastered the Item Alchemy technology and can convert items into various matrices.",
            "你已经掌握了物品点金技术，可以将物品点金为各种矩阵。");

        Register("T物品分解", "Item Deconstruction", "物品分解");
        Register("物品分解描述",
            "Item deconstruction technology can break down any item into the materials used to make it. Meanwhile, items without a recipe will decompose into foundation and sand, which is very helpful for expanding into new territories.",
            "物品分解科技可以将任意物品分解为制作它的材料。同时，无制作配方的物品会分解为地基和沙土，这对新地盘的扩展很有帮助。");
        Register("物品分解结果",
            "You have mastered the Item Deconstruction technology and can now break down unwanted items into raw materials.",
            "你已经掌握了物品分解技术，可以将不需要的物品分解为原材料了。");

        Register("T物品转化", "Item Conversion", "物品转化");
        Register("物品转化描述",
            "Item conversion technology can transform an item into other items related to that item. Although only some items can be converted, it is still an extremely powerful aid.",
            "物品转化科技可以将物品转化为与这个物品相关的其他物品。尽管只有部分物品能进行转化，但它依然是极其强力的援助。");
        Register("物品转化结果",
            "You have mastered item conversion technology and can now convert items into other items related to this item.",
            "你已经掌握了物品转化技术，可以将物品转化为与这个物品相关的其他物品了。");


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
        //     "It has been found that when multiple fractionators form a loop, there is often a buildup of product from one fractionator, which causes all fractionators to stop working. To solve this problem, the Mastermind provides technology that can control the fractionation process. Any time the number of products reaches 75% of the internal storage limit, the fractionator will not fractionate any products, but only maintain the flow of raw materials, thus ensuring the normal operation of the other fractionators in the loop.",
        //     "研究发现，多个分馏塔形成环路时，经常出现某个分馏塔产物堆积，从而导致所有分馏塔停止工作的情况。为了解决这个问题，主脑提供了可以控制分馏过程的科技。任何产物数目达到内部存储上限75%时，分馏塔将不会分馏出任何产物，仅维持原料的流动，以此确保环路其他分馏塔的正常运行。");
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
            //注：哈希块是3600的x倍时，实际需要的物品数目为当前数目*x
            [I电磁矩阵], [10], 3600,
            [],
            GetTechPos(1, 0)
        );
        tech分馏数据中心.PreTechsImplicit = [T电磁矩阵];
        tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
        tech分馏数据中心.PropertyItemCounts = [10];


        var tech超值礼包1 = ProtoRegistry.RegisterTech(
            TFE超值礼包1, "T超值礼包1", "超值礼包1描述", "超值礼包1结果", "Assets/fe/tech超值礼包1",
            [tech分馏数据中心.ID],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(0, 1)
        );
        tech超值礼包1.PreTechsImplicit = [T电磁矩阵];
        tech超值礼包1.AddItems = [IFE电磁奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片, IFE交互塔, IFE分馏塔原胚普通];
        tech超值礼包1.AddItemCounts = [100, 5, 3, 3, 50];
        tech超值礼包1.PropertyOverrideItems = [I电磁矩阵];
        tech超值礼包1.PropertyItemCounts = [100];

        var tech超值礼包2 = ProtoRegistry.RegisterTech(
            TFE超值礼包2, "T超值礼包2", "超值礼包2描述", "超值礼包2结果", "Assets/fe/tech超值礼包2",
            [tech超值礼包1.ID],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(0, 2)
        );
        tech超值礼包2.PreTechsImplicit = [T能量矩阵];
        tech超值礼包2.AddItems = [IFE能量奖券];
        tech超值礼包2.AddItemCounts = [100];
        tech超值礼包2.PropertyOverrideItems = [I能量矩阵];
        tech超值礼包2.PropertyItemCounts = [100];

        var tech超值礼包3 = ProtoRegistry.RegisterTech(
            TFE超值礼包3, "T超值礼包3", "超值礼包3描述", "超值礼包3结果", "Assets/fe/tech超值礼包3",
            [tech超值礼包2.ID],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(0, 3)
        );
        tech超值礼包3.PreTechsImplicit = [T结构矩阵];
        tech超值礼包3.AddItems = [IFE结构奖券];
        tech超值礼包3.AddItemCounts = [100];
        tech超值礼包3.PropertyOverrideItems = [I结构矩阵];
        tech超值礼包3.PropertyItemCounts = [100];

        var tech超值礼包4 = ProtoRegistry.RegisterTech(
            TFE超值礼包4, "T超值礼包4", "超值礼包4描述", "超值礼包4结果", "Assets/fe/tech超值礼包4",
            [tech超值礼包3.ID],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(0, 4)
        );
        tech超值礼包4.PreTechsImplicit = [T信息矩阵];
        tech超值礼包4.AddItems = [IFE信息奖券];
        tech超值礼包4.AddItemCounts = [100];
        tech超值礼包4.PropertyOverrideItems = [I信息矩阵];
        tech超值礼包4.PropertyItemCounts = [100];

        var tech超值礼包5 = ProtoRegistry.RegisterTech(
            TFE超值礼包5, "T超值礼包5", "超值礼包5描述", "超值礼包5结果", "Assets/fe/tech超值礼包5",
            [tech超值礼包4.ID],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(0, 5)
        );
        tech超值礼包5.PreTechsImplicit = [T引力矩阵];
        tech超值礼包5.AddItems = [IFE引力奖券];
        tech超值礼包5.AddItemCounts = [100];
        tech超值礼包5.PropertyOverrideItems = [I引力矩阵];
        tech超值礼包5.PropertyItemCounts = [100];

        var tech超值礼包6 = ProtoRegistry.RegisterTech(
            TFE超值礼包6, "T超值礼包6", "超值礼包6描述", "超值礼包6结果", "Assets/fe/tech超值礼包6",
            [tech超值礼包5.ID],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(0, 6)
        );
        tech超值礼包6.PreTechsImplicit = [T宇宙矩阵];
        tech超值礼包6.AddItems = [IFE宇宙奖券];
        tech超值礼包6.AddItemCounts = [100];
        tech超值礼包6.PropertyOverrideItems = [I宇宙矩阵];
        tech超值礼包6.PropertyItemCounts = [100];


        var tech电磁奖券 = ProtoRegistry.RegisterTech(
            TFE电磁奖券, "T电磁奖券", "电磁奖券描述", "电磁奖券结果", "Assets/fe/tech电磁奖券",
            [tech分馏数据中心.ID],
            [I电磁矩阵], [40], 18000,
            [RFE电磁奖券],
            GetTechPos(1, 1)
        );
        tech电磁奖券.PreTechsImplicit = [T电磁矩阵];
        tech电磁奖券.AddItems = [IFE电磁奖券];
        tech电磁奖券.AddItemCounts = [10];
        tech电磁奖券.PropertyOverrideItems = [I电磁矩阵];
        tech电磁奖券.PropertyItemCounts = [200];

        var tech能量奖券 = ProtoRegistry.RegisterTech(
            TFE能量奖券, "T能量奖券", "能量奖券描述", "能量奖券结果", "Assets/fe/tech能量奖券",
            [tech电磁奖券.ID],
            [I能量矩阵], [40], 36000,
            [RFE能量奖券],
            GetTechPos(1, 2)
        );
        tech能量奖券.PreTechsImplicit = [T能量矩阵];
        tech能量奖券.AddItems = [IFE能量奖券];
        tech能量奖券.AddItemCounts = [10];
        tech能量奖券.PropertyOverrideItems = [I能量矩阵];
        tech能量奖券.PropertyItemCounts = [400];

        var tech结构奖券 = ProtoRegistry.RegisterTech(
            TFE结构奖券, "T结构奖券", "结构奖券描述", "结构奖券结果", "Assets/fe/tech结构奖券",
            [tech能量奖券.ID],
            [I结构矩阵], [40], 54000,
            [RFE结构奖券],
            GetTechPos(1, 3)
        );
        tech结构奖券.PreTechsImplicit = [T结构矩阵];
        tech结构奖券.AddItems = [IFE结构奖券];
        tech结构奖券.AddItemCounts = [10];
        tech结构奖券.PropertyOverrideItems = [I结构矩阵];
        tech结构奖券.PropertyItemCounts = [600];

        var tech信息奖券 = ProtoRegistry.RegisterTech(
            TFE信息奖券, "T信息奖券", "信息奖券描述", "信息奖券结果", "Assets/fe/tech信息奖券",
            [tech结构奖券.ID],
            [I信息矩阵], [40], 72000,
            [RFE信息奖券],
            GetTechPos(1, 4)
        );
        tech信息奖券.PreTechsImplicit = [T信息矩阵];
        tech信息奖券.AddItems = [IFE信息奖券];
        tech信息奖券.AddItemCounts = [10];
        tech信息奖券.PropertyOverrideItems = [I信息矩阵];
        tech信息奖券.PropertyItemCounts = [800];

        var tech引力奖券 = ProtoRegistry.RegisterTech(
            TFE引力奖券, "T引力奖券", "引力奖券描述", "引力奖券结果", "Assets/fe/tech引力奖券",
            [tech信息奖券.ID],
            [I引力矩阵], [40], 90000,
            [RFE引力奖券],
            GetTechPos(1, 5)
        );
        tech引力奖券.PreTechsImplicit = [T引力矩阵];
        tech引力奖券.AddItems = [IFE引力奖券];
        tech引力奖券.AddItemCounts = [10];
        tech引力奖券.PropertyOverrideItems = [I引力矩阵];
        tech引力奖券.PropertyItemCounts = [1000];

        var tech宇宙奖券 = ProtoRegistry.RegisterTech(
            TFE宇宙奖券, "T宇宙奖券", "宇宙奖券描述", "宇宙奖券结果", "Assets/fe/tech宇宙奖券",
            [tech引力奖券.ID],
            [I宇宙矩阵], [40], 108000,
            [RFE宇宙奖券],
            GetTechPos(1, 6)
        );
        tech宇宙奖券.PreTechsImplicit = [T宇宙矩阵];
        tech宇宙奖券.AddItems = [IFE宇宙奖券];
        tech宇宙奖券.AddItemCounts = [10];
        tech宇宙奖券.PropertyOverrideItems = [I宇宙矩阵];
        tech宇宙奖券.PropertyItemCounts = [1200];

        var tech黑雾奖券 = ProtoRegistry.RegisterTech(
            TFE黑雾奖券, "T黑雾奖券", "黑雾奖券描述", "黑雾奖券结果", "Assets/fe/tech黑雾奖券",
            [],
            [I黑雾矩阵], [800], 9000,
            [RFE黑雾奖券],
            GetTechPos(1, 7)
        );
        tech黑雾奖券.IsHiddenTech = true;
        tech黑雾奖券.PreItem = [I黑雾矩阵];
        tech黑雾奖券.PreTechsImplicit = [TFE分馏数据中心];
        tech黑雾奖券.AddItems = [IFE黑雾奖券];
        tech黑雾奖券.AddItemCounts = [10];


        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [tech分馏数据中心.ID],
            [IFE分馏塔原胚普通], [10], 3600,
            [RFE分馏塔原胚定向],
            GetTechPos(2, 1)
        );
        tech分馏塔原胚.AddItems = [IFE分馏塔原胚定向];
        tech分馏塔原胚.AddItemCounts = [10];
        tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
        tech分馏塔原胚.PropertyItemCounts = [100];

        var tech物品交互 = ProtoRegistry.RegisterTech(
            TFE物品交互, "T物品交互", "物品交互描述", "物品交互结果", "Assets/fe/tech物品交互",
            [],
            [IFE交互塔], [10], 3600,
            [RFE交互塔],
            GetTechPos(2, 2)
        );
        tech物品交互.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品交互.AddItems = [IFE交互塔];
        tech物品交互.AddItemCounts = [10];
        tech物品交互.PropertyOverrideItems = [I电磁矩阵];
        tech物品交互.PropertyItemCounts = [200];

        var tech矿物复制 = ProtoRegistry.RegisterTech(
            TFE矿物复制, "T矿物复制", "矿物复制描述", "矿物复制结果", "Assets/fe/tech矿物复制",
            [],
            [IFE矿物复制塔], [10], 3600,
            [RFE矿物复制塔],
            GetTechPos(2, 3)
        );
        tech矿物复制.PreTechsImplicit = [TFE分馏塔原胚];
        tech矿物复制.AddItems = [IFE矿物复制塔];
        tech矿物复制.AddItemCounts = [10];
        tech矿物复制.PropertyOverrideItems = [I电磁矩阵];
        tech矿物复制.PropertyItemCounts = [200];

        var tech增产点数聚集 = ProtoRegistry.RegisterTech(
            TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果", "Assets/fe/tech增产点数聚集",
            [],
            [IFE点数聚集塔], [10], 3600,
            [RFE点数聚集塔],
            GetTechPos(2, 4)
        );
        tech增产点数聚集.PreTechsImplicit = [TFE分馏塔原胚];
        tech增产点数聚集.AddItems = [IFE点数聚集塔];
        tech增产点数聚集.AddItemCounts = [10];
        tech增产点数聚集.PropertyOverrideItems = [I电磁矩阵];
        tech增产点数聚集.PropertyItemCounts = [200];

        var tech量子复制 = ProtoRegistry.RegisterTech(
            TFE量子复制, "T量子复制", "量子复制描述", "量子复制结果", "Assets/fe/tech量子复制",
            [],
            [IFE量子复制塔, I黑雾矩阵], [1, 120], 60000,
            [RFE量子复制塔],
            GetTechPos(2, 5)
        );
        tech量子复制.PreTechsImplicit = [TFE分馏塔原胚, TFE增产点数聚集];
        tech量子复制.IsHiddenTech = true;
        tech量子复制.PreItem = [I物质重组器];
        tech量子复制.AddItems = [IFE量子复制塔];
        tech量子复制.AddItemCounts = [16];

        var tech物品点金 = ProtoRegistry.RegisterTech(
            TFE物品点金, "T物品点金", "物品点金描述", "物品点金结果", "Assets/fe/tech物品点金",
            [],
            [IFE点金塔], [10], 3600,
            [RFE点金塔],
            GetTechPos(2, 6)
        );
        tech物品点金.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品点金.AddItems = [IFE点金塔];
        tech物品点金.AddItemCounts = [10];
        tech物品点金.PropertyOverrideItems = [I电磁矩阵];
        tech物品点金.PropertyItemCounts = [200];

        var tech物品分解 = ProtoRegistry.RegisterTech(
            TFE物品分解, "T物品分解", "物品分解描述", "物品分解结果", "Assets/fe/tech物品分解",
            [],
            [IFE分解塔], [10], 3600,
            [RFE分解塔],
            GetTechPos(2, 7)
        );
        tech物品分解.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品分解.AddItems = [IFE分解塔];
        tech物品分解.AddItemCounts = [10];
        tech物品分解.PropertyOverrideItems = [I电磁矩阵];
        tech物品分解.PropertyItemCounts = [200];

        var tech物品转化 = ProtoRegistry.RegisterTech(
            TFE物品转化, "T物品转化", "物品转化描述", "物品转化结果", "Assets/fe/tech物品转化",
            [],
            [IFE转化塔], [10], 3600,
            [RFE转化塔],
            GetTechPos(2, 8)
        );
        tech物品转化.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品转化.AddItems = [IFE转化塔];
        tech物品转化.AddItemCounts = [10];
        tech物品转化.PropertyOverrideItems = [I电磁矩阵];
        tech物品转化.PropertyItemCounts = [200];

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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
        if (__instance.ID == TFE分馏数据中心) {
            __result = $"{"允许连接到分馏数据中心".Translate()}\r\n"
                       + $"{"解锁全部建筑培养配方".Translate()}\r\n"
                       + $"{"解锁非珍奇矿物复制配方".Translate()}";
            return false;
        }
        if (__instance.ID >= TFE超值礼包1 && __instance.ID <= TFE超值礼包6) {
            __result = $"{"一个物超所值的礼包".Translate()}";
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock))]
    public static void GameHistoryData_NotifyTechUnlock_Postfix(int _techId) {
        if (_techId == TFE分馏数据中心) {
            //解锁所有建筑培养配方
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.BuildingTrain)) {
                recipe.RewardThis();
            }
            //解锁非珍奇的原矿复制配方
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.MineralCopy)) {
                if (recipe.InputID >= I可燃冰 && recipe.InputID <= I单极磁石) {
                    continue;
                }
                recipe.RewardThis();
            }
        }
    }

    /// <summary>
    /// 如果已经解锁分馏数据中心科技，但是某些配方未解锁，解锁这些配方
    /// </summary>
    public static void CheckRecipesWhenImport() {
        if (GameMain.history.TechUnlocked(TFE分馏数据中心)) {
            bool recipesUnlocked = true;
            //判断所有建筑培养配方是否全部解锁
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.BuildingTrain)) {
                if (recipe.Locked) {
                    recipesUnlocked = false;
                    break;
                }
            }
            //判断非珍奇的原矿复制配方是否全部解锁
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.MineralCopy)) {
                if (recipe.InputID >= I可燃冰 && recipe.InputID <= I单极磁石) {
                    continue;
                }
                if (recipe.Locked) {
                    recipesUnlocked = false;
                    break;
                }
            }
            //如果有配方未解锁，可能是旧存档，解锁这部分配方
            if (!recipesUnlocked) {
                GameHistoryData_NotifyTechUnlock_Postfix(TFE分馏数据中心);
            }
        }
    }

    #region 一键解锁

    /// <summary>
    /// 处于沙盒模式下时，在点击“解锁全部”按钮后额外执行的操作
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    public static void UITechTree_Do1KeyUnlock_Postfix() {
        UnlockAllFracRecipes();
    }

    #endregion
}
