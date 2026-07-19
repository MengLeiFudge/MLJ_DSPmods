using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 教程系统翻译文本注册入口。
/// </summary>
public static partial class TutorialManager {
    public static void AddTranslations() {
        Register("万物分馏简介标题", "Fractionate Everything", "万物分馏简介");
        Register("万物分馏简介前字",
            $"""
             An abnormal old-civilization signal has been detected near the landing zone. The first contact is unstable, but the recovered starter kit contains a data-centre communication channel, initial tower protos, and one Interaction Tower.

             Here is a short early guide:
             1. Follow the abnormal signal and recover the starter kit. After the data-centre communication channel is established, press {"[Shift + F] key".WithColor(Orange)} to connect to the Fractionation Data Centre.
             2. Open the Recovery Guide page in the panel and follow it step by step to learn how to use the Interaction Tower to cultivate protos into new Fractionation Towers, as well as how to upload items.
             There will also be a new guide that explains how to use the Interactive Tower after you build it.
             3. Cultivate a new Interactive Tower and upload it to the Fractionation Data Centre to restore the Item Interaction protocol.
             This is an extremely powerful support feature that defies space limitations and can be thought of as an external backpack with unlimited capacity.
             4. Build the production line for the current-stage Matrix. Process matrices in the Analysis Tower, upload the physical analysis data, and spend the generated retrieval opportunities to recover recipes.

             By the way, there is one thing you should remember: you can always revisit all the guidelines by pressing {"[G] key".WithColor(Orange)}.

             {"Have fun with fractionation!".WithColor(Blue)}
             """,
            $"""
             着陆区附近探测到了异常旧文明信号。第一次接触并不稳定，但回收得到的启动套件里包含数据中心通信方式、初始分馏塔原胚和一座交互塔。

             以下是一个简短的前期指引：
             1.按异常信号提示前往信号点，回收旧文明启动套件。数据中心通信建立后，按{"[Shift + F]键".WithColor(Orange)}即可连接到分馏数据中心。
             2.打开分馏数据中心的“恢复手册”页面，按步骤学习如何使用交互塔将原胚培养为新的分馏塔，以及如何使用交互塔上传物品。
             建设交互塔之后，也会有新的指引对此进行讲解。
             3.培养出新的交互塔，并将其上传至分馏数据中心，恢复“物品交互”协议。
             这是一项极其强大的辅助功能，无视空间限制，你可以将它视为具有无限容量的外部背包。
             4.搭建当前阶段矩阵产线，用解析塔将矩阵处理为实体解析数据；上传数据后生成检索机会，用于发现并补全配方协议。

             对了，有一件事情你要记住：你可以随时按{"[G]键".WithColor(Orange)}重新查阅所有指引。

             {"尽情享受分馏的乐趣吧！".WithColor(Blue)}
             """);
        Register("万物分馏简介后字", "", "");

        Register("分馏数据中心标题", "Fractionation data centre", "分馏数据中心");
        Register("分馏数据中心前字",
            $"""
             You can now connect to the Fractionation Data Centre by pressing {"[Shift + F] key".WithColor(Orange)}.
             This recovered old-civilization panel manages physical resources, protocol recovery, the ancient technology tree, and achievements.

             {"[Core Loop]".WithColor(Blue)}
             1. Build the production line for a Matrix stage.
             2. Feed matrices into the Analysis Tower. It outputs physical analysis data for that stage.
             3. Upload the analysis data through an Interaction Tower or another data-centre upload path. Data is converted into progressively more expensive retrieval opportunities.
             4. Spend an opportunity on Protocol Recovery. A retrieval may fail, discover a recipe protocol, or increase the completeness of an already discovered protocol.
             5. A recipe becomes usable at 100% completeness. You may prioritise one discovered protocol, but discovery order remains partly random.

             {"[Ancient Technology]".WithColor(Blue)}
             After the main protocols of a stage are complete, later opportunities enter deep analysis. Deep analysis produces the single shared Ancient Technology Point type.
             Spend these points on the four tower branches. The first implemented nodes enable stacked flow output for Interaction, Resource, Conversion, and Analysis Towers.

             {"[Recipe Operation]".WithColor(Blue)}
             The Recipe page displays output structure and actual running parameters. Resource Replication and Conversion recipes display protocol discovery and completeness instead of legacy recipe levels.
             Equivalent Output estimates expected production after recipe, tower, proliferator, and achievement modifiers.

             {"[Item Interaction]".WithColor(Blue)} 
             Item Interaction is an extremely powerful and convenient protocol, much loved by various Icarus.

             After the Item Interaction protocol is restored, most actions can be performed using items stored in the Fractionation Data Centre, rather than having to extract them from the backpack and use them again. Examples include:
             Fabrication Table to manually craft items; Quick Build Bar to select a building; Tech to manually research; Building TAB to fill items; Fuel Fill; Warp Fill; Ammo Fill; Drone Fill ...... and many more.
             However, to use these handy features, you need to restore the Item Interaction protocol before storing a sufficient number of items to the Fractionation Data Centre.

             How to upload items:
             1. The Interaction Tower will upload items entered on the front interface. Check the [Fractionation Tower User Guide] guide for more information.
             2. The Logistics Interactive Station will upload items that meet certain conditions. Check the 'Logistics Interactive Station User's Guide' for more information.
             3. When you double-click the backpack button, all items in your backpack will be uploaded (the Item Interaction protocol must be restored first, and your logistics backpack will not be affected).

             How to download items:
             1. On the Item Interaction or Important Items page, left-click or right-click the corresponding item to extract it. You can set the number of groups to be extracted on the Miscellaneous Settings page.
             2. Logistics Interactive Station downloads items that meet certain conditions. For more information, refer to the 'Logistics Interactive Station User's Guide'.
             3. Various fill operations (manual creation, manual research, TAB fill, fuel fill, etc.) will automatically download items and use them.

             {"[Achievements]".WithColor(Blue)}
             Civilization achievements are fixed single-save milestones. They consume no technology points and apply their rewards automatically when their production or progression conditions are met.
             """,
            $"""
             现在，你可以按{"[Shift + F]键".WithColor(Orange)}连接到分馏数据中心。
             这是一个已恢复的旧文明总控面板，用于管理实体资源、协议恢复、远古科技树和文明成就。

             {"【核心流程】".WithColor(Blue)}
             1.搭建某个矩阵阶段的生产线。
             2.将矩阵输入解析塔，得到该阶段的实体解析数据。
             3.通过交互塔或其他数据中心上传入口上传解析数据。数据会转化为检索机会，且后续机会所需数据越来越多。
             4.在“文明协议恢复”页面消耗机会。一次检索可能无有效响应、发现新配方协议，或推进已发现协议的完整度。
             5.协议完整度达到 100% 后，对应配方才可运行。已发现协议可以设为优先目标，但发现顺序仍保留少量随机性。

             {"【远古科技】".WithColor(Blue)}
             当一个阶段的主协议全部完成后，后续检索机会会进入深层解析。深层解析只产出一种“远古文明科技点”。
             科技点可以投入交互塔、资源塔、转化塔和解析塔四条主干。当前首批节点用于解锁对应塔型的流动物品堆叠输出。

             {"【配方操作】".WithColor(Blue)}
             配方页面用于查看产物结构和实际运行参数。资源复制与转化配方的右侧信息改为协议发现状态和完整度，不再显示旧配方等级。
             “等效输出”会综合配方、塔型、增产剂和文明成就加成，用于估算平均产出。

             {"【物品交互】".WithColor(Blue)}
             物品交互是一项极其强大的便利协议，深受各个伊卡洛斯的喜爱。

             在物品交互协议恢复后，绝大多数操作都可以直接使用分馏数据中心存储的物品，而不需要提取物品到背包再使用。例如：
             制造台手动制作物品；快捷建造栏选择建筑；科技手动研究；建筑TAB填充物品；燃料填充；翘曲填充；弹药填充；无人机填充……等等。
             不过，要想使用这些便利的功能，你需要先恢复物品交互协议，再向分馏数据中心存储足够数目的物品。

             如何上传物品：
             1.交互塔会上传正面接口输入的物品。查阅【分馏塔使用指南】指引以了解更多信息。
             2.物流交互站会上传满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.双击整理背包按钮时，背包内的物品会全部上传（需恢复“物品交互”协议，物流背包不受影响）。

             如何下载物品：
             1.在物品交互页面，左键或右键点击对应物品即可提取。可以在杂项设置页面设定提取的组数。
             2.物流交互站会下载满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.各种填充操作（手动制作、手动研究、TAB填充、燃料填充等等）会自动下载物品并使用。

             {"【文明成就】".WithColor(Blue)}
             文明成就是单存档固定里程碑，不消耗科技点。达到生产或探索条件后会自动完成，并把固定奖励投影到分馏运行逻辑。
             """
        );
        Register("分馏数据中心后字", "", "");


        Register("分馏塔使用指南标题", "Fractionator guidelines", "分馏塔使用指南");
        Register("分馏塔使用指南前字",
            $"""
             {"[Cultivate Fractionation Tower]".WithColor(Blue)} 
             In the old-civilization recovery system, Fractionation Towers are no longer obtained by manufacturing, but mainly by cultivating them in Interactive Towers.
             Simply put, by using the Interaction Tower to fractionate a tower proto, you get the matching Fractionation Tower with 100% output.
             There are 4 tower protos, and their products are as follows:
             Type I: Interaction Tower
             Type II: Resource Tower
             Type III: Conversion Tower
             Type IV: Analysis Tower
             Note that {"Only one type of item can be processed by any Fractionation Tower at any one time".WithColor(Orange)}, so don't mix the different types of Protoembryo!
             Items can be uploaded to the Fractionation Data Centre by feeding the output Fractionation Tower through a conveyor belt to the front interface of another Interactive Tower, thus restoring the corresponding old-civilization protocol.
             Uploading different Fractionation Towers registers their tower type. For example, uploading a Resource Tower restores its control protocol, while uploading an Interaction Tower restores the Item Interaction protocol.
             Note that only Interactive Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. This means that there can be no items inside the tower, and the left and right ports cannot be connected to a conveyor belt.

             {"[Interaction Tower]".WithColor(Blue)} 
             The Interaction Tower has two functions: to grow embryos into various fractionation towers, and to upload items.
             Only Interactive Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. There is no limit to the upload rate, meaning that a single tower can upload a full belt of items.

             {"[Resource Tower]".WithColor(Blue)}
             Resource Towers replicate raw resources. Their recipe protocols are recovered through civilization analysis.

             {"[Analysis Tower]".WithColor(Blue)}
             The Analysis Tower converts matrices into physical analysis data. Upload that data to generate protocol retrieval opportunities.

             {"[Conversion Tower]".WithColor(Blue)} 
             The Conversion Tower can convert items into other related items.
             """,
            $"""
             {"【培养分馏塔】".WithColor(Blue)}
             在旧文明恢复体系中，分馏塔不再通过制造得到，而是主要通过交互塔培养得到。
             简而言之，使用交互塔分馏各种原胚，即可 100% 得到对应类型的分馏塔。
             注意，{"任何分馏塔同一时间只能处理一种物品".WithColor(Orange)}，所以不同类型的原胚不要混投！
             将产出的分馏塔通过传送带输入至另一个交互塔的正面接口，即可上传物品至分馏数据中心，从而恢复对应的旧文明协议。
             上传不同分馏塔会完成对应塔型注册。例如，上传资源塔会恢复资源塔的旧文明控制协议，上传交互塔会恢复物品交互协议。
             注意，只有处于“物品交互”模式下的交互塔才能上传正面输入的物品到分馏数据中心。也就是说，交互塔内部不能有物品，并且左右接口不能与传送带连接。

             如果你不确定第一轮该怎么搭建，可以打开分馏数据中心的“恢复手册”页面。推荐顺序是：先放下交互塔，将左右口接成环，正面输出接临时箱子；向环内输入交互塔原胚，产出第二台交互塔；再拆掉临时箱子，把第一台塔的产物接入第二台塔的正面入口完成上传。

             {"【交互塔】".WithColor(Blue)}
             交互塔有两个功能：将原胚培养为各种分馏塔，以及上传物品。
             只有处于“物品交互”模式下的交互塔才能上传正面输入的物品到分馏数据中心。上传速率没有限制，也就是说，一个交互塔可以上传一满带的物品。

             {"【资源塔】".WithColor(Blue)}
             资源塔用于复制原矿资源，对应配方协议通过文明解析逐步恢复。

             {"【解析塔】".WithColor(Blue)}
             解析塔将矩阵转化为实体解析数据。把解析数据上传到数据中心后，会积累对应阶段的协议检索机会。

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
}
