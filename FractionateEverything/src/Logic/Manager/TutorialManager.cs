using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class TutorialManager {
    static MethodInfo genesisBookIsLayoutMethod;
    static MethodInfo genesisBookGetLayoutMethod;
    static bool genesisBookLayoutMethodsInitialized;

    public static void AddTranslations() {
        Register("万物分馏简介标题", "Fractionate Everything", "万物分馏简介");
        Register("万物分馏简介前字",
            $"""
             With the huge amount of arithmetic power in each star zone, the Fractionation Technology has seen a major breakthrough. The Mastermind believes that the new Fractionation Technology can bring great convenience to Icarus, greatly increasing the speed of exploration and construction. Now, the new department 'Fractionation Data Centre' has been established, and the related technology has been distributed to all Icarus.

             However, you still need to do some preparatory work to unlock the appropriate permissions. Here is a short guide: 
             1. Research the 'Fractionation Data Centre' technology to unlock access to the Fractionation Data Centre.
             After researching, press {"[Shift + F] key".WithColor(Orange)} to connect to the Fractionation Data Centre.
             2. Research the 'Fractionator Proto' tech and read the tech description carefully to learn how to use the Interactive Tower to grow Raw Embryos into new Fractionation Towers, as well as how to use the Interactive Tower to upload items.
             There will also be a new guide that explains how to use the Interactive Tower after you build it.
             3. Cultivate a new Interactive Tower and upload it to the Fractionation Data Centre to unlock the 'Item Interaction' technology.
             This is an extremely powerful support feature that defies space limitations and can be thought of as an external backpack with unlimited capacity.
             4. Build the production line for the current-stage Matrix and spend the matrices directly in the Opening Pool or Proto Loop Pool.
             Fragments then become the long-term resource for growth catch-up and focus switching.

             By the way, there is one thing you should remember: you can always revisit all the guidelines by pressing {"[G] key".WithColor(Orange)}.

             {"Have fun with fractionation!".WithColor(Blue)}
             """,
            $"""
             在各个星区巨额算力的加持下，分馏科技迎来了重大突破。主脑认为，全新的分馏科技可以为伊卡洛斯带来极大的便利，从大幅提升探索和建设的速度。现在，新部门“分馏数据中心”已经建立，相关科技也下发给了各位伊卡洛斯。

             不过，你还需要做一些准备工作，才能解锁相应的权限。以下是一个简短的前期指引：
             1.研究“分馏数据中心”科技，解锁连接到分馏数据中心的权限。
             研究完成后，按{"[Shift + F]键".WithColor(Orange)}即可连接到分馏数据中心。
             2.研究“分馏塔原胚”科技，仔细阅读科技说明，了解如何使用交互塔将原胚培养为新的分馏塔，以及如何使用交互塔上传物品。
             建设交互塔之后，也会有新的指引对此进行讲解。
             3.培养出新的交互塔，并将其上传至分馏数据中心，解锁“物品交互”科技。
             这是一项极其强大的辅助功能，无视空间限制，你可以将它视为具有无限容量的外部背包。
             4.搭建当前阶段矩阵产线，并直接把矩阵投入开线池或原胚闭环池。
             残片则主要通过精馏矩阵、任务与成就获得，用于成长和聚焦。

             对了，有一件事情你要记住：你可以随时按{"[G]键".WithColor(Orange)}重新查阅所有指引。

             {"尽情享受分馏的乐趣吧！".WithColor(Blue)}
             """);
        Register("万物分馏简介后字", "", "");

        Register("分馏数据中心标题", "Fractionation data centre", "分馏数据中心");
        Register("分馏数据中心前字",
            $"""
             You can now connect to the Fractionation Data Centre by pressing {"[Shift + F] key".WithColor(Orange)}.
             This is a new master control panel that allows you to easily manage Fractionation Technology related content.

             {"[How to Use (simple version)]".WithColor(Blue)}
             The current 2.3 loop is organised around four keywords: Opening, Proto, Growth, and Focus.
             1. Build the production line for the current-stage Matrix. Matrices are spent directly for draws in version 2.3; physical tickets are no longer the main currency.
             2. Spend the current-stage Matrix in the Opening Pool or the Proto Loop Pool. The Opening Pool unlocks new recipes and line branches, while the Proto Loop Pool provides tower protos and directional protos.
             3. Feed matrices into the Rectification Tower to compress them into Fragments. Fragments, together with Growth Points, are then used on the Growth page for deterministic catch-up and breakthroughs.
             4. Change the Focus layer according to your current build direction. Focus does not create a new pool by itself; it biases Opening, Proto, and Growth outcomes toward the chosen route.
             5. Recipe growth and building growth are now split: recipes care about unlocks, levels, and full-upgrade progress, while buildings care about tower-type EXP and key breakthroughs.

             {"[Recipe Operation]".WithColor(Blue)} 
             This page lets you inspect any FE recipe and its current progress.
             1. Recipes are mainly obtained from the Opening Pool, the Growth page, or by spending Fractionation Recipe Cores.
             2. Recipe levels increase by actually running the corresponding line. Sand can still be used as a direct EXP shortcut.
             3. The gallery and long-term progression now care more about whether a recipe is unlocked and fully upgraded than about old ticket-era pool bookkeeping.
             4. Equivalent Output remains the best place to quantify expected outputs after recipe, tower, and proliferator bonuses are applied.

             {"[Building Operations]".WithColor(Blue)} 
             On the Building Operations page, you improve Fractionation Towers and Logistics Interaction Stations.
             1. Buildings use tower-type global growth rather than per-building training.
             2. Regular levels mainly come from tower EXP accumulated by use.
             3. Key breakthroughs consume the current-stage Matrix plus Fragments.
             4. Logistics Interaction Stations and Fractionation Towers follow the same growth philosophy, but gain EXP from different actions.

             {"[Item Interaction]".WithColor(Blue)} 
             Item Interaction is an extremely powerful and convenient feature, much loved by various Icarus.

             Under the influence of item interaction technology, most actions can be performed using items stored in the Fractionation Data Centre, rather than having to extract them from the backpack and use them again. Examples include: 
             Fabrication Table to manually craft items; Quick Build Bar to select a building; Tech to manually research; Building TAB to fill items; Fuel Fill; Warp Fill; Ammo Fill; Drone Fill ...... and many more.
             However, to use these handy features, you need to unlock the Item Interaction tech before storing a sufficient number of items to the Fractionation Data Centre.

             How to upload items:
             1. The Interaction Tower will upload items entered on the front interface. Check the [Fractionation Tower User Guide] guide for more information.
             2. The Logistics Interactive Station will upload items that meet certain conditions. Check the 'Logistics Interactive Station User's Guide' for more information.
             3. Some draw rewards and growth rewards will be uploaded automatically.
             4. When you double-click the backpack button, all items in your backpack will be uploaded (you need to unlock the 'Item Interaction' technology, and your logistics backpack will not be affected).

             How to download items:
             1. On the Item Interaction or Important Items page, left-click or right-click the corresponding item to extract it. You can set the number of groups to be extracted on the Miscellaneous Settings page.
             2. Logistics Interactive Station downloads items that meet certain conditions. For more information, refer to the 'Logistics Interactive Station User's Guide'.
             3. Various fill operations (manual creation, manual research, TAB fill, fuel fill, etc.) will automatically download items and use them.

             {"[Draw System]".WithColor(Blue)}
             The current draw structure is "three pools plus one focus layer":
             1. Opening Pool: spends the current-stage Matrix to unlock new recipes and line branches.
             2. Proto Loop Pool: spends the current-stage Matrix to obtain tower protos and directional protos.
             3. Growth Page: deterministic rather than random, used for catch-up, breakthroughs, and some Dark Fog side-branch offers.
             4. Focus Layer: not a standalone pool. It biases the Opening Pool, Proto Loop Pool, and Growth offers toward the selected direction.

             {"[Growth Planning]".WithColor(Blue)}
             The old Limited Time Store has been reworked into Growth Planning and Resource Coordination.
             Its main jobs now are:
             - spend Growth Points and Fragments for deterministic catch-up
             - use the current-stage Matrix as the direct draw cost
             - use Dark Fog Matrix offers as the entry point for the Dark Fog side branch

             {"[Quest System]".WithColor(Blue)} 
             Progress is now split into three layers:
             - Main Tasks: first unlocks and stage progression
             - Recurring Tasks: stable supplemental rewards
             - Achievement System: long-term milestones and global passive bonuses

             {"[Recipe Gallery]".WithColor(Blue)} 
             The recipe gallery now focuses on three totals: Fully Upgraded, Unlocked, and Total.
             Fully Upgraded means the recipe has reached its current full-upgrade state; the gallery no longer uses old "maximum reverberation" wording as a separate headline metric.
             """,
            $"""
             现在，你可以按{"[Shift + F]键".WithColor(Orange)}连接到分馏数据中心。
             这是一个全新的总控面板，可以让你方便地管理分馏科技相关内容。

             {"【使用简介（太长不看版）】".WithColor(Blue)}
             面板有很多功能，其中当前你必须理解的核心是“开线、原胚、成长、聚焦”这四组系统。一般而言，推进流程是这样的：
             1.搭建当前阶段矩阵产线。矩阵将直接作为抽取资源，不再需要实体奖券。
             2.把矩阵投入“开线池”或“原胚闭环池”。前者负责新配方，后者负责原胚与定向原胚。
             3.通过精馏塔把矩阵稳定压缩为残片，再配合池积分进入“成长规划”。
             4.根据你想走的路线切换“流派聚焦”，让对应方向的配方或原胚更容易出现。
             5.配方和建筑成长现在分流处理：配方侧强调解锁、升级与完全升级；建筑侧强调塔种经验与关键节点突破。

             {"【配方操作】".WithColor(Blue)}
             在配方操作页面中，你可以查询任何分馏配方的当前状态，并对它们进行操作。

             选择想要查看的配方类型后，左键或右键单击物品图标，即可切换配方。左键单击后，会显示所选类型下的当前已解锁的所有配方；右键单击后，会显示所选类型下的所有配方。
             分馏配方类型与分馏塔是一一对应的。例如，建筑培养配方只能由交互塔处理，交互塔也只能处理建筑培养配方。点数聚集塔是一个特例，它不需要分馏配方。

             1.配方解锁
             在对应配方解锁后，你才可以对相应物品进行处理。
             注意，即使是相同的物品，也有不同的配方。例如，[矿物复制-铁矿]和[点金-铁矿]是不同的配方，它们之间没有任何关联。铁矿输入矿物复制塔，将会根据[矿物复制-铁矿]配方进行处理；铁矿输入点金塔，将会根据[点金-铁矿]配方进行处理。
             一个配方刚解锁时，它的输出信息是隐藏的。你需要搭建对应产线并使用此配方，之后相关信息会逐渐解锁。你也可以在设置中选择直接显示配方的具体信息。
             配方的获取途径有：开线抽取、成长规划补差、直接使用分馏配方核心兑换（配方操作页面上方按钮）。

             2.配方经验、等级与升级
             每个配方都有等级，解锁后等级为1。等级越高，配方效果也就越强。等级上限受品质影响。
             使用分馏塔处理对应物品时，分馏配方将持续获得经验。你也可以直接用沙土兑换经验（配方操作页面上方按钮）。
             经验达到升级所需的数值后，配方将自动提升到下一个等级，直至到达当前品质对应的等级上限。

             3.完全升级与长期推进
             当前 2.3 口径更关注“是否解锁、是否完全升级、是否补齐关键成长项”。
             因此，任务、成就、图鉴和成长规划会更多围绕这些长期指标来组织，而不是围绕旧奖券时代的抽池文案。

             4.等效输出
             等效输出可以在一定程度上帮助你进行量化。
             首先选择输入物品的平均增产点数。一般而言，增产剂MKI、II、III可以使物品携带1、2、4点增产点数。
             然后你就可以看到，将携带指定的增产点数的1个原料投入到对应分馏塔中，最终可以得到的产物数目。
             这个数值已经计算了各种加成（包括配方本身加成、分馏塔加成等等），它是准确的期望。

             {"【建筑操作】".WithColor(Blue)}
             在建筑操作页面中，你可以提升分馏塔、物流交互站的效果。

             由于每个建筑的成长项都不一样，这里只讲几个关键原则：
             1.建筑采用塔种全局成长，而不是单个建筑逐台培养。
             2.普通等级主要依赖塔种经验自动成长。
             3.关键节点使用“残片 + 当前阶段矩阵”突破。
             4.物流交互站与分馏塔仍然共享同一套全局成长思路，只是经验来源不同。

             {"【物品交互】".WithColor(Blue)}
             物品交互是一项极其强大的便利功能，深受各个伊卡洛斯的喜爱。

             在物品交互科技的影响下，绝大多数操作都可以直接使用分馏数据中心存储的物品，而不需要提取物品到背包再使用。例如：
             制造台手动制作物品；快捷建造栏选择建筑；科技手动研究；建筑TAB填充物品；燃料填充；翘曲填充；弹药填充；无人机填充……等等。
             不过，要想使用这些便利的功能，你需要先解锁物品交互科技，再向分馏数据中心存储足够数目的物品。

             如何上传物品：
             1.交互塔会上传正面接口输入的物品。查阅【分馏塔使用指南】指引以了解更多信息。
             2.物流交互站会上传满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.部分抽取奖励、成长奖励会自动上传到分馏数据中心。
             4.双击整理背包按钮时，背包内的物品会全部上传（需解锁“物品交互”科技，物流背包不受影响）。

             如何下载物品：
             1.在物品交互或重要物品页面，左键或右键点击对应物品即可提取。可以在杂项设置页面设定提取的组数。
             2.物流交互站会下载满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.各种填充操作（手动制作、手动研究、TAB填充、燃料填充等等）会自动下载物品并使用。

             {"【抽取系统】".WithColor(Blue)}
             抽取系统现在围绕“三池一层”展开：

             1.开线池
             主要负责矿物复制、转化以及未来真正承担“开新线”的分馏配方。
             消耗当前阶段矩阵，强调“抽到之后值得回去重搭一条线”。

             2.原胚闭环池
             主要负责各类原胚、定向原胚以及原胚闭环相关奖励。
             这是分馏塔原胚的核心来源。

             3.成长规划
             属于非随机入口，主要负责定向补差、关键节点突破、黑雾支线报价等内容。

             4.流派聚焦
             不是独立奖池，而是对开线池、原胚闭环池、成长规划进行方向加权。
             速通模式下，聚焦会更激进地强化被选中的路线，并压低其他路线。

             {"【限时商店】".WithColor(Blue)}
             当前版本中，传统“限时商店”已被重构为成长规划与资源统筹。
             重点不再是随机刷新货物，而是：
             - 用池积分和残片做定向补差
             - 用当前阶段矩阵承担抽取消耗
             - 用黑雾矩阵接入黑雾支线成长报价

             {"【任务系统】".WithColor(Blue)}
             任务系统已经拆分为：
             - 主线任务：首次解锁、阶段推进
             - 循环任务：稳定补给
             - 成就系统：里程碑与全局长期被动

             {"【配方图鉴】".WithColor(Blue)}
             此处可以查看配方的整体情况，当前统一展示的是“完全升级 / 已解锁 / 总数”三项数据。
             图鉴页面现在主要负责展示与统计；相关全局增幅会更多迁入成就系统统一汇总，而不再单独强调“最大回响”这类旧指标。
             """
        );
        Register("分馏数据中心后字", "", "");


        Register("分馏塔使用指南标题", "Fractionator guidelines", "分馏塔使用指南");
        Register("分馏塔使用指南前字",
            $"""
             {"[Cultivate Fractionation Tower]".WithColor(Blue)} 
             In the new Fractionation Technology, Fractionation Towers are no longer obtained by manufacturing, but mainly by cultivating them in Interactive Towers.
             Simply put, by using the Interactive Tower to fractionate non-directional 'Fractionation Tower Raw Blanks', you can get different Fractionation Towers, and at the same time, there is a small chance that you can get 'Fractionation Tower Directional Raw Blanks'.
             There are 5 types of non-targeted proto-germs, and their products are as follows (the introductory information of the proto-germs also shows the type of product): 
             Type I: Mineral Replication Tower (96%), Fractionator Directed Proto (4%) 
             Type II: Interaction Tower (96%), Fractionator Directed Proto (4%) 
             Type III: Alchemy Tower (32%), Deconstruction Tower (32%), Conversion Tower (32%), Fractionator Directed Proto (4%)
             Type IV: Points Aggregate Tower (96%), Fractionator Directed Proto (4%) 
             Type V: Quantum Replication Tower (96%), Fractionator Directed Proto (4%) 
             Note that {"Only one type of item can be processed by any Fractionation Tower at any one time".WithColor(Orange)}, so don't mix the different types of Protoembryo!
             Items can be uploaded to the Fractionation Data Centre by feeding the output Fractionation Tower through a conveyor belt to the front interface of another Interactive Tower, thus unlocking the corresponding tech.
             Uploading different Fractionation Towers will unlock different techs. For example, uploading a Mineral Replication Tower will unlock the Mineral Replication tech, and uploading an Interaction Tower will unlock the Item Interaction tech.
             Note that only Interactive Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. This means that there can be no items inside the tower, and the left and right ports cannot be connected to a conveyor belt.
             Directional prototypes can be crafted directly into the specified fractionation tower without being processed by the interaction tower.

             {"[Interaction Tower]".WithColor(Blue)} 
             The Interaction Tower has two functions: to grow embryos into various fractionation towers, and to upload items.
             Only Interactive Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. There is no limit to the upload rate, meaning that a single tower can upload a full belt of items.

             {"[Mineral Replication Tower]".WithColor(Blue)} 
             Mineral Replication Towers can replicate various minerals in multiples. This is helpful for resource-poor star zones.

             {"[Points Aggregate Tower]".WithColor(Blue)} 
             The Points Aggregate Tower allows you to adjust the number of points for input items, focusing points on some items to break through the limitations of the Increaser.
             The initial product has only 4 points of production enhancers. You can increase the number of points you can produce by selecting Points Aggregate Tower on the Building Operations page and upgrading the 'Point Aggregation Efficiency Level', up to a maximum of 10 points.

             {"[Quantum Replication Tower]".WithColor(Blue)} 
             The Quantum Replication Tower can replicate most items.
             All types of Essence are consumed when duplicating, and any Essence consumed is deducted directly from the Fractionation Data Centre. Lack of any type of Essence will result in all Quantum Replication Towers being unable to fractionate the item.
             The higher the value of the item, the more Essence is consumed. The higher the average increase in production points of an ingredient, the less Essence is consumed.

             {"[Alchemy Tower]".WithColor(Blue)} 
             The Alchemy Tower converts items to the corresponding level of the matrix.

             {"[Deconstruction Tower]".WithColor(Blue)} 
             The Deconstruction Tower can decompose an item into the raw materials used to craft it. If the item being decomposed does not have a corresponding single-product recipe, it will be processed as sand.

             {"[Conversion Tower Mutual Tower]".WithColor(Blue)} 
             The Conversion Tower can convert items into other related items.
             """,
            $"""
             {"【培养分馏塔】".WithColor(Blue)}
             在新的分馏科技中，分馏塔不再通过制造得到，而是主要通过交互塔培养得到。
             简而言之，使用交互塔分馏各种原胚，即可得到不同的分馏塔，同时还有小概率（4%）得到“分馏塔定向原胚”。
             分馏塔定向原胚可以直接制作为指定的分馏塔，无需经过交互塔处理。
             注意，{"任何分馏塔同一时间只能处理一种物品".WithColor(Orange)}，所以不同类型的原胚不要混投！
             将产出的分馏塔通过传送带输入至另一个交互塔的正面接口，即可上传物品至分馏数据中心，从而解锁对应的科技。
             上传不同分馏塔会解锁不同科技。例如，上传矿物复制塔将解锁矿物复制科技，上传交互塔将解锁物品交互科技。
             注意，只有处于“物品交互”模式下的交互塔才能上传正面输入的物品到分馏数据中心。也就是说，交互塔内部不能有物品，并且左右接口不能与传送带连接。
             定向原胚可以直接制作为指定的分馏塔，无需经过交互塔处理。

             {"【交互塔】".WithColor(Blue)}
             交互塔有两个功能：将原胚培养为各种分馏塔，以及上传物品。
             只有处于“物品交互”模式下的交互塔才能上传正面输入的物品到分馏数据中心。上传速率没有限制，也就是说，一个交互塔可以上传一满带的物品。

             {"【矿物复制塔】".WithColor(Blue)}
             矿物复制塔可以将各种矿物复制为多个。这对资源贫瘠的星区很有帮助。

             {"【点数聚集塔】".WithColor(Blue)}
             点数聚集塔可以调整输入物品的点数，将点数集中在部分物品上，突破增产剂的限制。
             初始产物只有4点增产点数。在建筑操作页面选择点数聚集塔，升级“点数聚集效率层次”，即可提升产物的增产点数，最多可以到10点。

             {"【量子复制塔】".WithColor(Blue)}
             量子复制塔可以复制绝大多数物品。
             复制时会消耗所有种类的精华，消耗的精华会直接从分馏数据中心扣除。缺少任何一种精华都会导致所有量子复制塔无法分馏出物品。
             物品价值越高，消耗的精华越多。原料的平均增产点数越多，消耗的精华越少。

             {"【点金塔】".WithColor(Blue)}
             点金塔可以将物品转换为对应层次的矩阵。

             {"【分解塔】".WithColor(Blue)}
             分解塔可以将物品精馏为制作它的原料。如果被分解的物品没有对应的单产物配方，将会被处理为沙土。

             {"【转化塔】".WithColor(Blue)}
             转化塔可以将物品转化为其他相关的物品。
             """
        );
        Register("分馏塔使用指南后字", "", "");

        Register("物流交互站使用指南标题", "Interaction station guidelines", "物流交互站使用指南");
        Register("物流交互站使用指南前字",
            $"""
             The Interaction Station is a logistic station that can interact with the Fractionation Data Centre.

             The Interaction Station has multiple modes to adapt to different scenarios.
             1. Items can be downloaded from the data centre when supply is unlocked or demand is locked. Items will no longer be downloaded after the slots are above the set threshold (initial value 20%).
             2. When supply is locked or demand is unlocked, items can be uploaded to the data centre. Items will no longer be uploaded after the items in the slot are below the set threshold (80% of the initial value). After a certain number of items have been stored in the Fractionation Data Centre (10 groups for buildings, 100 groups for non-buildings), items can no longer be uploaded in this way.
             The threshold value can be modified on the Miscellaneous Settings page.
             3. When storage is unlocked, the number of items will be maintained at half of the slot limit as much as possible. There is no limit to the number of items that can be uploaded in this way.
             4. When storage is locked, the number of items will be kept as close as possible to the number of items currently stored in the Fractionation Data Centre.
             When the interstellar policy and local policy are different, they will take effect at the same time; when the interstellar policy is storage, only the local policy will be considered.

             Uploading and downloading items consumes power from the Interaction Station. The higher the value of the item, the more power it consumes; the higher the enhancement level, the less power it consumes.
             The update frequency of the Interaction Station is 30 ticks (0.5s). When uploading or downloading items, individual slots consume up to '1/number of slots' of the Interaction Station's current power each time.

             You can use the lift function to replace a logistic station with a corresponding interaction station. For example, Planetary Logistic Station can be upgraded to Planetary Interaction Station.
             """,
            $"""
             物流交互站是可以与分馏数据中心进行物品交互的物流运输站。

             物流交互站具有多种模式，以便于适配不同的场景。
             1.供应无锁或需求锁定时，可从数据中心下载物品。槽位的物品高于设定的阈值（初始值20%）之后，物品将不再下载。
             2.供应锁定或需求无锁时，可上传物品至数据中心。槽位的物品低于设定的阈值（初始值80%）之后，物品将不再上传。当分馏数据中心存储的物品达到一定数目后（建筑10组，非建筑100组），无法再通过此方式上传物品。
             阈值可以在杂项设置页面修改。
             3.仓储无锁时，物品数目将尽量维持在槽位上限的一半。此方式上传物品没有数目限制。
             4.仓储锁定时，物品数目将尽量与分馏数据中心当前存储的物品数目保持一致。
             当星际策略和本地策略不同时，它们将同时生效；当星际策略为仓储时，仅考虑本地策略。

             上传、下载物品都会消耗物流交互站的电力。物品价值越高，消耗的电力越大；强化等级越高，消耗的电力越少。
             物流交互站的更新频率是30tick（0.5s）。上传或下载物品时，单个槽位每次至多消耗物流交互站当前电量的“1/槽位数目”。

             你可以使用升降级功能，将物流运输站与对应的物流交互站替换。例如，行星内物流运输站可以升级为行星内物流交互站。
             """
        );
        Register("物流交互站使用指南后字", "", "");
    }

    /// <summary>
    /// 添加指引手册内容（G键）
    /// </summary>
    public static void AddTutorials() {
        //DeterminatorName：解锁时机
        //DeterminatorParams：解锁参数
        //TOR_GameSecond    第几秒提示，参数为[伊卡洛斯落地后第几秒]
        //TOR_TechUnlocked  哪个科技解锁后提示，参数为[科技ID, 科技研究完成后再等待几秒]（第二个参数一般为4或7）
        //TOR_OnBuild       哪种建筑建造后提示，参数为[建筑1ID, 建筑2ID, ...]（任何一个建筑建造都会提示）
        //TOR_SandboxMode   沙盒模式提示，参数为[]
        //TOR_CombatMode    战斗系统提示，参数为[]
        //TOR_LowFuel       低能量提示，参数为[]
        //TOR_RecipeCopyTip 配方复制提示，参数为[]

        AddTutorial("万物分馏简介", "TOR_GameSecond", [10]);
        AddTutorial("分馏数据中心", "TOR_TechUnlocked", [TFE分馏数据中心, 4]);
        AddTutorial("分馏塔使用指南", "TOR_OnBuild", [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔]);
        AddTutorial("物流交互站使用指南", "TOR_OnBuild", [IFE行星内物流交互站, IFE星际物流交互站]);
    }

    private static int currTutorialID = 201;

    private static void AddTutorial(string name, string determinatorName, long[] determinatorParams) {
        TutorialProto proto = new() {
            ID = currTutorialID,
            SID = "",
            Name = $"{name}标题",
            name = $"{name}标题",
            LayoutFileName = $"tutorial-fe-{currTutorialID}",
            DeterminatorName = determinatorName,
            DeterminatorParams = determinatorParams,
        };
        LDBTool.PreAddProto(proto);
        proto.Preload();
        currTutorialID++;
    }

    /// <summary>
    /// 在指引窗口打开时，将左侧区域的垂直滚动条设为可见并添加事件监听器。
    /// 感谢海星佬（@starfi5h）的帮助！
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow._OnOpen))]
    private static void UITutorialWindow_OnOpen_Postfix(UITutorialWindow __instance) {
        if (!__instance.entryList.VertScroll) {
            __instance.entryList.VertScroll = true;
            __instance.entryList.m_ScrollRect.vertical = true;
            __instance.entryList.m_ScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            // Trigger ScrollRect.OnEnable() to add listeners
            __instance.entryList.m_ScrollRect.enabled = false;
            __instance.entryList.m_ScrollRect.enabled = true;
        }
    }

    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow.OnTutorialChange))]
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore(GenesisBook.GUID)]
    public static IEnumerable<CodeInstruction> UITutorialWindow_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) {
        var instructionList = instructions as List<CodeInstruction> ?? [.. instructions];
        var useCustomLayoutParserMethod = AccessTools.Method(typeof(TutorialManager), nameof(UseCustomLayoutParser));
        if (instructionList.Any(i => i.opcode == OpCodes.Call && Equals(i.operand, useCustomLayoutParserMethod))) {
            return instructionList;
        }

        var matcher = new CodeMatcher(instructionList, ilGenerator);

        /*
            string layoutStr = UILayoutParserManager.GetLayoutStr(UITutorialWindow.textFolder, this.tutorialProto.LayoutFileName);

            Ldarg_0
            Ldfld tutorialProto
            Call IsFELayout
            Brfalse_S originalLogicLabel

            Ldarg_0
            Ldfld tutorialProto
            Call GetLayoutStr
            Br_S endLabel

            IL_0027: ldsfld       string UITutorialWindow::textFolder // originalLogicLabel
            IL_002c: ldarg.0      // this
            IL_002d: ldfld        class TutorialProto UITutorialWindow::tutorialProto
            IL_0032: ldfld        string TutorialProto::LayoutFileName
            IL_0037: call         string UILayoutParserManager::GetLayoutStr(string, string)
            IL_003c: stloc.0      // layoutStr // endLabel

         */

        matcher.MatchForward(false, new CodeMatch(OpCodes.Stloc_0));
        if (matcher.IsInvalid) {
            LogError("TutorialManager.UITutorialWindow_Transpiler failed: cannot find stloc.0 anchor.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out var endLabel);

        matcher.MatchBack(false,
            new CodeMatch(OpCodes.Ldsfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.textFolder))));
        if (matcher.IsInvalid) {
            LogError("TutorialManager.UITutorialWindow_Transpiler failed: cannot find textFolder anchor.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out var originalLogicLabel);

        // 插入预加载和判断
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(TutorialManager), nameof(UseCustomLayoutParser))),
            new CodeInstruction(OpCodes.Brfalse_S, originalLogicLabel)
        );

        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(TutorialManager), nameof(GetLayoutStr))),
            new CodeInstruction(OpCodes.Br_S, endLabel)
        );

        return matcher.InstructionEnumeration();
    }

    public static bool IsFELayout(TutorialProto proto) {
        var layoutFileName = proto.LayoutFileName;
        return !string.IsNullOrEmpty(layoutFileName) && layoutFileName.StartsWith("tutorial-fe-");
    }

    public static bool UseCustomLayoutParser(TutorialProto proto) {
        return IsFELayout(proto) || IsGenesisBookLayout(proto);
    }

    const string preText =
        "{$Text|fontsize=16;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,30}\n";
    const string postText =
        "{$Text|fontsize=14;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,20}\n";

    public static string GetLayoutStr(TutorialProto proto) {
        if (IsGenesisBookLayout(proto)) {
            return GetGenesisBookLayoutStr(proto);
        }

        string protoName = proto.Name;
        if (!protoName.EndsWith("标题")) {
            return string.Empty;
        }
        var text = protoName.Replace("标题", "前字");
        return $"{preText}{protoName.Translate()}{postText}{text.Translate()}";
    }

    static bool IsGenesisBookLayout(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return false;
        }

        return genesisBookIsLayoutMethod.Invoke(null, [proto]) is bool isGenesisBookLayout && isGenesisBookLayout;
    }

    static string GetGenesisBookLayoutStr(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return string.Empty;
        }

        return genesisBookGetLayoutMethod.Invoke(null, [proto]) as string ?? string.Empty;
    }

    static bool TryInitGenesisBookLayoutMethods() {
        if (genesisBookLayoutMethodsInitialized) {
            return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
        }

        genesisBookLayoutMethodsInitialized = true;
        var tutorialPatchType = AccessTools.TypeByName("ProjectGenesis.Patches.UITutorialWindowPatches");
        if (tutorialPatchType == null) {
            return false;
        }

        genesisBookIsLayoutMethod = AccessTools.Method(tutorialPatchType, "IsGenesisBookLayout", [typeof(TutorialProto)]);
        genesisBookGetLayoutMethod =
            AccessTools.Method(tutorialPatchType, "GetGenesisBookLayoutStr", [typeof(TutorialProto)]);
        return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
    }
}
