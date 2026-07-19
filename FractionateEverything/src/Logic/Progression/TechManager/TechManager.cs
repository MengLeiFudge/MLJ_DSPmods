using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    public static void AddTranslations() {
        Register("T分馏数据中心", "Fractionation Data Centre", "分馏数据中心");
        Register("分馏数据中心描述",
            $"An abnormal old-civilization signal has been recovered. The starter kit contains the data-centre communication channel, initial tower protos, and one Interaction Tower. Press {"Shift + F".WithColor(Orange)} after recovery to connect to the Fractionation Data Centre.",
            $"已回收异常旧文明信号中的启动套件。套件包含数据中心通信方式、初始分馏塔原胚和一座交互塔。回收完成后按 {"Shift + F".WithColor(Orange)} 即可连接到分馏数据中心。");
        Register("分馏数据中心结果",
            "Data-centre communication has been restored. The starter kit cargo is available.",
            "数据中心通信已恢复，启动套件货物已就绪。");
        Register("允许连接到分馏数据中心", "Restore data-centre communication", "恢复数据中心通信");
        Register("给予一些分馏塔原胚", "Recover starter tower protos", "回收启动套件原胚");
        Register("旧文明启动套件回收提示", "Old-civilization starter kit recovered.", "已回收旧文明启动套件，数据中心通信已建立。");
        Register("异常旧文明信号出生星提示",
            "Abnormal old-civilization signal detected on the birth planet.",
            "出生星上探测到异常旧文明信号。");
        Register("异常旧文明信号距离提示",
            "Abnormal old-civilization signal: about {0} m to the east-northeast.",
            "异常旧文明信号：东北偏东方向约 {0} 米。");
        Register("文明阶段补给恢复提示", "Civilization recovery stage supplies received.", "文明恢复阶段补给已回收。");
        Register("旧文明协议恢复提示", "Old-civilization protocol restored.", "旧文明协议已恢复。");
        Register("物流交互协议恢复提示", "Logistics interaction protocol restored.", "物流交互协议已恢复。");


        Register("T超值礼包1", "Electromagnetic Recovery Supplies", "电磁阶段补给");
        Register("超值礼包1描述",
            "Civilization recovery supplies released after the electromagnetic matrix stage is restored. Contains fragments and early growth materials.",
            "电磁矩阵阶段恢复后释放的文明补给，包含残片与前期成长物资。");
        Register("超值礼包1结果",
            "Fragments and early-stage growth supplies have been recovered.",
            "残片与前期成长物资已回收。");
        Register("一个物超所值的礼包", "Civilization recovery stage supplies", "文明恢复阶段补给");

        Register("T超值礼包2", "Energy Recovery Supplies", "能量阶段补给");
        Register("超值礼包2描述",
            "Civilization recovery supplies released after the energy matrix stage is restored. Contains fragments and early growth materials.",
            "能量矩阵阶段恢复后释放的文明补给，包含残片与前期成长物资。");
        Register("超值礼包2结果",
            "Fragments and early-stage growth supplies have been recovered.",
            "残片与前期成长物资已回收。");

        Register("T超值礼包3", "Structure Recovery Supplies", "结构阶段补给");
        Register("超值礼包3描述",
            "Civilization recovery supplies released after the structure matrix stage is restored. Contains fragments and mid-stage growth materials.",
            "结构矩阵阶段恢复后释放的文明补给，包含残片与中期成长物资。");
        Register("超值礼包3结果",
            "Fragments and mid-stage growth supplies have been recovered.",
            "残片与中期成长物资已回收。");

        Register("T超值礼包4", "Information Recovery Supplies", "信息阶段补给");
        Register("超值礼包4描述",
            "Civilization recovery supplies released after the information matrix stage is restored. Contains fragments and mid-stage growth materials.",
            "信息矩阵阶段恢复后释放的文明补给，包含残片与中期成长物资。");
        Register("超值礼包4结果",
            "Fragments and mid-stage growth supplies have been recovered.",
            "残片与中期成长物资已回收。");

        Register("T超值礼包5", "Gravity Recovery Supplies", "引力阶段补给");
        Register("超值礼包5描述",
            "Civilization recovery supplies released after the gravity matrix stage is restored. Contains fragments and late-stage growth materials.",
            "引力矩阵阶段恢复后释放的文明补给，包含残片与后期成长物资。");
        Register("超值礼包5结果",
            "Fragments and late-stage growth supplies have been recovered.",
            "残片与后期成长物资已回收。");

        Register("T超值礼包6", "Universe Recovery Supplies", "宇宙阶段补给");
        Register("超值礼包6描述",
            "Civilization recovery supplies released after the universe matrix stage is restored. Contains fragments and endgame growth materials.",
            "宇宙矩阵阶段恢复后释放的文明补给，包含残片与终局成长物资。");
        Register("超值礼包6结果",
            "Fragments and endgame growth supplies have been recovered.",
            "残片与终局成长物资已回收。");

        Register("T分馏塔原胚", "Fractionator Proto", "分馏塔原胚");
        Register("分馏塔原胚描述",
            "In the recovered fractionation system, new towers are cultivated from recovered protos instead of being crafted directly. Feed a matching proto through an Interaction Tower to obtain that tower type.",
            "在恢复后的分馏体系中，新分馏塔不再直接制造，而是由回收的原胚培养得到。将对应原胚送入交互塔，即可得到相应塔型。");
        Register("分馏塔原胚结果",
            "You have learned about the relevant information of the distillation tower precursor, and can combine different qualities of distillation tower precursor into directional distillation tower precursor.",
            "你已经了解了分馏塔原胚的相关信息，可以将分馏塔原胚培养为不同的分馏塔了。");
        Register("恢复全部建筑培养配方", "Restore all building train recipes", "恢复全部建筑培养配方");
        Register("给予一个交互塔", "Provide a Interactive Tower");

        Register("T物品交互", "Item Interaction", "物品交互");
        Register("物品交互描述",
            $"The old-civilization protocol lets the Interaction Tower convert physical items into data-centre records and recover selected records as items. The same tower also cultivates factionator protos into tower types.\n\n{"Upload an Interaction Tower to the Fractionation Data Centre to restore this protocol.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"旧文明协议允许交互塔把实体物品转换为数据中心记录，也可以把选中的记录重新取回为实体物品。同时，交互塔也承担了培养分馏塔原胚的职责。\n\n{"将交互塔上传至分馏数据中心即可恢复此协议。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("物品交互结果",
            "The item interaction protocol has been restored. The Interaction Tower can now interact with production lines.",
            "物品交互协议已恢复，现在可以用交互塔与产线交互了。");
        Register("自动上传被扔掉的物品", "Automatically upload dropped items");
        Register("双击背包排序按钮，自动上传背包内物品",
            "Double-click the backpack sort button to automatically upload the items within the backpack");

        Register("T矿物复制", "Resource Tower Control", "资源塔控制");
        Register("矿物复制描述",
            $"Registering a Resource Tower restores its control interface. Individual resource-replication recipes still require civilization protocol recovery.\n\n{"Upload a Resource Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"注册资源塔会恢复其控制接口；具体资源复制配方仍需通过文明协议恢复。\n\n{"将资源塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("矿物复制结果",
            "The Resource Tower has been registered. Recover individual recipe protocols through civilization analysis.",
            "资源塔已完成注册；请继续通过文明解析恢复具体配方协议。");
        Register("恢复部分矿物复制配方", "Restore some Mineral Replication recipes", "恢复部分矿物复制配方");

        Register("T物品转化", "Item Conversion", "物品转化");
        Register("物品转化描述",
            $"Registering a Conversion Tower restores its control interface. Individual conversion recipes still require civilization protocol recovery.\n\n{"Upload a Conversion Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"注册转化塔会恢复其控制接口；具体转化配方仍需通过文明协议恢复。\n\n{"将转化塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("物品转化结果",
            "The Conversion Tower has been registered. Recover individual recipe protocols through civilization analysis.",
            "转化塔已完成注册；请继续通过文明解析恢复具体配方协议。");

        Register("T物品精馏", "Civilization Analysis", "文明解析");
        Register("物品精馏描述",
            $"The Analysis Tower converts matrices into physical analysis data used for protocol recovery.\n\n{"Upload an Analysis Tower to the Fractionation Data Centre to register it.".WithColor(Orange)}\nSee the {"[G] key".WithColor(Orange)} guide for detailed instructions.",
            $"解析塔会将矩阵转化为实体解析数据，用于恢复配方协议。\n\n{"将解析塔上传至分馏数据中心即可完成注册。".WithColor(Orange)}\n查看{"[G]键".WithColor(Orange)}指引以了解详细信息。");
        Register("物品精馏结果",
            "The Analysis Tower has been registered. Matrices can now be converted into analysis data.",
            "解析塔已完成注册；现在可以将矩阵转化为解析数据。");


        Register("T行星内物流交互", "Planetary Logistics Interaction", "行星内物流交互");
        Register("行星内物流交互描述",
            "Planetary Logistics Interaction lets local logistics stations exchange items directly with the Fractionation Data Centre, reducing repeated manual hauling within the same planet.",
            "行星内物流交互协议允许本地物流站直接与分馏数据中心交换物品，减少同星球范围内的重复搬运与手动中转。");
        Register("行星内物流交互结果",
            "The planetary logistics interaction protocol has been restored. Local logistics stations can now interact with the Fractionation Data Centre.",
            "行星内物流交互协议已恢复，现在可以让本地物流站与分馏数据中心直接交互。");

        Register("T星际物流交互", "Interstellar Logistics Interaction", "星际物流交互");
        Register("星际物流交互描述",
            "Interstellar Logistics Interaction extends the same direct data-centre exchange to interstellar logistics stations, turning them into long-range item interaction hubs.",
            "星际物流交互协议把同样的直连交互能力扩展到星际物流站，使其成为跨星系的物资交互中枢。");
        Register("星际物流交互结果",
            "The interstellar logistics interaction protocol has been restored. Interstellar logistics stations can now interact with the Fractionation Data Centre.",
            "星际物流交互协议已恢复，现在可以让星际物流站与分馏数据中心直接交互。");
    }

    /// <summary>
    /// 添加所有科技。对于科技的位置，x向右y向下，间距固定为4
    /// </summary>
}
