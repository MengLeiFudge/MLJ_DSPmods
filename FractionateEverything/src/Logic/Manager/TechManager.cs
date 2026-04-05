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
    private static readonly bool[] techUnlockFlags = new bool[7];

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
            "Super Value Gift Pack! Spend 100 Electromagnetic Matrix to receive Fragments and early-stage growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100电磁矩阵，即可获得残片与前期成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包1结果",
            "Fragments and early-stage growth supplies have been added to the fractionation data centre.",
            "残片与前期成长物资已下载到分馏数据中心。");
        Register("一个物超所值的礼包", "A great value package deal");

        Register("T超值礼包2", "Super Value Gift Pack 2", "超值礼包2");
        Register("超值礼包2描述",
            "Super Value Gift Pack! Spend 100 Energy Matrix to receive Fragments and early-stage growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100能量矩阵，即可获得残片与前期成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包2结果",
            "Fragments and early-stage growth supplies have been added to the fractionation data centre.",
            "残片与前期成长物资已下载到分馏数据中心。");

        Register("T超值礼包3", "Super Value Gift Pack 3", "超值礼包3");
        Register("超值礼包3描述",
            "Super Value Gift Pack! Spend 100 Structure Matrix to receive Fragments and mid-stage growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100结构矩阵，即可获得残片与中期成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包3结果",
            "Fragments and mid-stage growth supplies have been added to the fractionation data centre.",
            "残片与中期成长物资已下载到分馏数据中心。");

        Register("T超值礼包4", "Super Value Gift Pack 4", "超值礼包4");
        Register("超值礼包4描述",
            "Super Value Gift Pack! Spend 100 Information Matrix to receive Fragments and mid-stage growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100信息矩阵，即可获得残片与中期成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包4结果",
            "Fragments and mid-stage growth supplies have been added to the fractionation data centre.",
            "残片与中期成长物资已下载到分馏数据中心。");

        Register("T超值礼包5", "Super Value Gift Pack 5", "超值礼包5");
        Register("超值礼包5描述",
            "Super Value Gift Pack! Spend 100 Gravity Matrix to receive Fragments and late-stage growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100引力矩阵，即可获得残片与后期成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包5结果",
            "Fragments and late-stage growth supplies have been added to the fractionation data centre.",
            "残片与后期成长物资已下载到分馏数据中心。");

        Register("T超值礼包6", "Super Value Gift Pack 6", "超值礼包6");
        Register("超值礼包6描述",
            "Super Value Gift Pack! Spend 100 Universe Matrix to receive Fragments and endgame growth supplies.\n\nA tiny line in the bottom-right reads: final interpretation belongs to COSMO.",
            "超值礼包！只要100宇宙矩阵，即可获得残片与终局成长物资！\n\n（右下角有一行很小的字，上面写着：本活动解释权归主脑所有。）");
        Register("超值礼包6结果",
            "Fragments and endgame growth supplies have been added to the fractionation data centre.",
            "残片与终局成长物资已下载到分馏数据中心。");

        Register("T分馏塔原胚", "Fractionator Proto", "分馏塔原胚");
        Register("分馏塔原胚描述",
            "In the new fractionate technology, the new fractionators are no longer crafted directly with materials. Obtain various protos from the Proto Loop Pool or Growth page, then use the Interaction Tower to cultivate them into different fractionators. Rare directional protos can be cultivated directly into the chosen tower type.",
            "在新的分馏体系中，新分馏塔不再直接由材料制作。玩家需要从原胚闭环池或成长页获得不同原胚，再用交互塔培养成不同的分馏塔；稀有的定向原胚则可以直接培养为指定塔种。");
        Register("分馏塔原胚结果",
            "You have learned about the relevant information of the distillation tower precursor, and can combine different qualities of distillation tower precursor into directional distillation tower precursor.",
            "你已经了解了分馏塔原胚的相关信息，可以将分馏塔原胚培养为不同的分馏塔了。");
        Register("解锁全部建筑培养配方", "Unlock all building train recipes");
        Register("给予一个交互塔", "Provide a Interactive Tower");
        // Register("给予一些分馏塔原胚", "Provide some fractionator protos");//上面有了

        Register("T物品交互", "Item Interaction", "物品交互");
        Register("物品交互描述",
            $"COSMO has developed an advanced item transmission technology, in which the interaction tower is responsible for converting physical items into virtual data and vice versa. When the interaction tower is in interaction mode, input items are transmitted to the fractionation data centre in the form of data, and selected items are output in physical form. Additionally, the interaction tower is responsible for cultivating fractionator prototypes. When the interaction tower is in cultivation mode, it can cultivate non-specific fractionator prototypes into different types of fractionators.\n\n{"Upload the corresponding Fractionator to the Fractionation Data Centre".WithColor(Orange)} to unlock this technology, refer to the {"[G] key".WithColor(Orange)} guide for details.",
            $"主脑开发了一种高级的物品传输技术，其中交互塔承担了实体物品与虚拟数据互相转化的职责。交互塔处于交互模式时，输入的物品会以数据的形式传递到分馏数据中心，选择的物品会以实体形式输出。同时，交互塔也承担了培养分馏塔原胚的职责。交互塔处于培养模式时，可以将非定向的分馏塔原胚培养为不同的分馏塔。\n\n{"上传对应分馏塔至分馏数据中心".WithColor(Orange)}以解锁此科技，详情参考{"[G]键".WithColor(Orange)}指引。");
        Register("物品交互结果",
            "You have mastered the Item Interaction technology and can now use the item interaction tower to interact with the production line.",
            "你已经掌握了物品交互技术，可以用物品交互塔与产线交互了。");
        Register("自动上传被扔掉的物品", "Automatically upload dropped items");
        Register("双击背包排序按钮，自动上传背包内物品",
            "Double-click the backpack sort button to automatically upload the items within the backpack");

        Register("T矿物复制", "Mineral Replication", "矿物复制");
        Register("矿物复制描述",
            $"During the exploration of Icarus, the COSMO discovered that some star systems were extremely resource-poor and difficult to sustain. Mineral replication technology was the perfect solution to this problem, as it could replicate most minerals, allowing Icarus to easily explore barren star systems.\n\n{"Upload the corresponding Fractionator to the Fractionation Data Centre".WithColor(Orange)} to unlock this technology, refer to the {"[G] key".WithColor(Orange)} guide for details.",
            $"在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。矿物复制科技刚好可以解决这个问题，它能复制绝大多数矿物，让伊卡洛斯轻松探索贫瘠的星区。\n\n{"上传对应分馏塔至分馏数据中心".WithColor(Orange)}以解锁此科技，详情参考{"[G]键".WithColor(Orange)}指引。");
        Register("矿物复制结果",
            "You have mastered the mineral replication technique and can now replicate minerals into multiple copies.",
            "你已经掌握了矿物复制技术，可以将矿物复制为多份了。");
        Register("解锁部分矿物复制配方", "Unlock some Mineral Replication recipes");

        Register("T增产点数聚集", "Proliferator Points Aggregate", "增产点数聚集");
        Register("增产点数聚集描述",
            $"Due to material limitations, proliferator technology has been unable to make further breakthroughs. However, proliferator point aggregation technology has solved this problem through fractionation. It can concentrate proliferator points onto specific items, thereby producing items that carry more proliferator points.\n\n{"Upload the corresponding Fractionator to the Fractionation Data Centre".WithColor(Orange)} to unlock this technology, refer to the {"[G] key".WithColor(Orange)} guide for details.",
            $"增产剂科技因材料限制暂时无法突破，而增产点数聚集科技通过分馏的形式解决了此问题。它可以将增产点数集中到部分物品上，从而产出携带更多的增产点数的物品。\n\n{"上传对应分馏塔至分馏数据中心".WithColor(Orange)}以解锁此科技，详情参考{"[G]键".WithColor(Orange)}指引。");
        Register("增产点数聚集结果",
            "You have mastered the technique of accumulating proliferator points, allowing items to carry more proliferator points.",
            "你已经掌握了增产点数聚集技术，可以让物品携带更多的增产点数了。");

        Register("T物品转化", "Item Conversion", "物品转化");
        Register("物品转化描述",
            $"Item conversion technology can convert items into other items related to them. According to COSMO, transformations follow the principle of equivalence, though in practice there seems to be more to it than that...\n\n{"Upload the corresponding Fractionator to the Fractionation Data Centre".WithColor(Orange)} to unlock this technology, refer to the {"[G] key".WithColor(Orange)} guide for details.",
            $"物品转化科技可以将物品转化成与其相关的其他物品。据主脑说，转化遵循等价原则，不过实际似乎不止这样……\n\n{"上传对应分馏塔至分馏数据中心".WithColor(Orange)}以解锁此科技，详情参考{"[G]键".WithColor(Orange)}指引。");
        Register("物品转化结果",
            "You have mastered the art of item conversion and can now convert items into other items related to them.",
            "你已经掌握了物品转化技术，可以将物品转化成与其相关的其他物品了。");

        Register("T物品精馏", "Item Rectification", "物品精馏");
        Register("物品精馏描述",
            $"Rectification technology can compress matrix-tier items into Fragments, providing a stable side resource for Growth and Focus systems. Higher-stage matrices and a stronger Rectification Tower both improve fragment output efficiency.\n\n{"Upload the corresponding Fractionator to the Fractionation Data Centre".WithColor(Orange)} to unlock this technology, refer to the {"[G] key".WithColor(Orange)} guide for details.",
            $"物品精馏科技可以将矩阵层级物品稳定压缩为残片，为成长与聚焦系统提供持续副资源。矩阵层级越高、精馏塔越强，残片转化效率越好。\n\n{"上传对应分馏塔至分馏数据中心".WithColor(Orange)}以解锁此科技，详情参考{"[G]键".WithColor(Orange)}指引。");
        Register("物品精馏结果",
            "You have mastered Rectification technology and can now compress matrix-tier items into Fragments.",
            "你已经掌握了物品精馏技术，可以将矩阵层级物品稳定压缩为残片了。");


        Register("T行星内物流交互", "Planetary Logistics Interaction", "行星内物流交互");
        Register("行星内物流交互描述",
            "Planetary Logistics Interaction lets local logistics stations exchange items directly with the Fractionation Data Centre, reducing repeated manual hauling within the same planet.",
            "行星内物流交互科技允许本地物流站直接与分馏数据中心交换物品，减少同星球范围内的重复搬运与手动中转。");
        Register("行星内物流交互结果",
            "You have mastered planetary logistics interaction and can now let local logistics stations interact with the Fractionation Data Centre.",
            "你已经掌握了行星内物流交互技术，现在可以让本地物流站与分馏数据中心直接交互。");

        Register("T星际物流交互", "Interstellar Logistics Interaction", "星际物流交互");
        Register("星际物流交互描述",
            "Interstellar Logistics Interaction extends the same direct data-centre exchange to interstellar logistics stations, turning them into long-range item interaction hubs.",
            "星际物流交互科技把同样的直连交互能力扩展到星际物流站，使其成为跨星系的物资交互中枢。");
        Register("星际物流交互结果",
            "You have mastered interstellar logistics interaction and can now let interstellar logistics stations interact with the Fractionation Data Centre.",
            "你已经掌握了星际物流交互技术，现在可以让星际物流站与分馏数据中心直接交互。");
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
        tech分馏数据中心.AddItems = [IFE交互塔原胚];
        tech分馏数据中心.AddItemCounts = [80];//20用于解锁分馏塔原胚科技，60赠送
        tech分馏数据中心.PropertyOverrideItems = [I电磁矩阵];
        tech分馏数据中心.PropertyItemCounts = [10];
        tech分馏数据中心.IconTag = "flsjzx";


        var tech超值礼包1 = ProtoRegistry.RegisterTech(
            TFE超值礼包1, "T超值礼包1", "超值礼包1描述", "超值礼包1结果", "Assets/fe/tech超值礼包",
            [TFE分馏数据中心],
            [I电磁矩阵], [100], 3600,
            [],
            GetTechPos(0, 1)
        );
        tech超值礼包1.PreTechsImplicit = [TFE物品交互];
        tech超值礼包1.AddItems = [IFE残片, IFE交互塔原胚];
        tech超值礼包1.AddItemCounts = [300, 20];
        tech超值礼包1.PropertyOverrideItems = [I电磁矩阵];
        tech超值礼包1.PropertyItemCounts = [100];
        tech超值礼包1.IconTag = "tczlb1";

        var tech超值礼包2 = ProtoRegistry.RegisterTech(
            TFE超值礼包2, "T超值礼包2", "超值礼包2描述", "超值礼包2结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包1],
            [I能量矩阵], [100], 3600,
            [],
            GetTechPos(0, 2)
        );
        tech超值礼包2.PreTechsImplicit = [T能量矩阵];
        tech超值礼包2.AddItems = [IFE残片, IFE矿物复制塔原胚];
        tech超值礼包2.AddItemCounts = [400, 20];
        tech超值礼包2.PropertyOverrideItems = [I能量矩阵];
        tech超值礼包2.PropertyItemCounts = [100];
        tech超值礼包2.IconTag = "tczlb2";

        var tech超值礼包3 = ProtoRegistry.RegisterTech(
            TFE超值礼包3, "T超值礼包3", "超值礼包3描述", "超值礼包3结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包2],
            [I结构矩阵], [100], 3600,
            [],
            GetTechPos(0, 3)
        );
        tech超值礼包3.PreTechsImplicit = [T结构矩阵];
        tech超值礼包3.AddItems = [IFE残片, IFE点数聚集塔原胚];
        tech超值礼包3.AddItemCounts = [500, 10];
        tech超值礼包3.PropertyOverrideItems = [I结构矩阵];
        tech超值礼包3.PropertyItemCounts = [100];
        tech超值礼包3.IconTag = "tczlb3";

        var tech超值礼包4 = ProtoRegistry.RegisterTech(
            TFE超值礼包4, "T超值礼包4", "超值礼包4描述", "超值礼包4结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包3],
            [I信息矩阵], [100], 3600,
            [],
            GetTechPos(0, 4)
        );
        tech超值礼包4.PreTechsImplicit = [T信息矩阵];
        tech超值礼包4.AddItems = [IFE残片, IFE转化塔原胚];
        tech超值礼包4.AddItemCounts = [600, 10];
        tech超值礼包4.PropertyOverrideItems = [I信息矩阵];
        tech超值礼包4.PropertyItemCounts = [100];
        tech超值礼包4.IconTag = "tczlb4";

        var tech超值礼包5 = ProtoRegistry.RegisterTech(
            TFE超值礼包5, "T超值礼包5", "超值礼包5描述", "超值礼包5结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包4],
            [I引力矩阵], [100], 3600,
            [],
            GetTechPos(0, 5)
        );
        tech超值礼包5.PreTechsImplicit = [T引力矩阵];
        tech超值礼包5.AddItems = [IFE残片, IFE精馏塔原胚];
        tech超值礼包5.AddItemCounts = [800, 10];
        tech超值礼包5.PropertyOverrideItems = [I引力矩阵];
        tech超值礼包5.PropertyItemCounts = [100];
        tech超值礼包5.IconTag = "tczlb5";

        var tech超值礼包6 = ProtoRegistry.RegisterTech(
            TFE超值礼包6, "T超值礼包6", "超值礼包6描述", "超值礼包6结果", "Assets/fe/tech超值礼包",
            [TFE超值礼包5],
            [I宇宙矩阵], [100], 3600,
            [],
            GetTechPos(0, 6)
        );
        tech超值礼包6.PreTechsImplicit = [T宇宙矩阵];
        tech超值礼包6.AddItems = [IFE残片, IFE分馏塔定向原胚];
        tech超值礼包6.AddItemCounts = [1200, 2];
        tech超值礼包6.PropertyOverrideItems = [I宇宙矩阵];
        tech超值礼包6.PropertyItemCounts = [100];
        tech超值礼包6.IconTag = "tczlb6";

        var tech分馏塔原胚 = ProtoRegistry.RegisterTech(
            TFE分馏塔原胚, "T分馏塔原胚", "分馏塔原胚描述", "分馏塔原胚结果", "Assets/fe/tech分馏塔原胚",
            [TFE分馏数据中心],
            [IFE交互塔原胚], [20], 3600,
            [],
            GetTechPos(1, 1)
        );
        tech分馏塔原胚.AddItems = [IFE交互塔, IFE交互塔原胚, IFE矿物复制塔原胚, IFE分馏塔定向原胚];
        tech分馏塔原胚.AddItemCounts = [1, 30, 30, 20];
        tech分馏塔原胚.PropertyOverrideItems = [I电磁矩阵];
        tech分馏塔原胚.PropertyItemCounts = [100];
        tech分馏塔原胚.IconTag = "tfltyp";

        var tech物品交互 = ProtoRegistry.RegisterTech(
            TFE物品交互, "T物品交互", "物品交互描述", "物品交互结果", "Assets/fe/tech物品交互",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE交互塔],
            GetTechPos(1, 2)
        );
        tech物品交互.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品交互.PropertyOverrideItems = [I电磁矩阵];
        tech物品交互.PropertyItemCounts = [200];
        tech物品交互.IconTag = "twpjh";

        var tech矿物复制 = ProtoRegistry.RegisterTech(
            TFE矿物复制, "T矿物复制", "矿物复制描述", "矿物复制结果", "Assets/fe/tech矿物复制",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE矿物复制塔],
            GetTechPos(1, 3)
        );
        tech矿物复制.PreTechsImplicit = [TFE分馏塔原胚];
        tech矿物复制.PropertyOverrideItems = [I电磁矩阵];
        tech矿物复制.PropertyItemCounts = [200];
        tech矿物复制.IconTag = "tkwfz";

        var tech增产点数聚集 = ProtoRegistry.RegisterTech(
            TFE增产点数聚集, "T增产点数聚集", "增产点数聚集描述", "增产点数聚集结果", "Assets/fe/tech增产点数聚集",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE点数聚集塔],
            GetTechPos(1, 4)
        );
        tech增产点数聚集.PreTechsImplicit = [TFE分馏塔原胚];
        tech增产点数聚集.PropertyOverrideItems = [I电磁矩阵];
        tech增产点数聚集.PropertyItemCounts = [200];
        tech增产点数聚集.IconTag = "zcdsjj";

        var tech物品转化 = ProtoRegistry.RegisterTech(
            TFE物品转化, "T物品转化", "物品转化描述", "物品转化结果", "Assets/fe/tech物品转化",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE转化塔],
            GetTechPos(1, 5)
        );
        tech物品转化.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品转化.PropertyOverrideItems = [I电磁矩阵];
        tech物品转化.PropertyItemCounts = [200];
        tech物品转化.IconTag = "twpzh";

        var tech物品精馏 = ProtoRegistry.RegisterTech(
            TFE物品精馏, "T物品精馏", "物品精馏描述", "物品精馏结果", "Assets/fe/tech物品分解",
            [],
            [IFE万物分馏科技解锁说明], [1], 3600000,
            [RFE精馏塔],
            GetTechPos(1, 6)
        );
        tech物品精馏.PreTechsImplicit = [TFE分馏塔原胚];
        tech物品精馏.PropertyOverrideItems = [I电磁矩阵];
        tech物品精馏.PropertyItemCounts = [200];
        tech物品精馏.IconTag = "twpjl";

        var tech行星物流系统 = LDB.techs.Select(T行星物流系统);
        var tech行星内物流交互 = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果", tech行星物流系统.IconPath,
            [],
            [..tech行星物流系统.Items], [..tech行星物流系统.ItemPoints], tech行星物流系统.HashNeeded,
            [RFE行星内物流交互站],
            GetTechPos(1, 7)
        );
        tech行星内物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE物品交互, tech行星物流系统.ID];
        tech行星内物流交互.PropertyOverrideItems = [..tech行星物流系统.PropertyOverrideItems];
        tech行星内物流交互.PropertyItemCounts = [..tech行星物流系统.PropertyItemCounts];
        tech行星内物流交互.IconTag = "txxnjh";

        var tech星际物流系统 = LDB.techs.Select(T星际物流系统);
        var tech星际物流交互 = ProtoRegistry.RegisterTech(
            TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果", tech星际物流系统.IconPath,
            [],
            [..tech星际物流系统.Items], [..tech星际物流系统.ItemPoints], tech星际物流系统.HashNeeded,
            [RFE星际物流交互站],
            GetTechPos(1, 8)
        );
        tech星际物流交互.PreTechsImplicit = [TFE分馏塔原胚, TFE物品交互, tech星际物流系统.ID];
        tech星际物流交互.PropertyOverrideItems = [..tech星际物流系统.PropertyOverrideItems];
        tech星际物流交互.PropertyItemCounts = [..tech星际物流系统.PropertyItemCounts];
        tech星际物流交互.IconTag = "txjjh";
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

    /// <summary>
    /// 判断某一主线矩阵层的有限科技是否已全部研究完成。
    /// 隐藏科技与无限科技不参与该判定。
    /// </summary>
    public static bool IsMatrixTierFullyResearched(int matrixId) {
        if (GameMain.history == null) {
            return false;
        }

        bool hasFiniteTech = false;
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                continue;
            }
            if (tech.MaxLevel > 20) {
                continue;
            }
            if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                continue;
            }

            hasFiniteTech = true;
            if (!GameMain.history.TechUnlocked(tech.ID, true)) {
                return false;
            }
        }

        return hasFiniteTech;
    }

    public static float GetMatrixTierResearchProgress(int matrixId) {
        if (GameMain.history == null) {
            return 0f;
        }

        int total = 0;
        int unlocked = 0;
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                continue;
            }
            if (tech.MaxLevel > 20) {
                continue;
            }
            if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                continue;
            }

            total++;
            if (GameMain.history.TechUnlocked(tech.ID, true)) {
                unlocked++;
            }
        }

        if (total <= 0) {
            return 0f;
        }
        return unlocked / (float)total;
    }

    /// <summary>
    /// 原版配方增强采用“落后一层”的阶段开放规则：
    /// 只有下一层矩阵已解锁，且该层有限科技全部研究完成时，才开放低一层增强。
    /// </summary>
    public static bool IsVanillaEnhancementUnlockedForMatrix(int matrixId) {
        int stageIndex = ItemManager.GetMatrixStageIndex(matrixId);
        if (stageIndex < 0 || stageIndex >= ItemManager.MainProgressMatrixIds.Length) {
            return false;
        }

        int requiredIndex = Mathf.Min(stageIndex + 1, ItemManager.MainProgressMatrixIds.Length - 1);
        int requiredMatrixId = ItemManager.MainProgressMatrixIds[requiredIndex];
        if (GameMain.history == null || !GameMain.history.ItemUnlocked(requiredMatrixId)) {
            return false;
        }
        return IsMatrixTierFullyResearched(requiredMatrixId);
    }

    public static int GetHighestUnlockedVanillaEnhancementMatrix() {
        for (int i = ItemManager.MainProgressMatrixIds.Length - 1; i >= 0; i--) {
            int matrixId = ItemManager.MainProgressMatrixIds[i];
            if (IsVanillaEnhancementUnlockedForMatrix(matrixId)) {
                return matrixId;
            }
        }

        return 0;
    }

    public static void ResetTechUnlockFlags() {
        Array.Clear(techUnlockFlags, 0, techUnlockFlags.Length);
    }

    /// <summary>
    /// 当分馏塔上传至数据中心时，将解锁标记置为true。
    /// </summary>
    public static void CheckTechUnlockCondition(int itemId) {
        if (itemId >= IFE交互塔 && itemId <= IFE精馏塔) {
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
        if (__instance.ID >= TFE超值礼包1 && __instance.ID <= TFE超值礼包9) {
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
                recipe.RewardThis();
            }
        } else if (_techId == TFE矿物复制) {
            //解锁非珍奇的原矿复制配方
            foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.MineralCopy)) {
                int itemID = recipe.InputID;
                ItemProto item = LDB.items.Select(itemID);
                if (recipe.RecipeType == ERecipe.MineralCopy
                    && (LDB.veins.dataArray.Any(vein => vein.MiningItem == itemID) || item.Type == EItemType.Resource)
                    && (itemID < I可燃冰 || itemID > I单极磁石)) {
                    recipe.RewardThis();
                }
            }
        }
    }
}
