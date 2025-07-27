using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;
using Object = UnityEngine.Object;

namespace FE.UI.Patches;

public static class IconSetPatch {
    // /// <summary>
    // /// 创世使用的方法，2000之后的科技图标不再添加到图标集。
    // /// </summary>
    // [HarmonyPatch(typeof(IconSet), nameof(IconSet.Create))]
    // [HarmonyTranspiler]
    // public static IEnumerable<CodeInstruction> IconSet_Create_Transpiler(
    //     IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
    //     var matcher = new CodeMatcher(instructions);
    //
    //     matcher.MatchForward(false,
    //         new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(TechProto), nameof(TechProto.iconSprite))));
    //
    //     object label = matcher.InstructionAt(5).operand;
    //
    //     object index_V_23 = matcher.Advance(-2).Operand;
    //     object dataArray3 = matcher.Advance(-1).Operand;
    //
    //     matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, dataArray3), new CodeInstruction(OpCodes.Ldloc_S, index_V_23),
    //         new CodeInstruction(OpCodes.Ldelem_Ref),
    //         new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(IconSetPatch), nameof(IconSet_Create_Patch))),
    //         new CodeInstruction(OpCodes.Brtrue_S, label));
    //
    //     return matcher.InstructionEnumeration();
    // }
    //
    // public static bool IconSet_Create_Patch(TechProto proto) {
    //    return proto.ID < 2000;
    // }

    /// <summary>
    /// 升级科技（也就是ID>2000的科技）仅添加最后一个图标到图标集。
    /// </summary>
    /// <details>
    /// 图标集指传送带的可选显示图标，不影响游戏其他显示。
    /// </details>
    [HarmonyPatch(typeof(IconSet), nameof(IconSet.Create))]
    [HarmonyPrefix]
    public static bool IconSet_Create_Prefix(ref IconSet __instance) {
        if (__instance.loaded)
            return false;
        HighStopwatch highStopwatch = new HighStopwatch();
        highStopwatch.Begin();
        __instance.texture = new Texture2D(2000, 2000, TextureFormat.ARGB32, true);
        __instance.itemIconIndex = new uint[12000];
        __instance.recipeIconIndex = new uint[12000];
        __instance.techIconIndex = new uint[12000];
        __instance.signalIconIndex = new uint[60000];
        __instance.itemDescArr = new float[25000];
        __instance.itemIconIndexBuffer = new ComputeBuffer(12000, 4, ComputeBufferType.Default);
        __instance.recipeIconIndexBuffer = new ComputeBuffer(12000, 4, ComputeBufferType.Default);
        __instance.techIconIndexBuffer = new ComputeBuffer(12000, 4, ComputeBufferType.Default);
        __instance.signalIconIndexBuffer = new ComputeBuffer(60000, 4, ComputeBufferType.Default);
        __instance.itemIconDescBuffer = new ComputeBuffer(25000, 4, ComputeBufferType.Default);
        __instance.spriteIndexMap = new Dictionary<Sprite, uint>();
        __instance.texture.SetPixels(new Color[4000000]);
        __instance.texture.Apply();
        uint num1 = 0;
        ItemProto[] dataArray1 = LDB.items.dataArray;
        int length1 = dataArray1.Length;
        for (int index = 0; index < length1; ++index) {
            uint num2 = 0;
            Sprite iconSprite = dataArray1[index].iconSprite;
            if ((Object)iconSprite != (Object)null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num2 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num2 = ++num1;
                    if (num2 >= 625U) {
                        num2 = 0U;
                        Debug.LogWarning((object)"图标图集空间不足！");
                    } else {
                        int num3 = (int)num2 % 25;
                        int num4 = (int)num2 / 25;
                        Graphics.CopyTexture((Texture)dataArray1[index].iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, (Texture)__instance.texture, 0, 0, num3 * 80/*0x50*/, num4 * 80/*0x50*/);
                        LogDebug($"添加图标到图集，idx={num2}，物品{dataArray1[index].name}({dataArray1[index].ID})");
                    }
                    __instance.spriteIndexMap[iconSprite] = num2;
                }
            }
            __instance.itemIconIndex[dataArray1[index].ID] = num2;
            __instance.signalIconIndex[dataArray1[index].ID] = num2;
        }
        __instance.itemIconIndexBuffer.SetData((Array)__instance.itemIconIndex);
        RecipeProto[] dataArray2 = LDB.recipes.dataArray;
        int length2 = dataArray2.Length;
        for (int index = 0; index < length2; ++index) {
            uint num5 = 0;
            Sprite iconSprite = dataArray2[index].iconSprite;
            if ((Object)iconSprite != (Object)null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num5 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num5 = ++num1;
                    if (num5 >= 625U) {
                        num5 = 0U;
                        Debug.LogWarning((object)"图标图集空间不足！");
                    } else {
                        int num6 = (int)num5 % 25;
                        int num7 = (int)num5 / 25;
                        Graphics.CopyTexture((Texture)dataArray2[index].iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, (Texture)__instance.texture, 0, 0, num6 * 80/*0x50*/, num7 * 80/*0x50*/);
                        LogDebug($"添加图标到图集，idx={num5}，配方{dataArray2[index].name}({dataArray2[index].ID})");
                    }
                    __instance.spriteIndexMap[iconSprite] = num5;
                }
            }
            __instance.recipeIconIndex[dataArray2[index].ID] = num5;
            __instance.signalIconIndex[dataArray2[index].ID + 20000] = num5;
        }
        __instance.recipeIconIndexBuffer.SetData((Array)__instance.recipeIconIndex);
        TechProto[] dataArray3 = LDB.techs.dataArray;
        int length3 = dataArray3.Length;
        int lastTechId = 0;
        //for (int index = 0; index < length3; ++index) {
        for (int index = length3 - 1; index >= 0; --index) {
            int techId = dataArray3[index].ID;
            uint num8 = 0;
            Sprite iconSprite = dataArray3[index].iconSprite;
            if ((Object)iconSprite != (Object)null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num8 = __instance.spriteIndexMap[iconSprite];
                } else {
                    if (techId > 2000 && techId == lastTechId - 1) {
                        LogInfo($"添加图标到图集，跳过科技{dataArray3[index].name}({dataArray3[index].ID})");
                        lastTechId = techId;
                        continue;
                    }
                    lastTechId = techId;
                    num8 = ++num1;
                    if (num8 >= 625U) {
                        num8 = 0U;
                        Debug.LogWarning((object)"图标图集空间不足！");
                    } else {
                        int num9 = (int)num8 % 25;
                        int num10 = (int)num8 / 25;
                        Graphics.CopyTexture((Texture)dataArray3[index].iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, (Texture)__instance.texture, 0, 0, num9 * 80/*0x50*/, num10 * 80/*0x50*/);
                        LogDebug($"添加图标到图集，idx={num8}，科技{dataArray3[index].name}({dataArray3[index].ID})");
                    }
                    __instance.spriteIndexMap[iconSprite] = num8;
                }
            }
            __instance.techIconIndex[dataArray3[index].ID] = num8;
            __instance.signalIconIndex[dataArray3[index].ID + 40000] = num8;
        }
        __instance.techIconIndexBuffer.SetData((Array)__instance.techIconIndex);
        SignalProto[] dataArray4 = LDB.signals.dataArray;
        int length4 = dataArray4.Length;
        for (int index = 0; index < length4; ++index) {
            uint num11 = 0;
            Sprite iconSprite = dataArray4[index].iconSprite;
            if ((Object)iconSprite != (Object)null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num11 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num11 = ++num1;
                    if (num11 >= 625U) {
                        num11 = 0U;
                        Debug.LogWarning((object)"图标图集空间不足！");
                    } else {
                        int num12 = (int)num11 % 25;
                        int num13 = (int)num11 / 25;
                        Graphics.CopyTexture((Texture)dataArray4[index].iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, (Texture)__instance.texture, 0, 0, num12 * 80/*0x50*/, num13 * 80/*0x50*/);
                        LogDebug($"添加图标到图集，idx={num11}，信号{dataArray4[index].name}({dataArray4[index].ID})");
                    }
                    __instance.spriteIndexMap[iconSprite] = num11;
                }
            }
            __instance.signalIconIndex[dataArray4[index].ID] = num11;
        }
        __instance.signalIconIndexBuffer.SetData((Array)__instance.signalIconIndex);
        __instance.texture.Apply(true);
        Array.Clear((Array)__instance.itemDescArr, 0, __instance.itemDescArr.Length);
        TextAsset textAsset = Resources.Load<TextAsset>(IconSet.iconDescPath);
        if ((Object)textAsset != (Object)null) {
            using (MemoryStream input = new MemoryStream(textAsset.bytes)) {
                using (BinaryReader binaryReader = new BinaryReader((Stream)input)) {
                    binaryReader.ReadInt32();
                    int num14 = binaryReader.ReadInt32();
                    for (int index1 = 0; index1 < num14; ++index1) {
                        binaryReader.ReadInt32();
                        int index2 = binaryReader.ReadInt32();
                        uint num15 = index2 >= 12000 || index2 <= 0 ? 0U : __instance.itemIconIndex[index2];
                        for (int index3 = 0; index3 < 40; ++index3) {
                            float num16 = binaryReader.ReadSingle();
                            if (num15 > 0U)
                                __instance.itemDescArr[(long)(num15 * 40U) + (long)index3] = num16;
                        }
                    }
                }
            }
        }
        __instance.itemIconDescBuffer.SetData((Array)__instance.itemDescArr);
        __instance.loaded = true;
        Debug.Log((object)$"Icon set generated. Time cost: {highStopwatch.duration:0.000} s");
        return false;
    }
}
