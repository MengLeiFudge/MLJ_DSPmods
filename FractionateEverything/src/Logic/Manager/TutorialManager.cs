using HarmonyLib;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class TutorialManager {
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
             4. Research the 'Super Value Gift Pack 1' tech and 'Electromagnetic Ticket' tech to automate the production of tickets and use them for raffles.
             The information about raffles is explained in the Fractionation Data Centre guide.

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
             4.研究“超值礼包1”科技和“电磁奖券”科技，自动化生产奖券，并用它们抽奖。
             抽奖的相关信息已在分馏数据中心的指引中说明。

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
             Panel has a lot of features, among which the core ones you must understand are draws and upgrades. Generally speaking, the advancement process goes like this:
             1. Research the gift pack tech and raffle tech. This will allow you to automate the creation of raffle tickets.
             2. create raffle tickets and upload them. If you don't understand how to upload items, you can check [Item Interaction] below.
             3. Use raffle tickets to draw. Draw the Recipe Prize Pool, you can get Fractionated Recipe, Fractionated Recipe Universal Core, the first time you draw a recipe it will unlock the recipe, after that, it will be converted to Eponymous Echo (one of the conditions for recipe breakthrough). Draw the original embryo prize pool, you can get the original embryo of the fractionation tower, fractionation tower increase chip.
             4. The recipe needs to be unlocked, upgraded, and broken through, and the full level is Gold 10. When using the Fractionator to process the corresponding item, you can get experience regardless of success or failure; you can also use sand to exchange experience directly. Cores can be turned into designated recipes, equivalent to drawing recipes by lottery.
             5. Buildings need to be upgraded and strengthened with chips, and the maximum strengthening is +20. Chips can directly improve the effect of buildings. This is a very precious resource that needs to be planned wisely. Once all the upgrades have been completed, you can strengthen them to further enhance the building's effect.

             {"[Recipe Operation]".WithColor(Blue)} 
             In the Recipe Operation page, you can query the current status of any fractionated recipe and operate on them.

             After selecting the type of recipe you want to view, left-click or right-click on the item icon to toggle the recipe. After left-clicking, all currently unlocked recipes under the selected type will be displayed; after right-clicking, all recipes under the selected type will be displayed.
             Fractionation recipe types and fractionation towers are one-to-one correspondence. For example, building cultivation recipes can only be processed by the Interactive Tower, and the Interactive Tower can only process building cultivation recipes. The Points Aggregation Tower is a special case, which does not require fractionated recipes.

             1. Recipe Unlocking 
             You can only process the corresponding item after the corresponding recipe is unlocked.
             Note that there are different recipes even for the same items. For example, [Mineral Duplication - Iron Ore] and [Fractionation - Iron Ore] are different recipes, and there is no connection between them. Iron Ore entered into the Mineral Duplication Tower will be processed according to the [Mineral Duplication - Iron Ore] recipe; Iron Ore entered into the Gold Pitting Tower will be processed according to the [Gold Pitting - Iron Ore] recipe.
             When a recipe is first unlocked, its output information is hidden. You need to build the corresponding production line and use this recipe, and then the relevant information will be unlocked gradually. You can also choose to display the recipe information directly in the settings.
             Recipes can be obtained through the Recipe Lucky Draw, purchasing from the Limited Time Store, or directly redeeming with the Fractionated Recipe Generic Core (button at the top of the Recipe Operation page).

             2. Recipe Experience, Levels and Upgrades 
             Each recipe has a level, and when unlocked, the level is 1. The higher the level, the stronger the recipe effect will be. The higher the level, the stronger the effect of the recipe. The level limit is affected by the quality.
             Fractionated recipes will continue to gain experience when you use the Fractionation Tower to process the corresponding item. You can also exchange experience directly with sand (button at the top of the recipe operation page).
             Once the experience reaches the value required for upgrading, the recipe will be automatically upgraded to the next level until it reaches the level cap corresponding to the current quality.

             3.Recipe Quality and Breakthrough 
             Each recipe has a quality, from low to high it is {"Unlocked".WithColor(0)}-{"White Quality".WithColor(1)}-{"Green Quality".WithColor(2)}-{"Blue Quality".WithColor(3)}-{"Purple Quality".WithColor(4)}-{"Red Quality".WithColor(5)}-{"Gold Quality".WithColor(7)}."
             White quality recipes have a level cap of 4, red quality recipes have a level cap of 8, and gold quality recipes have a level cap of 10. 
             There are 3 breakthrough conditions: 
             First, the level of the recipe needs to reach the highest level of the current quality.
             Second, the experience of the recipe must reach the current level of upgrade experience.
             Third, you must have a sufficient number of Echoes of the same name.
             Once all the breakthrough conditions are met, the recipe will automatically attempt to breakthrough. There is a success rate for breakthroughs, and the higher the quality, the lower the success rate.
             Regardless of whether the breakthrough is successful or not, a certain percentage will be deducted based on the current upgrade experience required. Failure deducts 20%; success deducts 100%, and the recipe level is reset to 1 to upgrade to the next quality.
             You can also use the button above to consume sand to break through with one click, and it will not stop until there is not enough sand or the recipe is successfully broken through.

             4. Equivalent Output 
             Equivalent Output can help you quantify to some extent.
             Firstly, select the average yield increase points of the input item. In general, the yield enhancers MKI, II, and III will allow the item to carry 1, 2, and 4 yield increase points.
             You can then see the final number of products that can be obtained by putting 1 feedstock carrying the specified number of yield increase points into the corresponding fractionation column.
             This value has been calculated for various additions (including additions to the recipe itself, additions to the fractionating tower, etc.), and it is an accurate expectation.

             {"[Building Operations]".WithColor(Blue)} 
             On the Building Operations page, you can upgrade the effects of Fractionation Towers and Logistics Interaction Stations.

             Since the upgrade items are different for each building, here are only some important ones:
             1. Each 'Value Pack' technology will give you 3 chips, so please plan their use wisely, as it's hard to get chips in the early stages (EM Matrix and Energy Matrix stages).
             2. Product Output Stacking can be upgraded 3 times, Point Aggregation Efficiency Level can be upgraded 6 times, and all others only need to be upgraded once.
             3. After all the upgrades are completed, the 'Reinforcement' function will be opened. Enhancement can greatly improve the effect of the Fractionator.
             There are 20 levels of enhancement, and each enhancement will consume 1 chip. There is a success rate for enhancement, but fortunately, failure does not result in a loss of level, which is probably the mercy of the mastermind~.

             {"[Item Interaction]".WithColor(Blue)} 
             Item Interaction is an extremely powerful and convenient feature, much loved by various Icarus.

             Under the influence of item interaction technology, most actions can be performed using items stored in the Fractionation Data Centre, rather than having to extract them from the backpack and use them again. Examples include: 
             Fabrication Table to manually craft items; Quick Build Bar to select a building; Tech to manually research; Building TAB to fill items; Fuel Fill; Warp Fill; Ammo Fill; Drone Fill ...... and many more.
             However, to use these handy features, you need to unlock the Item Interaction tech before storing a sufficient number of items to the Fractionation Data Centre.

             How to upload items:
             1. The Interaction Tower will upload items entered on the front interface. Check the [Fractionation Tower User Guide] guide for more information.
             2. The Logistics Interactive Station will upload items that meet certain conditions. Check the 'Logistics Interactive Station User's Guide' for more information.
             3. Items obtained from the Raffle Ticket Lucky Draw and items exchanged in the Limited Time Store will be uploaded automatically.
             4. When you double-click the backpack button, all items in your backpack will be uploaded (you need to unlock the 'Item Interaction' technology, and your logistics backpack will not be affected).

             How to download items:
             1. On the Item Interaction or Important Items page, left-click or right-click the corresponding item to extract it. You can set the number of groups to be extracted on the Miscellaneous Settings page.
             2. Logistics Interactive Station downloads items that meet certain conditions. For more information, refer to the 'Logistics Interactive Station User's Guide'.
             3. Various fill operations (manual creation, manual research, TAB fill, fuel fill, etc.) will automatically download items and use them.

             {"[Raffle Draw]".WithColor(Blue)} 
             The raffle system is one of the most important parts of Fractionation Technology. There are currently four prize pools available for drawing.

             1. Recipe Pool 
             You can draw all kinds of Fractionation recipes. The first time you draw a recipe, you will unlock the corresponding recipe, and then you can use the corresponding Fractionator to process the corresponding item.
             If you draw the same recipe later, the recipe will automatically be converted to 'Echoes of the same name', which is one of the conditions for recipe breakthrough.
             Once the number of echoes reaches 5, the recipe will be removed from the prize pool and you will not be able to draw it again.
             Non-Black Mist raffle tickets can only be drawn for non-Black Mist recipes, and Black Mist raffle tickets can only be drawn for Black Mist-only recipes.
             Quantum Duplicate Recipes can only be drawn when using a higher level raffle ticket and after all non-Quantum Duplicate Recipes in the higher level pool have been drawn.
             When drawing with a certain raffle ticket, the current level recipe will be drawn first, then the Quantum Duplication recipe from the previous level, and finally only the Fractionated Essence will be drawn.
             There is a small chance of obtaining a Fractionated Recipe Universal Core, which can be used to unlock any recipe, or converted to any Echo of the same name.
             Unlocking is relatively free. For example, you can still use the core to convert quantum duplicate recipes at the electromagnetic matrix level until the non-quantum duplicate recipes in the energy matrix recipe pool have been drawn.

             2. Primary Embryo Pool 
             Various types of primary embryos can be extracted. This is the main source of Fractionation Tower Primary Embryos.
             There is a small chance that you will get a 'Fractionator Orientation Raw Blank', which can be directly converted to a specific Fractionator without cultivation. This can be converted to a specific Fractionator without cultivation, provided that the corresponding Fractionator Technology has been unlocked.
             There is a small chance to obtain 'Fractionation Tower Increase Chip', which can be used to increase the effect of Fractionation Tower or Logistics Interactive Station.

             3. Material Pool 
             All unlocked materials (excluding Matrix) can be drawn.

             4. Building Pool 
             All unlocked buildings can be drawn.

             All draws follow the 'Equivalence Principle'. The higher the value of the tickets you put in, the better the rewards you will get.
             Also, the higher the VIP level, the higher the value of the tickets and the better the rewards.

             {"[Limited Time Store]".WithColor(Blue)} 
             The Limited Time Store sells random items and refreshes regularly.

             The shop is initially refreshed every 10 minutes and offers recipes and a variety of valuable items.
             Refreshing or purchasing items requires the use of the current tech level's Matrix; the higher the VIP level, the less Matrix value is spent.
             In addition, there are some free items in each refreshed shipment, which will be automatically redeemed without any manual action, and the higher the VIP level, the more free items there will be.
             The more VIP level you have, the more free items you will get. The shop will alert you in the bottom left corner when it is refreshed, so don't miss out on your favourite items!

             {"[Quest System]".WithColor(Blue)} 
             Haven't written it yet meow~ Don't curse meow!

             {"[VIP Privileges]".WithColor(Blue)} 
             VIP has the following privileges:
             1. Raffle tickets will be considered of higher value during raffles.
             2. There are more free redemption items in the Limited Time Store.
             3. Greater discounts on paid redemption items in the Limited Time Store.

             VIP experience can be accumulated by using Raffle Tickets to draw prizes, or by purchasing items in the Limited Time Shop. Upon reaching a certain level of experience, VIP will be automatically upgraded.

             {"[Recipe Gallery]".WithColor(Blue)} 
             Here you can view the overall situation of the recipe, including the four data items of Fully Upgraded, Maximum Reverberation, Unlocked, and Total.
             Full Upgrade refers to recipes that have reached gold quality and level 10, and Max Reverberations refers to recipes that have a number of reverberations equal to 5.
             """,
            $"""
             现在，你可以按{"[Shift + F]键".WithColor(Orange)}连接到分馏数据中心。
             这是一个全新的总控面板，可以让你方便地管理分馏科技相关内容。

             {"【使用简介（太长不看版）】".WithColor(Blue)}
             面板有很多功能，其中你必须了解的核心功能是抽奖和升级。一般而言，推进流程是这样的：
             1.研究礼包科技和奖券科技。这样你就可以自动化制作奖券。
             2.制作奖券并上传。如果你不了解如何上传物品，可以查阅下面的【物品交互】。
             3.使用奖券抽奖。抽取配方奖池，可以得到分馏配方、分馏配方核心，第一次抽到配方会解锁该配方，之后会转为同名回响（配方突破的条件之一）。抽取原胚奖池，可以得到分馏塔原胚、分馏塔增幅芯片。
             4.配方需要解锁、升级、突破，满级为金色10级。使用分馏塔处理对应物品时，无论成功失败都可以得到经验；也可以使用沙土直接兑换经验。核心可以转为指定的配方，相当于抽奖抽到配方。
             5.建筑需要使用芯片升级、强化，强化最高+20。芯片可以直接提升建筑效果。这是很珍贵的资源，需要合理规划。升级项全部升满后即可强化，进一步提升建筑效果。

             {"【配方操作】".WithColor(Blue)}
             在配方操作页面中，你可以查询任何分馏配方的当前状态，并对它们进行操作。

             选择想要查看的配方类型后，左键或右键单击物品图标，即可切换配方。左键单击后，会显示所选类型下的当前已解锁的所有配方；右键单击后，会显示所选类型下的所有配方。
             分馏配方类型与分馏塔是一一对应的。例如，建筑培养配方只能由交互塔处理，交互塔也只能处理建筑培养配方。点数聚集塔是一个特例，它不需要分馏配方。

             1.配方解锁
             在对应配方解锁后，你才可以对相应物品进行处理。
             注意，即使是相同的物品，也有不同的配方。例如，[矿物复制-铁矿]和[点金-铁矿]是不同的配方，它们之间没有任何关联。铁矿输入矿物复制塔，将会根据[矿物复制-铁矿]配方进行处理；铁矿输入点金塔，将会根据[点金-铁矿]配方进行处理。
             一个配方刚解锁时，它的输出信息是隐藏的。你需要搭建对应产线并使用此配方，之后相关信息会逐渐解锁。你也可以在设置中选择直接显示配方的具体信息。
             配方的获取途径有：配方奖池抽奖、限时商店购买、直接使用分馏配方核心兑换（配方操作页面上方按钮）。

             2.配方经验、等级与升级
             每个配方都有等级，解锁后等级为1。等级越高，配方效果也就越强。等级上限受品质影响。
             使用分馏塔处理对应物品时，分馏配方将持续获得经验。你也可以直接用沙土兑换经验（配方操作页面上方按钮）。
             经验达到升级所需的数值后，配方将自动提升到下一个等级，直至到达当前品质对应的等级上限。

             3.配方品质与突破
             每个配方都有品质，从低到高为{"未解锁".WithColor(0)}-{"白色品质".WithColor(1)}-{"绿色品质".WithColor(2)}-{"蓝色品质".WithColor(3)}-{"紫色品质".WithColor(4)}-{"红色品质".WithColor(5)}-{"金色品质".WithColor(7)}。"
             白色品质配方的等级上限为4，红色品质配方的等级上限为8，金色品质配方的等级上限为10。
             突破条件共有3条：
             其一，配方的等级需要达到当前品质的最高等级。
             其二，配方的经验需要达到当前等级的升级经验。
             其三，拥有足够数目的同名回响。
             突破条件全部达成后，配方将自动尝试突破。突破有成功率，品质越高成功率越低。
             无论突破是否成功，都会以当前升级所需经验为基准，扣除一定百分比。失败扣除20%；成功扣除100%，配方等级重置为1，提升到下一品质。
             你也可以使用上方按钮消耗沙土一键突破，直至沙土不足或配方成功突破才会停止。

             4.等效输出
             等效输出可以在一定程度上帮助你进行量化。
             首先选择输入物品的平均增产点数。一般而言，增产剂MKI、II、III可以使物品携带1、2、4点增产点数。
             然后你就可以看到，将携带指定的增产点数的1个原料投入到对应分馏塔中，最终可以得到的产物数目。
             这个数值已经计算了各种加成（包括配方本身加成、分馏塔加成等等），它是准确的期望。

             {"【建筑操作】".WithColor(Blue)}
             在建筑操作页面中，你可以提升分馏塔、物流交互站的效果。

             由于每个建筑的升级项都不一样，这里只讲一些重要的内容：
             1.每个“超值礼包”科技都会赠送3个芯片，请合理规划它们的用途，因为前期（指电磁矩阵、能量矩阵阶段）很难抽到芯片。
             2.产物输出堆叠可以升级3次，点数聚集效率层次可以升级6次，其他都只需要升级1次。
             3.升级项全部升满后，将开启“强化”功能。强化可以大幅提升分馏塔的效果。
             强化共有20级，每次强化都会消耗1个芯片。强化是有成功率的，幸好强化失败不掉级，这大概是主脑的仁慈吧~

             {"【物品交互】".WithColor(Blue)}
             物品交互是一项极其强大的便利功能，深受各个伊卡洛斯的喜爱。

             在物品交互科技的影响下，绝大多数操作都可以直接使用分馏数据中心存储的物品，而不需要提取物品到背包再使用。例如：
             制造台手动制作物品；快捷建造栏选择建筑；科技手动研究；建筑TAB填充物品；燃料填充；翘曲填充；弹药填充；无人机填充……等等。
             不过，要想使用这些便利的功能，你需要先解锁物品交互科技，再向分馏数据中心存储足够数目的物品。

             如何上传物品：
             1.交互塔会上传正面接口输入的物品。查阅【分馏塔使用指南】指引以了解更多信息。
             2.物流交互站会上传满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.奖券抽奖获得的物品、限时商店兑换的物品会自动上传。
             4.双击整理背包按钮时，背包内的物品会全部上传（需解锁“物品交互”科技，物流背包不受影响）。

             如何下载物品：
             1.在物品交互或重要物品页面，左键或右键点击对应物品即可提取。可以在杂项设置页面设定提取的组数。
             2.物流交互站会下载满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.各种填充操作（手动制作、手动研究、TAB填充、燃料填充等等）会自动下载物品并使用。

             {"【奖券抽奖】".WithColor(Blue)}
             抽奖系统是分馏科技中最重要的部分之一。目前有四个奖池可供抽取。

             1.配方奖池
             可抽取各种分馏配方。首次抽到配方会解锁对应配方，之后可以使用对应分馏塔处理对应物品。
             后续抽到相同配方时，抽到的配方会自动转换为“同名回响”，这是配方突破的条件之一。
             同名回响的数目达到5个之后，配方会从奖池中移除，你不会再抽到此配方。
             非黑雾奖券只能抽到非黑雾配方，黑雾奖券只能抽到黑雾独属配方。
             量子复制配方只有在使用高一级的奖券，并且在高一级奖池的非量子复制配方全部抽取完成后，才能抽到。
             当使用某种奖券抽奖时，会先抽取当前层次配方，再抽取上一层次的量子复制配方，最后只能抽到分馏精华。
             抽奖时有小概率获取“分馏配方核心”，可用于解锁任意配方，或转换为任意同名回响。
             解锁是相对自由的。例如，在能量矩阵配方池中的非量子复制配方未抽取完之前，你仍然可以使用核心兑换电磁矩阵层次的量子复制配方。

             2.原胚奖池
             可抽取各种原胚。这是分馏塔原胚的主要来源。
             抽奖时有小概率获取“分馏塔定向原胚”，可以直接转换为指定的分馏塔，无需培养。前提是已解锁对应的分馏塔科技。
             抽奖时有小概率获取“分馏塔增幅芯片”，可用于提高分馏塔或物流交互站的效果。

             3.材料奖池
             可抽取各种已解锁的材料（不包含矩阵）。

             4.建筑奖池
             可抽取各种已解锁的建筑。

             所有的抽取都遵循“等价原则”。投入的奖券价值越高，得到的奖励也就越好。
             同时，VIP等级越高，奖券价值也就越高，得到的奖励也就越好。

             {"【限时商店】".WithColor(Blue)}
             限时商店会随机出售一些物品，并定时刷新。

             限时商店初始每10分钟刷新一次，提供配方和各种珍贵物品。
             刷新或购买物品都需要使用当前科技层次的矩阵。VIP等级越高，花费的矩阵价值就越少。
             除此之外，每次刷新货物都会有一些免费物品，它们会被自动兑换，无需手动操作。VIP等级越高，免费项数就越多。
             商店刷新时会在左下角进行提示，不要错过心仪的物品！

             {"【任务系统】".WithColor(Blue)}
             还没写喵~别骂了喵！

             {"【VIP特权】".WithColor(Blue)}
             VIP具有以下特权：
             1.在抽奖时，奖券将视为更高的价值。
             2.限时商店有更多的免费兑换项。
             3.限时商店的付费兑换项有更大的折扣。

             使用奖券抽奖，或者在限时商店中购买物品，都可以累计VIP经验。经验达到一定程度时，VIP会自动升级。

             {"【配方图鉴】".WithColor(Blue)}
             此处可以查看配方的整体情况，包括完全升级、最大回响、已解锁、总数这四项数据。
             其中，完全升级指的是达到金色品质并且等级为10级的配方，最大回响指的是回响数目等于5的配方。
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
             分解塔可以将物品回收为制作它的原料。如果被分解的物品没有对应的单产物配方，将会被处理为沙土。

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

    private static int currTutorialID = 51;

    private static void AddTutorial(string name, string determinatorName, long[] determinatorParams) {
        // TutorialProto proto = new() {
        //     ID = currTutorialID,
        //     SID = "",
        //     Name = $"{name}标题",
        //     name = $"{name}标题",
        //     // PreText = $"{name}前字",
        //     // PostText = $"{name}后字",
        //     // Video = "",
        //     DeterminatorName = determinatorName,
        //     DeterminatorParams = determinatorParams,
        //     LayoutFileName = $"tutorial-fe-{currTutorialID - 50}"
        // };
        // LDBTool.PreAddProto(proto);
        // proto.Preload();
        // currTutorialID++;
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
}
