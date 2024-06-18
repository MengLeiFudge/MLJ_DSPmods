using FractionateEverything.Compatibility;
using static FractionateEverything.Utils.TranslationUtils;

namespace FractionateEverything.Main {
    public static class Translation {
        public static void AddTranslations() {

            #region 加载完成弹窗提示

            Register("FE标题", "Fractionate Everything Mod Tips", "万物分馏提示");
            Register("FE信息",
                "Thank you for using Fractionation Everything! This mod adds 6 Fractionators, and over 200 fractionation recipes.\n"
                + "If you are using this mod for the first time, it is highly recommended that you check out the module introduction page to get an idea of its contents and features.\n"
                + "You can change mod options in Settings - Miscellaneous, such as change fractionate recipe icon style (currently there are 3 styles available), or no longer show this window.\n"
                + "Recommended for use with Genesis Book, They Come From Void, and More Mega Structure.\n"
                + "If you have any issues or ideas about the mod, please feedback to Github Issue.\n"
                + "Have fun with fractionation!".AddOrangeLabel(),
                "感谢你使用万物分馏！该MOD添加了6个分馏塔，以及超过200个的分馏配方。\n"
                + "如果你是首次使用该模组，强烈建议你查看模组介绍页面以了解其内容与特色。\n"
                + "你可以在设置-杂项中修改MOD配置，例如切换分馏配方的图标样式（目前有3种样式可选），或不再显示该窗口。\n"
                + "推荐与创世之书、深空来敌、更多巨构一起使用。\n"
                + "如果你在游玩MOD时遇到了任何问题，或者有宝贵的意见或建议，欢迎加入创世之书MOD交流群反馈。\n"
                + "尽情享受分馏的乐趣吧！".AddOrangeLabel());
            Register("FE交流群", "View on Github", "加入交流群");
            Register("FE交流群链接",
                "https://github.com/MengLeiFudge/MLJ_DSPmods",
                "https://jq.qq.com/?_wv=1027&k=5bnaDEp3");
            Register("FE日志", "Update Log", "更新日志");
            Register("FE日志链接",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/",
                "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/");

            #endregion

            #region 设置页面

            Register("DisableMessageBox", "Disable message box when loaded", "禁用万物分馏提示信息");
            Register("DisableMessageBoxAdditionalText",
                "(Disable additional information about Fractionate Everything in a messagebox after the mod is loaded)",
                "（在MOD加载完成后禁用弹窗显示万物分馏的额外信息）");
            Register("IconVersion", "Fractionation recipe icon style", "分馏配方图标样式");
            Register("v1", "Original deuterium fractionate", "原版重氢分馏");
            Register("v2", "Slanting line segmentation", "斜线分割");
            Register("v3", "Circular segmentation", "圆弧分割");
            Register("IconVersionAdditionalText",
                "(need reload save for full effect)",
                "（需要重新载入存档才能完全生效）");
            Register("EnableDestroy", "Enable fractionation destroy probability", "启用分馏损毁概率");
            Register("EnableDestroyAdditionalText",
                "(only affects some recipes, recommended to open, need reload save for full effect)",
                "（只影响部分配方，建议开启，需要重新载入存档才能完全生效）");

            #endregion

            #region 游戏内切换&配方显示

            Register("切换快捷键", "CapsLock\n↑ Hotkey Row ↓", "CapsLock\n↑快捷键切换↓");

            Register("分馏页面1", "Fractionate I", "分馏 I");
            Register("分馏页面2", "Fractionate II", "分馏 II");
            Register("流动", "Flow");
            Register("损毁", "Destroy");

            #endregion

            #region 建筑说明

            Register("精准分馏塔", "Precision Fractionator");
            Register("I精准分馏塔",
                "It can accurately fractionate the target product with low power consumption. Due to the special characteristics of the structure, it cannot fractionate a large number of items at the same time. The lower the number of items to be processed inside the fractionator, the higher the success rate of fractionation.",
                "可以精准分馏出目标产物，耗电较低。由于结构的特殊性，它无法同时分馏大量物品。分馏塔内部需要处理的物品数目越少，分馏的成功率就越高。");

            Register("建筑极速分馏塔", "Building-HighSpeed Fractionator");
            Register("I建筑极速分馏塔",
                "Quickly converts low-level buildings to high-level buildings, but non-building items are extremely inefficient to fractionate. You should carefully verify that the item being fractionated is not a building before fractionating.",
                "快速将低级建筑转换为高级建筑，但非建筑物品分馏效率极低。分馏前应仔细确认被分馏的物品是不是建筑。");

            Register("垃圾回收分馏塔", "Trash Recycle Fractionator");
            Register("I垃圾回收分馏塔",
                $"Converts unwanted items into foundation or sand. All connections can be input, but only {"the front one".AddOrangeLabel()} can be output.\n{"Any item will be accepted.".AddOrangeLabel()}",
                $"将不需要的物品转换为地基或沙土。所有连接口都可输入，但只有{"正面".AddOrangeLabel()}的连接口可以输出。\n{"可接受任何物品。".AddOrangeLabel()}");

            Register("通用分馏塔", "Universal Fractionator");
            Register("I通用分馏塔",
                "It is heavily used for deuterium fractionation by Icarus in all parts of the universe. In fact, the Universal Fractionator has unlimited potential, as it can fractionate everything in the universe!",
                "被宇宙各处的伊卡洛斯大量用于重氢分馏。实际上，通用分馏塔潜力无限，因为它可以分馏宇宙万物！");

            Register("点数聚集分馏塔", "Points Aggregate Fractionator");
            Register("I点数聚集分馏塔",
                $"Crafts an item with 10 proliferator points by concentrating the item's proliferator points on a portion of the item, breaking the upper limit of proliferator points.\n{"Any item will be accepted.".AddOrangeLabel()} The success rate is only related to the number of proliferator points entered for the item.",
                $"将物品的增产点数集中到一部分物品上，突破增产点数的上限，从而制作出10增产点数的物品。\n{"可接受任何物品。".AddOrangeLabel()}成功率仅与输入物品的增产点数有关。");

            Register("增产分馏塔", "Increase Production Fractionator");
            Register("I增产分馏塔",
                $"Instead of boosting the success rate of fractionation, the production increase points boost the number of products, truly achieving the effect of creating something from nothing.\n{"Any item will be accepted.".AddOrangeLabel()} The success rate is related to the number of proliferator points entered for the item, and whether or not it has a self-fractionating recipe.",
                $"增产点数不再提升分馏成功率，而是提升产物数目，真正达到无中生有的效果。\n{"可接受任何物品。".AddOrangeLabel()}成功率与输入物品的增产点数、是否具有自分馏配方有关。");

            #endregion

            #region 建筑解锁科技

            Register("T精准分馏", "Precision Fractionation", "精准分馏");
            Register("精准分馏描述",
                "Fractionation is an advanced technology with many details to explore. Typically, fractionation success is only affected by the production enhancer, and the faster the input the higher the efficiency. However, current production lines are not capable of manufacturing high grade conveyor belts, nor are they capable of consolidation. Precision Fractionation was born, the slower the input, the higher the success rate, and the lower the power consumption, which is just right for the early stage of exploration.",
                "分馏是一门高级技术，有很多细节值得探讨。一般情况下，分馏成功率仅受增产剂影响，输入越快效率越高。然而目前产线无法制造高等级传送带，也无法集装。精准分馏技术随之诞生，输入越慢成功率越高，且耗电较低，刚好适合在探索初期使用。");
            Register("精准分馏结果",
                "You have mastered the Precision Fractionation technology and taken the first step on the path of discovery of fractionation from scratch. Congratulations!",
                "你已经掌握了精准分馏技术，在分馏的探索之路上迈出了从无到有的第一步。恭喜！");

            Register("T建筑极速分馏", "Building-HighSpeed Fractionation", "建筑极速分馏");
            Register("建筑极速分馏描述",
                "Although buildings could be upgraded through fractionation, the efficiency was too low, and it was far inferior to directly creating new buildings. As it happens, the mastermind has long considered this situation, and the Building Extreme Fractionation technology can quickly convert low-level buildings into high-level buildings, saving a lot of time. Note that this technology is a specialization for buildings, and is rather inferior to other fractionators for non-buildings.",
                "虽然可以通过分馏的方式升级建筑，但是效率未免过于低下，远不如直接制造新的建筑。恰好，主脑早就考虑到了这种情况，建筑极速分馏技术可以快速把低级建筑转换为高级建筑，省去大量时间。注意，这项技术是对建筑的特化处理，对非建筑反而不如其他分馏塔。");
            Register("建筑极速分馏结果",
                "You have mastered the Building-HighSpeed Fractionation technology, which allows you to quickly convert various buildings with the Building-HighSpeed Fractionator.",
                "你已经掌握了建筑极速分馏技术，可以用建筑极速分馏塔迅速转换各种建筑。");

            Register("T垃圾回收", "Trash Recycle", "垃圾回收");
            Register("垃圾回收描述",
                "Foundations and sand are an essential part of the exploration process. Trash pickup allows you to dispose of any item as foundation or sand, which is helpful for expanding into new terrain.",
                "地基和沙土是探索过程中必不可少的一环。垃圾回收可以将任意的物品处理为地基或沙土，对新地盘的扩展很有帮助。");
            Register("垃圾回收结果",
                "You have mastered the Trash Recycle technology and can recycle unwanted items, converting them into foundations or sand.",
                "你已经掌握了垃圾回收技术，可以回收不需要的物品，将其转换为地基或沙土。");

            if (GenesisBook.Enable) {
                Register("T粒子对撞与通用分馏", "Particle Collisions and Universal Fractionation", "粒子对撞与通用分馏");
                Register("粒子对撞与通用分馏描述",
                    "Building a particle collider requires advanced materials as well as a huge energy supply, which is expensive, but worth it. Or, consider fractionation? Universal Fractionation is also a good choice, and it is uniquely suited to handling integrated cargo.",
                    "建造一座粒子对撞机需要先进的材料以及巨大的能量供给，虽然成本昂贵，不过却是值得的。或者，考虑一下分馏？通用分馏也是个不错的选择，它在处理集装货物方面有着独特的优势。");
                Register("粒子对撞与通用分馏结果",
                    "You have unlocked the Particle Collider, which can now be utilized to create deuterium and antimatter at a stable rate, though it consumes a lot of energy. You have also unlocked the Universal Fractionator, so you can use it to handle integrated cargo.",
                    "你解锁了粒子对撞机，现在可以利用它来稳定制造重氢和反物质，不过会消耗很多能量。你也解锁了通用分馏塔，可以用它处理集装货物了。");
            }
            else {
                Register("T通用分馏", "Universal Fractionation", "通用分馏");
                Register("通用分馏描述",
                    "Universal fractionation technique is the most widely used fractionation technique in the universe. The Precision Fractionator will not work in a situation where belt MK3 is used and the items are integrated, but the Universal Fractionator can handle such a situation.",
                    "通用分馏技术是宇宙中应用最广泛的分馏技术。在使用极速传送带且集装物品的情况下，精准分馏塔无法工作，而通用分馏塔可以处理这样的情况。");
                Register("通用分馏结果",
                    "You have mastered the Universal Fractionation technology, and now you can handle the integrated items.",
                    "你已经掌握了通用分馏技术，现在可以处理集装的货物了。");
            }

            Register("T增产点数聚集", "Proliferator Points Aggregation", "增产点数聚集");
            Register("增产点数聚集描述",
                "Due to the limitations of material technology, the spawn line is unable to create more advanced proliferators, but fractionation technology can break through the limitations by concentrating the raw material's proliferator points into a certain number of items. It was found that the proliferator points of items could be stacked indefinitely, but the portion over 10 points did not work. Proliferate Point Aggregation technology can fractionate just the items with 10 proliferator points.",
                "由于材料技术的限制，产线无法制造更高级的增产剂，但分馏技术可以将原料的增产点数集中到某几个物品上，从而突破限制。研究发现，物品的增产点数可以无限叠加，但超过10点的部分不起作用。增产点数聚集技术可以刚好分馏出10点增产点数的物品。");
            Register("增产点数聚集结果",
                "You have mastered the Proliferator Points Aggregation technology. The item's proliferator points can now be pushed to the limit, and production capacity has been greatly increased!",
                "你已经掌握了增产点数聚集技术。现在物品的增产点数可以突破到极限，产能得到了极大的提升！");

            Register("T增产分馏", "Increase production fractionate", "增产分馏");
            Register("增产分馏描述",
                "Typically, proliferator points only boost the fractionation success rate. If proliferator points are used to boost the number of fractionation products and keep the products consistent with the inputs, we can achieve the effect of creating something out of nothing.\n"
                + "It is clear that the direction of transforming proliferator points in fractionation is highly uncontrollable. This research exists only in anecdotal evidence and whether it can be done is still unknown.\n"
                + $"{"Warning:".AddOrangeLabel()} The technology associated with this technology has been banned by the COSMO Technology Ethics Committee, {"Please research manually.".AddOrangeLabel()}",
                "通常情况下，增产点数只能提升分馏成功率。如果将增产点数用于提升分馏产物的数目，并保持产物与输入一致，我们就能达到无中生有的效果。\n"
                + "显然，转变增产点数在分馏中的应用方向高度不可控。这项研究仅存在于在传闻中，能否做到是仍是未知。\n"
                + $"{"警告：".AddOrangeLabel()}该科技的相关技术已被COSMO技术伦理委员会禁用，{"请手动研究。".AddOrangeLabel()}");
            Register("增产分馏结果",
                "You have unlocked the Increased Production Fractionation technology. Now you truly have the ability to create something from nothing!",
                "你已经掌握了增产分馏技术。现在，你真正拥有了无中生有的能力！");

            #endregion

            #region 分馏塔效果加强科技

            Register("T分馏塔流动输出集装", "Fractionator Fluid Output Integrate", "分馏塔流动输出集装");
            Register("分馏塔流动输出集装描述",
                $"Failed fractionated items will be integrated as much as possible in a cargo. {"Precision Fractionator is not affected by this technology.".AddOrangeLabel()}",
                $"分馏失败的原料将会尽可能以集装形式输出。{"精准分馏塔不受此科技影响。".AddOrangeLabel()}");
            Register("分馏塔流动输出集装结果",
                "All failed fractionated items will now be integrated as much as possible in a cargo. Precision Fractionator is not affected.",
                "现在，所有分馏失败的原料都将尽可能集装后再输出。精准分馏塔不受影响。");
            Register("分馏塔流动输出集装等级",
                " integration count of Fractionator fluid output",
                "分馏塔流动输出集装数量");

            Register("T分馏塔产物输出集装", "Fractionator Product Output Integrate", "分馏塔产物输出集装");
            Register("分馏塔产物输出集装描述1",
                "Successful fractionated items will be integrated as much as possible in a cargo.",
                "分馏成功的产物将会尽可能以集装形式输出。");
            Register("分馏塔产物输出集装结果1",
                "All successful fractionated items will now be integrated as much as possible in a cargo.",
                "现在，所有分馏成功的产物都将尽可能集装后再输出。");
            Register("分馏塔产物输出集装描述2",
                "Further increases the integration count of product output in a cargo for all Fractionators.",
                "进一步提高分馏塔产物输出的集装数量。");
            Register("分馏塔产物输出集装结果2",
                "the integration count of product output in a cargo for all Fractionators was further improved.",
                "所有分馏塔产物输出的集装数量进一步提升了。");
            Register("分馏塔产物输出集装等级",
                " integration count of Fractionator product output",
                "分馏塔产物输出集装数量");

            #endregion

        }
    }
}
