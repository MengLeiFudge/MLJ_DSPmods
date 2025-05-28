using FE.Utils;
using System.Collections.Generic;

namespace FE.Logic.Item;

public static class ItemManager {
    public static void AddFractionalPrototypes() {
        List<Proto> protoList = new List<Proto>();

        // 创建分馏原胚
        ItemProto fractionalPrototype = new ItemProto {
            Name = "分馏原胚",
            ID = 2548,
            Type = EItemType.Component,
            IconPath = "Assets/FractionateEverything/Icons/fractional-prototype.png",
            GridIndex = 2301,// 需根据实际情况调整
            StackSize = 30
        };

        // 创建分馏原胚(破损)
        ItemProto damagedFractionalPrototype = new ItemProto {
            Name = "分馏原胚（破损）",
            ID = 2549,
            Type = EItemType.Component,
            IconPath = "Assets/FractionateEverything/Icons/damaged-fractional-prototype.png",
            GridIndex = 2302,// 需根据实际情况调整
            StackSize = 30
        };

        // 创建分解精华（基础）
        ItemProto basicEssence = new ItemProto {
            Name = "基础分解精华",
            ID = 2550,
            Type = EItemType.Component,
            IconPath = "Assets/FractionateEverything/Icons/basic-essence.png",
            GridIndex = 2303,
            StackSize = 100
        };

        // 创建分解精华（高级）
        ItemProto advancedEssence = new ItemProto {
            Name = "高级分解精华",
            ID = 2551,
            Type = EItemType.Component,
            IconPath = "Assets/FractionateEverything/Icons/advanced-essence.png",
            GridIndex = 2304,
            StackSize = 100
        };

        // 创建分解精华（稀有）
        ItemProto rareEssence = new ItemProto {
            Name = "稀有分解精华",
            ID = 2552,
            Type = EItemType.Component,
            IconPath = "Assets/FractionateEverything/Icons/rare-essence.png",
            GridIndex = 2305,
            StackSize = 50
        };

        protoList.Add(fractionalPrototype);
        protoList.Add(damagedFractionalPrototype);
        protoList.Add(basicEssence);
        protoList.Add(advancedEssence);
        protoList.Add(rareEssence);

        // 添加到游戏中
        AddProtoUtils.AddItem(protoList);
    }
}
