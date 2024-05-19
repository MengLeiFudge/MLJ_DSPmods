using CommonAPI.Systems.ModLocalization;

namespace FractionateEverything.Main {
    public static class Translation {
        private static void RegisterTranslation(string key, string enTrans, string cnTrans = null) {
            LocalizationModule.RegisterTranslation(key, enTrans, cnTrans ?? key, enTrans);
        }

        private static void EditTranslation(string key, string enTrans, string cnTrans = null) {
            LocalizationModule.EditTranslation(key, enTrans, cnTrans ?? key, enTrans);
        }

        public static void AddTranslations() {
            RegisterTranslation("DisableMessageBox", "Disable message box when loaded", "禁用万物分馏提示信息");
            RegisterTranslation("DisableMessageBoxAdditionalText",
                "(Disable additional information about Fractionate Everything in a messagebox after the mod is loaded)",
                "（在MOD加载完成后禁用弹窗显示万物分馏的额外信息）");
            RegisterTranslation("IconVersion", "Fractionation recipe icon style", "分馏配方图标样式");
            RegisterTranslation("v1", "Original deuterium fractionate(need restart)", "原版重氢分馏（需要重启）");
            RegisterTranslation("v2", "Slanting line segmentation(need restart)", "斜线分割（需要重启）");
            RegisterTranslation("v3", "Circular segmentation(need restart)", "圆弧分割（需要重启）");
            RegisterTranslation("EnableDestroy", "Enable fractionation destroy probability", "启用分馏损毁概率");
            RegisterTranslation("EnableDestroyAdditionalText",
                "(only affects some recipes, recommended to open, requires restart game to take effect)",
                "（只影响部分配方，建议开启，需要重启游戏才可生效）");

            RegisterTranslation("FE标题", "Fractionate Everything Mod Tips", "万物分馏提示");
            RegisterTranslation("FE信息",
                "Thank you for using Fractionation Everything! This mod adds 5 Fractionators, and over 200 fractionation recipes.\n"
                + "If you are using this mod for the first time, it is highly recommended that you check out the module introduction page to get an idea of its contents and features.\n"
                + "You can change mod options in Settings - Miscellaneous, such as change fractionate recipe icon style (currently there are 3 styles available), or no longer show this window.\n"
                + "Recommended for use with Genesis Book, They Come From Void, and More Mega Structure.\n"
                + "If you have any issues or ideas about the mod, please feedback to Github Issue.\n"
                + "<color=\"#FD965ECC\">Have fun with fractionation!</color>",
                "感谢你使用万物分馏！该MOD添加了5个分馏塔，以及超过200个的分馏配方。\n"
                + "如果你是首次使用该模组，强烈建议你查看模组介绍页面以了解其内容与特色。\n"
                + "你可以在设置-杂项中修改MOD配置，例如切换分馏配方的图标样式（目前有3种样式可选），或不再显示该窗口。\n"
                + "推荐与创世之书、深空来敌、更多巨构一起使用。\n"
                + "如果你在游玩MOD时遇到了任何问题，或者有宝贵的意见或建议，欢迎加入创世之书MOD交流群反馈。\n"
                + "<color=\"#FD965ECC\">尽情享受分馏的乐趣吧！</color>");
            RegisterTranslation("FE交流群", "View on Github", "加入交流群");
            RegisterTranslation("FE交流群链接",
                "https://github.com/MengLeiFudge/MLJ_DSPmods",
                "https://jq.qq.com/?_wv=1027&k=5bnaDEp3");
            RegisterTranslation("FE日志", "Update Log", "更新日志");
            RegisterTranslation("FE日志链接",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/");

            RegisterTranslation("分馏页面1", "Fractionate I", "分馏 I");
            RegisterTranslation("分馏页面2", "Fractionate II", "分馏 II");
            RegisterTranslation("分馏", " Fractionation");
            RegisterTranslation("从", "Fractionate ");
            RegisterTranslation("中分馏出", " to ");
            RegisterTranslation("。", ".");
            RegisterTranslation("分馏出", " fractionate ");
            RegisterTranslation("个产物", " product");
            RegisterTranslation("损毁分馏警告1",
                "<color=\"#FD965ECC\">WARNING: </color>This item is difficult to fractionate and has a ",
                "<color=\"#FD965ECC\">警告：</color>该物品难以分馏，有");
            RegisterTranslation("损毁分馏警告2",
                " chance of being destroyed!",
                "概率损毁！");
            RegisterTranslation("流动", "Flow");
            RegisterTranslation("损毁", "Destroy");
            EditTranslation("R重氢分馏",
                "The successful fractionation of deuterium from liquid hydrogen has greatly advanced the utilization of nuclear fuel.\n1% fractionate 1 product",
                "成功从液态氢中分馏出重氢，极大地推动了核燃料的使用。\n1%分馏出一个产物");

            RegisterTranslation("精准分馏塔", "Precision Fractionator");
            RegisterTranslation("I精准分馏塔",
                "It can accurately fractionate the target product with low power consumption. Due to the special characteristics of the structure, it cannot fractionate a large number of items at the same time. The more items need to be fractionated at the same time, the lower the fractionation success rate.",
                "可以精准分馏出目标产物，耗电较低。由于结构的特殊性，它无法同时分馏大量物品。需要同时分馏的物品数越多，则分馏成功率越低。");

            RegisterTranslation("建筑极速分馏塔", "Building-HighSpeed Fractionator");
            RegisterTranslation("I建筑极速分馏塔",
                "Quickly converts low-level buildings to high-level buildings, but non-building items are extremely inefficient to fractionate. You should carefully verify that the item being fractionated is not a building before fractionating.",
                "快速将低级建筑转换为高级建筑，但非建筑物品分馏效率极低。分馏前应仔细确认被分馏的物品是不是建筑。");

            RegisterTranslation("通用分馏塔", "Universal Fractionator");
            RegisterTranslation("I通用分馏塔",
                "It is heavily used for deuterium fractionation by Icarus in all parts of the universe. In fact, the Universal Fractionator has unlimited potential, as it can fractionate everything in the universe!",
                "被宇宙各处的伊卡洛斯大量用于重氢分馏。实际上，通用分馏塔潜力无限，因为它可以分馏宇宙万物！");

            RegisterTranslation("点数聚集分馏塔", "Points Aggregate Fractionator");
            RegisterTranslation("I点数聚集分馏塔",
                "Crafts an item with 10 proliferator points by concentrating the item's proliferator points on a portion of the item, breaking the upper limit of proliferator points.\n<color=\"#FD965ECC\">Any item will be accepted.</color> The success rate is only related to the number of proliferator points entered for the item.",
                "将物品的增产点数集中到一部分物品上，突破增产点数的上限，从而制作出10增产点数的物品。\n<color=\"#FD965ECC\">可接受任何物品。</color>成功率仅与输入物品的增产点数有关。");

            RegisterTranslation("增产分馏塔", "Increase Production Fractionator");
            RegisterTranslation("I增产分馏塔",
                "Instead of boosting the success rate of fractionation, the production increase points boost the number of products, truly achieving the effect of creating something from nothing.\n<color=\"#FD965ECC\">Any item will be accepted.</color> The success rate is related to the number of proliferator points entered for the item, and whether or not it has a self-fractionating recipe.",
                "增产点数不再提升分馏成功率，而是提升产物数目，真正达到无中生有的效果。\n<color=\"#FD965ECC\">可接受任何物品。</color>成功率与输入物品的增产点数、是否具有自分馏配方有关。");

            RegisterTranslation("T分馏塔产物集装物流", "Fractionator Product Integrated Count Logistics", "分馏塔产物集装物流");
            RegisterTranslation("分馏塔产物集装物流描述1",
                "Fractionator products will be exported as much as possible in product outgoing cargo.",
                "分馏塔的产物将会尽可能以集装形式输出。");
            RegisterTranslation("分馏塔产物集装物流结果1",
                "All products from the fractionator will now be exported as much as possible in product outgoing cargo.",
                "现在，所有分馏塔的产物都将尽可能集装后再输出。");
            RegisterTranslation("分馏塔产物集装物流描述2",
                "Further increases the integration count of product cargo for Fractionators.",
                "进一步提高分馏塔输出产物的集装数量。");
            RegisterTranslation("分馏塔产物集装物流结果2",
                "The integration count of product cargo for all Fractionators was further improved.",
                "所有分馏塔的产物集装数量进一步提升了。");
            RegisterTranslation("分馏塔产物集装等级",
                "integration count from fractionator product output",
                "分馏塔输出产物集装数量");

            RegisterTranslation("T增产点数聚集", "Proliferator Points Aggregation", "增产点数聚集");
            RegisterTranslation("增产点数聚集描述",
                "Due to the limitations of material technology, the spawn line is unable to create more advanced proliferators, but fractionation technology can break through the limitations by concentrating the raw material's proliferator points into a certain number of items. It was found that the proliferator points of items could be stacked indefinitely, but the portion over 10 points did not work. Proliferate Point Aggregation technology can fractionate just the items with 10 proliferator points.",
                "由于材料技术的限制，产线无法制造更高级的增产剂，但分馏技术可以将原料的增产点数集中到某几个物品上，从而突破限制。研究发现，物品的增产点数可以无限叠加，但超过10点的部分不起作用。增产点数聚集技术可以刚好分馏出10点增产点数的物品。");
            RegisterTranslation("增产点数聚集结果",
                "You have mastered the Proliferator Points Aggregation technology. The item's proliferator points can now be pushed to the limit, and production capacity has been greatly increased!",
                "你已经掌握了增产点数聚集技术。现在物品的增产点数可以突破到极限，产能得到了极大的提升！");

            RegisterTranslation("T增产分馏", "Increase production fractionate", "增产分馏");
            RegisterTranslation("增产分馏描述",
                "Typically, proliferator points only boost the fractionation success rate. If proliferator points are used to boost the number of fractionation products and keep the products consistent with the inputs, we can achieve the effect of creating something out of nothing.\n"
                + "It is clear that the direction of transforming proliferator points in fractionation is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
                + "<color=\"#FD965ECC\">Warning：</color>The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, <color=\"#FD965ECC\">Please research manually.</color>",
                "通常情况下，增产点数只能提升分馏成功率。如果将增产点数用于提升分馏产物的数目，并保持产物与输入一致，我们就能达到无中生有的效果。\n"
                + "显然，转变增产点数在分馏中的应用方向高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
                + "<color=\"#FD965ECC\">警告：</color>该科技的相关技术已被COSMO技术伦理委员会禁用，<color=\"#FD965ECC\">请手动研究。</color>");
            RegisterTranslation("增产分馏结果",
                "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
                "你已经掌握了增产分馏技术。现在，你真正拥有了无中生有的能力！");

            RegisterTranslation("粒子对撞与通用分馏", "Particle Collisions and Universal Fractionation");
            EditTranslation("微型粒子对撞机描述",
                "Building a particle collider requires advanced materials as well as a huge energy supply, which is expensive, but worth it. Or, consider fractionation?",
                "建造一座粒子对撞机需要先进的材料以及巨大的能量供给，虽然成本昂贵，不过却是值得的。或者，考虑一下分馏？");
            EditTranslation("微型粒子对撞机结果",
                "You have unlocked the Particle Collider, which can now be utilized to create deuterium and antimatter at a stable rate, though it consumes a lot of energy. You have also unlocked the Universal Fractionator, which has a more stable fractionation rate.",
                "你解锁了粒子对撞机，现在可以利用它来稳定制造重氢和反物质，不过会消耗很多能量。你也解锁了通用分馏塔，它的分馏速率更为稳定。");
        }
    }
}
