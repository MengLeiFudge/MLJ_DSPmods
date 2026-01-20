using System;
using System.Linq;
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
        Register("T分馏数据中心", "Fractionation Data Centre", "分馏数据中心");
        Register("分馏数据中心描述",
            $"With the exploration of more and more star regions, fractionation technology has greatly expanded. Press {"Shift + F".WithColor(Orange)} to connect to the fractionation data centre, which is a new way of data interaction and the main carrier of new fractionation technology. At the same time, in recognition of Icarus' spirit of exploration, some initial fractionation formulas will also be unlocked.",
            $"随着探索的星区越来越多，分馏科技有了极大的拓展。按 {"Shift + F".WithColor(Orange)} 即可连接到分馏数据中心，这是全新的数据交互方式，也是新分馏科技的主要载体。同时，为了嘉奖伊卡洛斯的探索精神，一些初期的分馏配方也会随之解锁。");
        Register("分馏数据中心结果",
            "You have mastered the method of connecting to the fractionation data centre, and now you can connect to the fractionation data centre.",
            "你已经掌握了连接分馏数据中心的方法，现在可以连接到分馏数据中心了。");
        Register("允许连接到分馏数据中心", "Allow connection to fractionation data centre");
        Register("给予一些分馏塔原胚", "Provide some fractionator protos");


        Register("T超值礼包1", "Super Value Gift Pack 1", "超值礼包1");
        Register("超值礼包1描述",
            "Super Value Gift Pack! For as little as 100 Electromagnetic Matrix, you can get a lot of Electromagnetic Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100电磁矩阵，就可以获取大量电磁奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包1结果",
            "Downloaded Electromagnetic Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 电磁奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");
        Register("一个物超所值的礼包", "A great value package deal");

        Register("T超值礼包2", "Super Value Gift Pack 2", "超值礼包2");
        Register("超值礼包2描述",
            "Super Value Gift Pack! For as little as 100 Energy Matrix, you can get a lot of Energy Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100能量矩阵，就可以获取大量能量奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包2结果",
            "Downloaded Energy Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 能量奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");

        Register("T超值礼包3", "Super Value Gift Pack 3", "超值礼包3");
        Register("超值礼包3描述",
            "Super Value Gift Pack! For as little as 100 Structure Matrix, you can get a lot of Structure Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100结构矩阵，就可以获取大量结构奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包3结果",
            "Downloaded Structure Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 结构奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");

        Register("T超值礼包4", "Super Value Gift Pack 4", "超值礼包4");
        Register("超值礼包4描述",
            "Super Value Gift Pack! For as little as 100 Information Matrix, you can get a lot of Information Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100信息矩阵，就可以获取大量信息奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包4结果",
            "Downloaded Information Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 信息奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");

        Register("T超值礼包5", "Super Value Gift Pack 5", "超值礼包5");
        Register("超值礼包5描述",
            "Super Value Gift Pack! For as little as 100 Gravity Matrix, you can get a lot of Gravity Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100引力矩阵，就可以获取大量引力奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包5结果",
            "Downloaded Gravity Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 引力奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");

        Register("T超值礼包6", "Super Value Gift Pack 6", "超值礼包6");
        Register("超值礼包6描述",
            "Super Value Gift Pack! For as little as 100 Universe Matrix, you can get a lot of Universe Tickets and Rare Rewards!\n\nIn the bottom right corner, there is a small line of text that reads: The interpretation of this activity belongs to the COSMO.",
            "超值礼包！只要100宇宙矩阵，就可以获取大量宇宙奖券和稀有奖励！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包6结果",
            "Downloaded Universe Ticket x200, Fractionate Recipe Core x3, Fractionator Increase Chip x6 to the fractionation data centre.",
            "已下载 宇宙奖券x200，分馏配方通用核心x3，分馏塔增幅芯片x6 到分馏数据中心。");


        Register("T电磁奖券", "Electromagnetic Ticket", "电磁奖券");
        Register("电磁奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("电磁奖券结果",
            "You have mastered the technology of making electromagnetic tickets and can now produce them through automation.",
            "你已经掌握了制作电磁奖券的技术，可以通过自动化的方式制作它了。");

        Register("T能量奖券", "Energy Ticket", "能量奖券");
        Register("能量奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("能量奖券结果",
            "You have mastered the technology of making energy tickets and can now produce them through automation.",
            "你已经掌握了制作能量奖券的技术，可以通过自动化的方式制作它了。");

        Register("T结构奖券", "Structure Ticket", "结构奖券");
        Register("结构奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("结构奖券结果",
            "You have mastered the technology of making structure tickets and can now produce them through automation.",
            "你已经掌握了制作结构奖券的技术，可以通过自动化的方式制作它了。");

        Register("T信息奖券", "Information Ticket", "信息奖券");
        Register("信息奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("信息奖券结果",
            "You have mastered the technology of making information tickets and can now produce them through automation.",
            "你已经掌握了制作信息奖券的技术，可以通过自动化的方式制作它了。");

        Register("T引力奖券", "Gravity Ticket", "引力奖券");
        Register("引力奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("引力奖券结果",
            "You have mastered the technology of making gravity tickets and can now produce them through automation.",
            "你已经掌握了制作引力奖券的技术，可以通过自动化的方式制作它了。");

        Register("T宇宙奖券", "Universe Ticket", "宇宙奖券");
        Register("宇宙奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("宇宙奖券结果",
            "You have mastered the technology of making universe tickets and can now produce them through automation.",
            "你已经掌握了制作宇宙奖券的技术，可以通过自动化的方式制作它了。");

        Register("T黑雾奖券", "Dark Fog Ticket", "黑雾奖券");
        Register("黑雾奖券描述",
            "Lottery tickets are a type of token issued by the COSMO, which can generally only be obtained under specific conditions. However, the COSMO has also released technology for synthesizing lottery tickets on its own, but it requires a large number of matrices. Lotteries can be drawn on the lottery page of the fractionation data centre to obtain various items and formulas.",
            "奖券是主脑发行的一种代币，一般来讲只能在特定条件下获取。不过主脑也下发了自行合成奖券的科技，只不过需要大量矩阵。奖券可以在分馏数据中心的抽奖页面进行抽奖，获取各种物品和配方。");
        Register("黑雾奖券结果",
            "You have mastered the technology of making dark fog tickets and can now produce them through automation.",
            "你已经掌握了制作黑雾奖券的技术，可以通过自动化的方式制作它了。");


        Register("T分馏塔原胚", "Fractionator Proto", "分馏塔原胚");
        Register("分馏塔原胚描述",
            "In the new fractionate technology, the new fractionator cannot be crafted using materials. At the fractionation data centre's lottery page, using vouchers to draw from the Prototype Pool yields various prototypes. Interacting with the fractionator to process these prototypes cultivates different fractionators. Additionally, the prototype lottery carries a minuscule chance of yielding a Targeted Prototype for a specific fractionator, which can be directly cultivated into that designated fractionator.",
            "在全新的分馏科技中，新分馏塔无法使用材料制作。在分馏数据中心的奖券抽奖页面使用奖券抽取原胚奖池，可以得到不同的原胚；使用交互塔分馏原胚，可以培养出不同的分馏塔。除此之外，原胚抽奖有极小概率得到分馏塔定向原胚，它可以直接培养出指定的分馏塔。");
        Register("分馏塔原胚结果",
            "You have learned about the relevant information of the distillation tower precursor, and can combine different qualities of distillation tower precursor into directional distillation tower precursor.",
            "你已经了解了分馏塔原胚的相关信息，可以将分馏塔原胚培养为不同的分馏塔了。");
        Register("解锁全部建筑培养配方", "Unlock all building train recipes");
        Register("给予一个交互塔", "Provide a Interactive Tower");
        // Register("给予一些分馏塔原胚", "Provide some fractionator protos");//上面有了

        Register("T物品交互", "Item Interaction", "物品交互");
        Register("物品交互描述",
            "COSMO has developed an advanced item transmission technology, in which the interaction tower is responsible for converting physical items into virtual data and vice versa. When the interaction tower is in interaction mode, input items are transmitted to the fractionation data centre in the form of data, and selected items are output in physical form. Additionally, the interaction tower is responsible for cultivating fractionator prototypes. When the interaction tower is in cultivation mode, it can cultivate non-specific fractionator prototypes into different types of fractionators.",
            "主脑开发了一种高级的物品传输技术，其中交互塔承担了实体物品与虚拟数据互相转化的职责。交互塔处于交互模式时，输入的物品会以数据的形式传递到分馏数据中心，选择的物品会以实体形式输出。同时，交互塔也承担了培养分馏塔原胚的职责。交互塔处于培养模式时，可以将非定向的分馏塔原胚培养为不同的分馏塔。");
        Register("物品交互结果",
            "You have mastered the Item Interaction technology and can now use the item interaction tower to interact with the production line.",
            "你已经掌握了物品交互技术，可以用物品交互塔与产线交互了。");
        Register("自动上传被扔掉的物品", "Automatically upload dropped items");
        Register("双击背包排序按钮，自动上传背包内物品",
            "Double-click the backpack sort button to automatically upload the items within the backpack");

        Register("T矿物复制", "Mineral Replication", "矿物复制");
        Register("矿物复制描述",
            "During the exploration of Icarus, the COSMO discovered that some star systems were extremely resource-poor and difficult to sustain. Mineral replication technology was the perfect solution to this problem, as it could replicate most minerals, allowing Icarus to easily explore barren star systems.",
            "在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。矿物复制科技刚好可以解决这个问题，它能复制绝大多数矿物，让伊卡洛斯轻松探索贫瘠的星区。");
        Register("矿物复制结果",
            "You have mastered the mineral replication technique and can now replicate minerals into multiple copies.",
            "你已经掌握了矿物复制技术，可以将矿物复制为多份了。");
        Register("解锁部分矿物复制配方", "Unlock some Mineral Replication recipes");

        Register("T增产点数聚集", "Proliferator Points Aggregate", "增产点数聚集");
        Register("增产点数聚集描述",
            "Due to material limitations, proliferator technology has been unable to make further breakthroughs. However, proliferator point aggregation technology has solved this problem through fractionation. It can concentrate proliferator points onto specific items, thereby producing items that carry more proliferator points.",
            "增产剂科技因材料限制暂时无法突破，而增产点数聚集科技通过分馏的形式解决了此问题。它可以将增产点数集中到部分物品上，从而产出携带更多的增产点数的物品。");
        Register("增产点数聚集结果",
            "You have mastered the technique of accumulating proliferator points, allowing items to carry more proliferator points.",
            "你已经掌握了增产点数聚集技术，可以让物品携带更多的增产点数了。");

        Register("T量子复制", "Quantum Replication", "量子复制");
        Register("量子复制描述",
            "With continued research into dark fog and distilled essence, a new replication method has been developed. It can be applied to most items. By reconfiguring an object at the microscopic level and incorporating distilled essences of exceptional malleability, this item can be replicated in bulk. The proliferator points no longer increase the processing speed, but they can reduce the consumption of distilled essence.",
            "随着对黑雾和分馏精华的不断研究，一种新的复制模式随之诞生。如果将物品在微观层面进行重组，并添加具有卓越可塑性的分馏精华，就能批量复制这个物品。增产点数不再增加处理速度，但可以减少分馏精华的消耗。");
        Register("量子复制结果",
            "You have mastered quantum replication technology and can now batch replicate items with Fractionate Essence.",
            "你已经掌握了量子复制技术，可以用分馏精华批量复制物品了。");

        Register("T物品点金", "Item Alchemy", "物品点金");
        Register("物品点金描述",
            "Item Alchemy Technology can transform items into various matrices. It's a simple, straightforward way to get matrices, but low-value items seem to have a hard time spawning matrices.",
            "物品点金科技可以将物品点金成各种矩阵。这是一种简单、直接获取矩阵的方式，但是低价值物品似乎很难产出矩阵。");
        Register("物品点金结果",
            "You have mastered the art of item alchemy and can now transform items into various matrices.",
            "你已经掌握了物品点金技术，可以将物品点金成各种矩阵了。");

        Register("T物品分解", "Item Deconstruction", "物品分解");
        Register("物品分解描述",
            "Item decomposition technology can break down items into the materials or sand used to make them. This may seem useless, but it can be powerful in certain specific scenarios.",
            "物品分解科技可以将物品分解成制作它的材料或沙土。这看起来似乎没有用，但是在某些特定的场景下，它能发挥出强大的威力。");
        Register("物品分解结果",
            "You have mastered the art of item decomposition and can now break down items into the materials or sand used to craft them.",
            "你已经掌握了物品分解技术，可以将物品分解成制作它的材料或沙土了。");

        Register("T物品转化", "Item Conversion", "物品转化");
        Register("物品转化描述",
            "Item conversion technology can convert items into other items related to them. According to COSMO, transformations follow the principle of equivalence, though in practice there seems to be more to it than that...",
            "物品转化科技可以将物品转化成与其相关的其他物品。据主脑说，转化遵循等价原则，不过实际似乎不止这样……");
        Register("物品转化结果",
            "You have mastered the art of item conversion and can now convert items into other items related to them.",
            "你已经掌握了物品转化技术，可以将物品转化成与其相关的其他物品了。");
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
        tech分馏数据中心.AddItems = [IFE分馏塔原胚I型];
        tech分馏数据中心.AddItemCounts = [80];//20用于解锁分馏塔原胚科技，60赠送
        tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
        tech分馏数据中心.PropertyItemCounts = [10];


        var tech超值礼包1 = ProtoRegistry.RegisterTech(
            TFE超值礼包1, "T超值礼包1", "超值礼包1描述", "超值礼包1结果", "Assets/fe/tech超值礼包",
            [TFE分馏数据中心],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(0, 1)
        );
        tech超值礼包1.PreTechsImplicit = [TFE物品交互];
        tech超值礼包1.AddItems = [IFE电磁奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包1.AddItemCounts = [200, 3, 6];
        tech超值礼包1.PropertyOverrideItems = [I电磁矩阵];
        tech超值礼包1.PropertyItemCounts = [100];

        var tech超值礼包2 = ProtoRegistry.RegisterTech(
            TFE超值礼包2, "T超值礼包2", "超值礼包2描述", "超值礼包2结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包1],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(0, 2)
        );
        tech超值礼包2.PreTechsImplicit = [T能量矩阵];
        tech超值礼包2.AddItems = [IFE能量奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包2.AddItemCounts = [200, 3, 6];
        tech超值礼包2.PropertyOverrideItems = [I能量矩阵];
        tech超值礼包2.PropertyItemCounts = [100];

        var tech超值礼包3 = ProtoRegistry.RegisterTech(
            TFE超值礼包3, "T超值礼包3", "超值礼包3描述", "超值礼包3结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包2],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(0, 3)
        );
        tech超值礼包3.PreTechsImplicit = [T结构矩阵];
        tech超值礼包3.AddItems = [IFE结构奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包3.AddItemCounts = [200, 3, 6];
        tech超值礼包3.PropertyOverrideItems = [I结构矩阵];
        tech超值礼包3.PropertyItemCounts = [100];

        var tech超值礼包4 = ProtoRegistry.RegisterTech(
            TFE超值礼包4, "T超值礼包4", "超值礼包4描述", "超值礼包4结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包3],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(0, 4)
        );
        tech超值礼包4.PreTechsImplicit = [T信息矩阵];
        tech超值礼包4.AddItems = [IFE信息奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包4.AddItemCounts = [200, 3, 6];
        tech超值礼包4.PropertyOverrideItems = [I信息矩阵];
        tech超值礼包4.PropertyItemCounts = [100];

        var tech超值礼包5 = ProtoRegistry.RegisterTech(
            TFE超值礼包5, "T超值礼包5", "超值礼包5描述", "超值礼包5结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包4],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(0, 5)
        );
        tech超值礼包5.PreTechsImplicit = [T引力矩阵];
        tech超值礼包5.AddItems = [IFE引力奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包5.AddItemCounts = [200, 3, 6];
        tech超值礼包5.PropertyOverrideItems = [I引力矩阵];
        tech超值礼包5.PropertyItemCounts = [100];

        var tech超值礼包6 = ProtoRegistry.RegisterTech(
            TFE超值礼包6, "T超值礼包6", "超值礼包6描述", "超值礼包6结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包5],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(0, 6)
        );
        tech超值礼包6.PreTechsImplicit = [T宇宙矩阵];
        tech超值礼包6.AddItems = [IFE宇宙奖券, IFE分馏配方通用核心, IFE分馏塔增幅芯片];
        tech超值礼包6.AddItemCounts = [200, 3, 6];
        tech超值礼包6.PropertyOverrideItems = [I宇宙矩阵];
        tech超值礼包6.PropertyItemCounts = [100];


        var tech电磁奖券 = ProtoRegistry.RegisterTech(
            TFE电磁奖券, "T电磁奖券", "电磁奖券描述", "电磁奖券结果", "Assets/fe/tech分馏奖券",
            [TFE分馏数据中心],
            [I电磁矩阵], [40], 18000,
            [RFE电磁奖券],
            GetTechPos(1, 1)
        );
        tech电磁奖券.PreTechsImplicit = [TFE物品交互];
        tech电磁奖券.PropertyOverrideItems = [I电磁矩阵];
        tech电磁奖券.PropertyItemCounts = [200];

        var tech能量奖券 = ProtoRegistry.RegisterTech(
            TFE能量奖券, "T能量奖券", "能量奖券描述", "能量奖券结果", "Assets/fe/tech分馏奖券",
            [TFE电磁奖券],
            [I能量矩阵], [40], 36000,
            [RFE能量奖券],
            GetTechPos(1, 2)
        );
        tech能量奖券.PreTechsImplicit = [T能量矩阵];
        tech能量奖券.PropertyOverrideItems = [I能量矩阵];
        tech能量奖券.PropertyItemCounts = [400];

        var tech结构奖券 = ProtoRegistry.RegisterTech(
            TFE结构奖券, "T结构奖券", "结构奖券描述", "结构奖券结果", "Assets/fe/tech分馏奖券",
            [TFE能量奖券],
            [I结构矩阵], [40], 54000,
            [RFE结构奖券],
            GetTechPos(1, 3)
        );
        tech结构奖券.PreTechsImplicit = [T结构矩阵];
        tech结构奖券.PropertyOverrideItems = [I结构矩阵];
        tech结构奖券.PropertyItemCounts = [600];

        var tech信息奖券 = ProtoRegistry.RegisterTech(
            TFE信息奖券, "T信息奖券", "信息奖券描述", "信息奖券结果", "Assets/fe/tech分馏奖券",
            [TFE结构奖券],
            [I信息矩阵], [40], 72000,
            [RFE信息奖券],
            GetTechPos(1, 4)
        );
        tech信息奖券.PreTechsImplicit = [T信息矩阵];
        tech信息奖券.PropertyOverrideItems = [I信息矩阵];
        tech信息奖券.PropertyItemCounts = [800];

        var tech引力奖券 = ProtoRegistry.RegisterTech(
            TFE引力奖券, "T引力奖券", "引力奖券描述", "引力奖券结果", "Assets/fe/tech分馏奖券",
            [TFE信息奖券],
            [I引力矩阵], [40], 90000,
            [RFE引力奖券],
            GetTechPos(1, 5)
        );
        tech引力奖券.PreTechsImplicit = [T引力矩阵];
        tech引力奖券.PropertyOverrideItems = [I引力矩阵];
        tech引力奖券.PropertyItemCounts = [1000];

        var tech宇宙奖券 = ProtoRegistry.RegisterTech(
            TFE宇宙奖券, "T宇宙奖券", "宇宙奖券描述", "宇宙奖券结果", "Assets/fe/tech分馏奖券",
            [TFE引力奖券],
            [I宇宙矩阵], [40], 108000,
            [RFE宇宙奖券],
            GetTechPos(1, 6)
        );
        tech宇宙奖券.PreTechsImplicit = [T宇宙矩阵];
        tech宇宙奖券.PropertyOverrideItems = [I宇宙矩阵];
        tech宇宙奖券.PropertyItemCounts = [1200];

        var tech黑雾奖券 = ProtoRegistry.RegisterTech(
            TFE黑雾奖券, "T黑雾奖券", "黑雾奖券描述", "黑雾奖券结果", "Assets/fe/tech分馏奖券",
            [],
            [I黑雾矩阵], [800], 9000,
            [RFE黑雾奖券],
            GetTechPos(1, 7)
        );
        tech黑雾奖券.IsHiddenTech = true;
        tech黑雾奖券.PreItem = [I黑雾矩阵];
        tech黑雾奖券.PreTechsImplicit = [TFE分馏塔原胚];


        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [TFE分馏数据中心],
            [IFE分馏塔原胚I型], [20], 3600,
            [RFE分馏塔原胚定向],
            GetTechPos(2, 1)
        );
        tech分馏塔原胚.AddItems = [IFE交互塔, IFE分馏塔原胚I型, IFE分馏塔原胚II型, IFE分馏塔定向原胚];
        tech分馏塔原胚.AddItemCounts = [1, 30, 30, 20];
        tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
        tech分馏塔原胚.PropertyItemCounts = [100];

        var tech物品交互 = ProtoRegistry.RegisterTech(
            TFE物品交互, "T物品交互", "物品交互描述", "物品交互结果", "Assets/fe/tech物品交互",
            [],
            [IFE万物分馏科技解锁提示], [1], 3600000,
            [RFE交互塔],
            GetTechPos(2, 2)
        );
        tech物品交互.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品交互.PropertyOverrideItems = [I电磁矩阵];
        tech物品交互.PropertyItemCounts = [200];

        var tech矿物复制 = ProtoRegistry.RegisterTech(
            TFE矿物复制, "T矿物复制", "矿物复制描述", "矿物复制结果", "Assets/fe/tech矿物复制",
            [],
            [IFE万物分馏科技解锁提示], [1], 3600000,
            [RFE矿物复制塔],
            GetTechPos(2, 3)
        );
        tech矿物复制.PreTechsImplicit = [TFE分馏塔原胚];
        tech矿物复制.PropertyOverrideItems = [I电磁矩阵];
        tech矿物复制.PropertyItemCounts = [200];

        var tech增产点数聚集 = ProtoRegistry.RegisterTech(
            TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果", "Assets/fe/tech增产点数聚集",
            [],
            [IFE万物分馏科技解锁提示], [1], 3600000,
            [RFE点数聚集塔],
            GetTechPos(2, 4)
        );
        tech增产点数聚集.PreTechsImplicit = [TFE分馏塔原胚];
        tech增产点数聚集.PropertyOverrideItems = [I电磁矩阵];
        tech增产点数聚集.PropertyItemCounts = [200];

        // var tech量子复制 = ProtoRegistry.RegisterTech(
        //     TFE量子复制, "T量子复制", "量子复制描述", "量子复制结果", "Assets/fe/tech量子复制",
        //     [],
        //     [IFE万物分馏科技解锁提示], [1], 3600000,
        //     [RFE量子复制塔],
        //     GetTechPos(2, 5)
        // );
        // tech量子复制.PreTechsImplicit = [TFE分馏塔原胚];
        // tech量子复制.PropertyOverrideItems = [I电磁矩阵];
        // tech量子复制.PropertyItemCounts = [200];
        //
        // var tech物品点金 = ProtoRegistry.RegisterTech(
        //     TFE物品点金, "T物品点金", "物品点金描述", "物品点金结果", "Assets/fe/tech物品点金",
        //     [],
        //     [IFE万物分馏科技解锁提示], [1], 3600000,
        //     [RFE点金塔],
        //     GetTechPos(2, 6)
        // );
        // tech物品点金.PreTechsImplicit = [TFE分馏塔原胚];
        // tech物品点金.PropertyOverrideItems = [I电磁矩阵];
        // tech物品点金.PropertyItemCounts = [200];
        //
        // var tech物品分解 = ProtoRegistry.RegisterTech(
        //     TFE物品分解, "T物品分解", "物品分解描述", "物品分解结果", "Assets/fe/tech物品分解",
        //     [],
        //     [IFE万物分馏科技解锁提示], [1], 3600000,
        //     [RFE分解塔],
        //     GetTechPos(2, 7)
        // );
        // tech物品分解.PreTechsImplicit = [TFE分馏塔原胚];
        // tech物品分解.PropertyOverrideItems = [I电磁矩阵];
        // tech物品分解.PropertyItemCounts = [200];

        var tech物品转化 = ProtoRegistry.RegisterTech(
            TFE物品转化, "T物品转化", "物品转化描述", "物品转化结果", "Assets/fe/tech物品转化",
            [],
            [IFE万物分馏科技解锁提示], [1], 3600000,
            [RFE转化塔],
            GetTechPos(2, 8)
        );
        tech物品转化.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品转化.PropertyOverrideItems = [I电磁矩阵];
        tech物品转化.PropertyItemCounts = [200];
    }

    /// <summary>
    /// 根据输入的行列，生成科技所在位置。
    /// </summary>
    /// <param name="row">从0开始，数字越大越靠下</param>
    /// <param name="column">从0开始，数字越大越靠右</param>
    /// <returns></returns>
    private static Vector2 GetTechPos(int row, int column) {
        if (GenesisBook.Enable) {
            return new(9 + column * 4, -47 - row * 4);
        }
        if (OrbitalRing.Enable) {
            return new(8 + column * 4, -76 - row * 4);
        }
        return new(13 + column * 4, -67 - row * 4);
    }

    private static readonly bool[] techUnlockFlags = new bool[7];

    public static void ResetTechUnlockFlags() {
        Array.Clear(techUnlockFlags, 0, techUnlockFlags.Length);
    }

    /// <summary>
    /// 当分馏塔上传至数据中心时，将解锁标记置为true。
    /// </summary>
    public static void CheckTechUnlockCondition(int itemId) {
        if (itemId >= IFE交互塔 && itemId <= IFE转化塔) {
            techUnlockFlags[itemId - IFE交互塔] = true;
        }
    }

    /// <summary>
    /// 对于所有解锁标记为true的分馏塔，解锁对应科技。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.GameTick))]
    public static void Player_GameTick_Postfix() {
        for (int i = 0; i < techUnlockFlags.Length; i++) {
            if (techUnlockFlags[i]) {
                if (!GameMain.history.TechUnlocked(TFE物品交互 + i)) {
                    GameMain.history.UnlockTechUnlimited(TFE物品交互 + i, false);
                } else {
                    techUnlockFlags[i] = false;
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
        if (__instance.ID == TFE分馏数据中心) {
            __result = $"{"允许连接到分馏数据中心".Translate()}\r\n"
                       + $"{"给予一些分馏塔原胚".Translate()}";
            return false;
        }
        if (__instance.ID >= TFE超值礼包1 && __instance.ID <= TFE超值礼包6) {
            __result = $"{"一个物超所值的礼包".Translate()}";
            return false;
        }
        if (__instance.ID == TFE分馏塔原胚) {
            __result = $"{"解锁全部建筑培养配方".Translate()}\r\n"
                       + $"{"给予一个交互塔".Translate()}\r\n"
                       + $"{"给予一些分馏塔原胚".Translate()}";
            return false;
        }
        if (__instance.ID == TFE物品交互) {
            __result = $"{"自动上传被扔掉的物品".Translate()}\r\n"
                       + $"{"双击背包排序按钮，自动上传背包内物品".Translate()}";
            return false;
        }
        if (__instance.ID == TFE矿物复制) {
            __result = $"{"解锁部分矿物复制配方".Translate()}";
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock))]
    public static void GameHistoryData_NotifyTechUnlock_Postfix(int _techId) {
        if (_techId == TFE分馏塔原胚) {
            //解锁所有建筑培养配方
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.BuildingTrain)) {
                recipe.ChangeEchoCount();
            }
        } else if (_techId == TFE矿物复制) {
            //解锁非珍奇的原矿复制配方
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.MineralCopy)) {
                int itemID = recipe.InputID;
                ItemProto item = LDB.items.Select(itemID);
                if (recipe.RecipeType == ERecipe.MineralCopy
                    && (LDB.veins.dataArray.Any(vein => vein.MiningItem == itemID) || item.Type == EItemType.Resource)
                    && (itemID < I可燃冰 || itemID > I单极磁石)) {
                    recipe.ChangeEchoCount();
                }
            }
        }
    }
}
