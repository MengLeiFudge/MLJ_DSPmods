using System;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
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

        Register("T物品交互", "Item Interaction", "物品交互");
        Register("物品交互描述",
            $"COSMO has developed an advanced item transmission technology, in which the interaction tower is responsible for converting physical items into virtual data and vice versa. When the interaction tower is in interaction mode, input items are transmitted to the fractionation data centre in the form of data, and selected items are output in physical form. Additionally, the interaction tower is responsible for cultivating fractionator prototypes. When the interaction tower is in cultivation mode, it can cultivate non-specific fractionator prototypes into different types of fractionators.\n\n{"Upload an Interaction Tower to the Fractionation Data Centre to unlock this technology.".WithColor(Orange)}\n{"This technology is unlocked through a special method rather than normal research. Hover the placeholder item in the tech requirements to learn how to unlock it.".WithColor(Gold)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"主脑开发了一种高级的物品传输技术，其中交互塔承担了实体物品与虚拟数据互相转化的职责。交互塔处于交互模式时，输入的物品会以数据的形式传递到分馏数据中心，选择的物品会以实体形式输出。同时，交互塔也承担了培养分馏塔原胚的职责。交互塔处于培养模式时，可以将非定向的分馏塔原胚培养为不同的分馏塔。\n\n{"将交互塔上传至分馏数据中心即可解锁此科技。".WithColor(Orange)}\n{"该科技通过特殊方式解锁，而非通过研究。鼠标移至科技需求物品占位符上以了解如何解锁该科技。".WithColor(Gold)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("物品交互结果",
            "You have mastered the Item Interaction technology and can now use the item interaction tower to interact with the production line.",
            "你已经掌握了物品交互技术，可以用物品交互塔与产线交互了。");
        Register("自动上传被扔掉的物品", "Automatically upload dropped items");
        Register("双击背包排序按钮，自动上传背包内物品",
            "Double-click the backpack sort button to automatically upload the items within the backpack");

        Register("T矿物复制", "Mineral Replication", "矿物复制");
        Register("矿物复制描述",
            $"During the exploration of Icarus, the COSMO discovered that some star systems were extremely resource-poor and difficult to sustain. Mineral replication technology was the perfect solution to this problem, as it could replicate most minerals, allowing Icarus to easily explore barren star systems.\n\n{"Upload a Mineral Replication Tower to the Fractionation Data Centre to unlock this technology.".WithColor(Orange)}\n{"This technology is unlocked through a special method rather than normal research. Hover the placeholder item in the tech requirements to learn how to unlock it.".WithColor(Gold)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"在伊卡洛斯探索的过程中，主脑发现一些星区的资源极度匮乏，难以为继。矿物复制科技刚好可以解决这个问题，它能复制绝大多数矿物，让伊卡洛斯轻松探索贫瘠的星区。\n\n{"将矿物复制塔上传至分馏数据中心即可解锁此科技。".WithColor(Orange)}\n{"该科技通过特殊方式解锁，而非通过研究。鼠标移至科技需求物品占位符上以了解如何解锁该科技。".WithColor(Gold)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("矿物复制结果",
            "You have mastered the mineral replication technique and can now replicate minerals into multiple copies.",
            "你已经掌握了矿物复制技术，可以将矿物复制为多份了。");
        Register("解锁部分矿物复制配方", "Unlock some Mineral Replication recipes");

        Register("T增产点数聚集", "Proliferator Points Aggregate", "增产点数聚集");
        Register("增产点数聚集描述",
            $"Due to material limitations, proliferator technology has been unable to make further breakthroughs. However, proliferator point aggregation technology has solved this problem through fractionation. It can concentrate proliferator points onto specific items, thereby producing items that carry more proliferator points.\n\n{"Upload a Points Aggregate Tower to the Fractionation Data Centre to unlock this technology.".WithColor(Orange)}\n{"This technology is unlocked through a special method rather than normal research. Hover the placeholder item in the tech requirements to learn how to unlock it.".WithColor(Gold)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"增产剂科技因材料限制暂时无法突破，而增产点数聚集科技通过分馏的形式解决了此问题。它可以将增产点数集中到部分物品上，从而产出携带更多的增产点数的物品。\n\n{"将点数聚集塔上传至分馏数据中心即可解锁此科技。".WithColor(Orange)}\n{"该科技通过特殊方式解锁，而非通过研究。鼠标移至科技需求物品占位符上以了解如何解锁该科技。".WithColor(Gold)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("增产点数聚集结果",
            "You have mastered the technique of accumulating proliferator points, allowing items to carry more proliferator points.",
            "你已经掌握了增产点数聚集技术，可以让物品携带更多的增产点数了。");

        Register("T物品转化", "Item Conversion", "物品转化");
        Register("物品转化描述",
            $"Item conversion technology can convert items into other items related to them. According to COSMO, transformations follow the principle of equivalence, though in practice there seems to be more to it than that...\n\n{"Upload a Conversion Tower to the Fractionation Data Centre to unlock this technology.".WithColor(Orange)}\n{"This technology is unlocked through a special method rather than normal research. Hover the placeholder item in the tech requirements to learn how to unlock it.".WithColor(Gold)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"物品转化科技可以将物品转化成与其相关的其他物品。据主脑说，转化遵循等价原则，不过实际似乎不止这样……\n\n{"将转化塔上传至分馏数据中心即可解锁此科技。".WithColor(Orange)}\n{"该科技通过特殊方式解锁，而非通过研究。鼠标移至科技需求物品占位符上以了解如何解锁该科技。".WithColor(Gold)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("物品转化结果",
            "You have mastered the art of item conversion and can now convert items into other items related to them.",
            "你已经掌握了物品转化技术，可以将物品转化成与其相关的其他物品了。");

        Register("T物品精馏", "Item Rectification", "物品精馏");
        Register("物品精馏描述",
            $"Rectification technology can compress matrix-tier items into Fragments, providing a stable side resource for Growth and Focus systems. Higher-stage matrices and a stronger Rectification Tower both improve fragment output efficiency.\n\n{"Upload a Rectification Tower to the Fractionation Data Centre to unlock this technology.".WithColor(Orange)}\n{"This technology is unlocked through a special method rather than normal research. Hover the placeholder item in the tech requirements to learn how to unlock it.".WithColor(Gold)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"物品精馏科技可以将矩阵层级物品稳定压缩为残片，为成长与聚焦系统提供持续副资源。矩阵层级越高、精馏塔越强，残片转化效率越好。\n\n{"将精馏塔上传至分馏数据中心即可解锁此科技。".WithColor(Orange)}\n{"该科技通过特殊方式解锁，而非通过研究。鼠标移至科技需求物品占位符上以了解如何解锁该科技。".WithColor(Gold)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
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
}
