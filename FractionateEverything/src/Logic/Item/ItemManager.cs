using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using xiaoye97;
using static FE.Utils.ProtoID;

namespace FE.Logic;

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

        protoList.Add(fractionalPrototype);
        protoList.Add(damagedFractionalPrototype);
        // 添加到游戏中
        AddProtoUtils.AddItem(protoList);
    }
}
