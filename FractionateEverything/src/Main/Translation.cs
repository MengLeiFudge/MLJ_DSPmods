using static FractionateEverything.Utils.RegisterTranslationUtils;

namespace FractionateEverything.Main {
    public class Translation {
        public static void RegisterTranslations() {
            RegisterTranslation("分馏页面1", "Fractionate I", "分馏 I");
            RegisterTranslation("分馏页面2", "Fractionate II", "分馏 II");
            RegisterTranslation("分馏", " Fractionation");
            RegisterTranslation("从", "Fractionate ");
            RegisterTranslation("中分馏出", " to ");
            RegisterTranslation("。", ".");
            RegisterTranslation("分馏出", " fractionated ");
            RegisterTranslation("个产物", " product");
            RegisterTranslation("损毁分馏警告1",
                "<color=\"#FD965ECC\">WARNING: </color>This item is difficult to fractionate and has a ",
                "<color=\"#FD965ECC\">警告：</color>该物品难以分馏，有");
            RegisterTranslation("损毁分馏警告2",
                " chance of being destroyed!",
                "概率损毁！");
            RegisterTranslation("流动", "Flow");
            RegisterTranslation("损毁", "Destroy");

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
                "Crafts an item with 10 Incremental Yield Points by concentrating the item's Incremental Yield Points on a portion of the item, breaking the upper limit of Incremental Yield Points. Accepts any item as a raw material.",
                "将物品的增产点数集中到一部分物品上，突破增产点数的上限，从而制作出10增产点数的物品。可接受任何物品作为原料。");

            RegisterTranslation("增产分馏塔", "Increase Production Fractionator");
            RegisterTranslation("I增产分馏塔",
                "Instead of boosting the success rate of fractionation, the production increase points boost the number of products, truly achieving the effect of creating something from nothing. Accepts any item as a raw material.",
                "增产点数不再提升分馏成功率，而是提升产物数目，真正达到无中生有的效果。可接受任何物品作为原料。");

            RegisterTranslation("T分馏塔产物集装物流", "Fractionator product consolidation logistics", "分馏塔产物集装物流");
            RegisterTranslation("分馏塔产物集装物流描述1",
                "The product of the fractionator will be exported as much as possible in a containerized form.",
                "分馏塔的产物将会尽可能以集装形式输出。");
            RegisterTranslation("分馏塔产物集装物流结果1",
                "All products from the fractionator will now be consolidated as much as possible before being exported.",
                "现在，所有分馏塔的产物都将尽可能集装后再输出。");
            RegisterTranslation("分馏塔产物集装物流描述2",
                "Further increase the number of sets of output products from the fractionator.",
                "进一步提高分馏塔输出产物的集装数量。");
            RegisterTranslation("分馏塔产物集装物流结果2",
                "The number of product assemblies in all fractionators was further improved.",
                "所有分馏塔的产物集装数量进一步提升了。");
            RegisterTranslation("分馏塔产物集装等级",
                "Quantity of output product set from fractionator",
                "分馏塔输出产物集装数量");

            RegisterTranslation("T增产点数聚集", "Aggregation of yield points", "增产点数聚集");
            RegisterTranslation("增产点数聚集描述",
                "Due to the limitations of material technology, the spawn line is unable to create more advanced yield enhancers, but fractionation technology can break through the limitations by concentrating the raw material's yield enhancement points into a certain number of items. It was found that the yield increase points of items could be stacked indefinitely, but the portion over 10 points did not work. The Yield Increase Point Aggregation technique can fractionate just the items with 10 Yield Increase Points.",
                "由于材料技术的限制，产线无法制造更高级的增产剂，但分馏技术可以将原料的增产点数集中到某几个物品上，从而突破限制。研究发现，物品的增产点数可以无限叠加，但超过10点的部分不起作用。增产点数聚集技术可以刚好分馏出10点增产点数的物品。");
            RegisterTranslation("增产点数聚集结果",
                "You have mastered the yield increase point aggregation technique. The item's yield increase points can now be pushed to the limit, and production capacity has been greatly increased!",
                "你已经掌握了增产点数聚集技术。现在物品的增产点数可以突破到极限，产能得到了极大的提升！");

            RegisterTranslation("T增产分馏", "Increase production fractionate", "增产分馏");
            RegisterTranslation("增产分馏描述",
                "Typically, yield increase points only boost the fractionation success rate. If the yield increase points are used to boost the number of fractionation products and keep the products consistent with the inputs, we can achieve the effect of creating something out of nothing.\n"
                + "It is clear that the direction of transforming yield-enhancing points in fractionation is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
                + "<color=\"#FD965ECC\">Warning：</color>The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, <color=\"#FD965ECC\">Please research manually.</color>",
                "通常情况下，增产点数只能提升分馏成功率。如果将增产点数用于提升分馏产物的数目，并保持产物与输入一致，我们就能达到无中生有的效果。\n"
                + "显然，转变增产点数在分馏中的应用方向高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
                + "<color=\"#FD965ECC\">警告：</color>该科技的相关技术已被COSMO技术伦理委员会禁用，<color=\"#FD965ECC\">请手动研究。</color>");
            RegisterTranslation("增产分馏结果",
                "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
                "你已经掌握了增产分馏技术。现在，你真正拥有了无中生有的能力！");
        }
    }
}
