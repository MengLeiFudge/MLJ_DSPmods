using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI.Youthcat;

namespace FE.UI.Patches;

public static class IconSetPatch {
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
        __instance.texture = new(2000, 2000, TextureFormat.ARGB32, true);
        __instance.itemIconIndex = new uint[12000];
        __instance.veinIconIndex = new uint[256/*0x0100*/];
        __instance.recipeIconIndex = new uint[12000];
        __instance.techIconIndex = new uint[12000];
        __instance.signalIconIndex = new uint[60000];
        __instance.itemDescArr = new float[25000];
        __instance.itemIconIndexBuffer = new(12000, 4, ComputeBufferType.Default);
        __instance.veinIconIndexBuffer = new(256/*0x0100*/, 4, ComputeBufferType.Default);
        __instance.recipeIconIndexBuffer = new(12000, 4, ComputeBufferType.Default);
        __instance.techIconIndexBuffer = new(12000, 4, ComputeBufferType.Default);
        __instance.signalIconIndexBuffer = new(60000, 4, ComputeBufferType.Default);
        __instance.itemIconDescBuffer = new(25000, 4, ComputeBufferType.Default);
        __instance.spriteIndexMap = new();
        __instance.texture.SetPixels(new Color[4000000]);
        __instance.texture.Apply();
        List<TextIconMapping.IconConfig> iconConfigList = new();
        uint num1 = 0;
        ItemProto[] dataArray1 = LDB.items.dataArray;
        int length1 = dataArray1.Length;
        for (int index = 0; index < length1; ++index) {
            uint num2 = 0;
            ItemProto itemProto = dataArray1[index];
            Sprite iconSprite = itemProto.iconSprite;
            if (iconSprite != null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num2 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num2 = ++num1;
                    if (num2 >= 625U) {
                        num2 = 0U;
                        Debug.LogWarning("图标图集空间不足！");
                    } else {
                        int num3 = (int)num2 % 25;
                        int num4 = (int)num2 / 25;
                        Graphics.CopyTexture(itemProto.iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/, 80/*0x50*/,
                            __instance.texture, 0, 0, num3 * 80/*0x50*/, num4 * 80/*0x50*/);
                    }
                    __instance.spriteIndexMap[iconSprite] = num2;
                }
                if (!string.IsNullOrEmpty(itemProto.IconTag)) {
                    int num5 = (int)num2 % 25;
                    int num6 = (int)num2 / 25;
                    TextIconMapping.IconConfig iconConfig = new TextIconMapping.IconConfig(itemProto.name,
                        itemProto.IconTag, new(num5 / 25f, num6 / 25f),
                        new((num5 + 1) / 25f, (num6 + 1) / 25f), new(24f, 24f), true);
                    iconConfigList.Add(iconConfig);
                }
            }
            __instance.itemIconIndex[dataArray1[index].ID] = num2;
            __instance.signalIconIndex[dataArray1[index].ID] = num2;
        }
        __instance.itemIconIndexBuffer.SetData(__instance.itemIconIndex);
        VeinProto[] dataArray2 = LDB.veins.dataArray;
        int length2 = dataArray2.Length;
        for (int index = 0; index < length2; ++index) {
            uint num7 = 0;
            VeinProto veinProto = dataArray2[index];
            Sprite iconSprite = veinProto.iconSprite;
            if (iconSprite != null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num7 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num7 = ++num1;
                    if (num7 >= 625U) {
                        num7 = 0U;
                        Debug.LogWarning("图标图集空间不足！");
                    } else {
                        int num8 = (int)num7 % 25;
                        int num9 = (int)num7 / 25;
                        Graphics.CopyTexture(veinProto.iconSprite80px.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, __instance.texture, 0, 0, num8 * 80/*0x50*/, num9 * 80/*0x50*/);
                    }
                    __instance.spriteIndexMap[iconSprite] = num7;
                }
            }
            __instance.veinIconIndex[dataArray2[index].ID] = num7;
            __instance.signalIconIndex[dataArray2[index].ID + 12000] = num7;
        }
        __instance.veinIconIndexBuffer.SetData(__instance.veinIconIndex);
        RecipeProto[] dataArray3 = LDB.recipes.dataArray;
        int length3 = dataArray3.Length;
        for (int index = 0; index < length3; ++index) {
            uint num12 = 0;
            RecipeProto recipeProto = dataArray3[index];
            Sprite iconSprite = recipeProto.iconSprite;
            if (iconSprite != null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num12 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num12 = ++num1;
                    if (num12 >= 625U) {
                        num12 = 0U;
                        Debug.LogWarning("图标图集空间不足！");
                    } else {
                        int num13 = (int)num12 % 25;
                        int num14 = (int)num12 / 25;
                        Graphics.CopyTexture(recipeProto.iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, __instance.texture, 0, 0, num13 * 80/*0x50*/, num14 * 80/*0x50*/);
                    }
                    __instance.spriteIndexMap[iconSprite] = num12;
                }
                if (!string.IsNullOrEmpty(recipeProto.IconTag)) {
                    int num15 = (int)num12 % 25;
                    int num16 = (int)num12 / 25;
                    TextIconMapping.IconConfig iconConfig = new TextIconMapping.IconConfig(recipeProto.name,
                        recipeProto.IconTag, new(num15 / 25f, num16 / 25f),
                        new((num15 + 1) / 25f, (num16 + 1) / 25f), new(24f, 24f), true);
                    iconConfigList.Add(iconConfig);
                }
            }
            __instance.recipeIconIndex[dataArray3[index].ID] = num12;
            __instance.signalIconIndex[dataArray3[index].ID + 20000] = num12;
        }
        __instance.recipeIconIndexBuffer.SetData(__instance.recipeIconIndex);
        TechProto[] dataArray4 = LDB.techs.dataArray;
        int length4 = dataArray4.Length;
        int lastTechId = 0;
        List<string> iconPathSet = [];
        for (int index = length4 - 1; index >= 0; --index) {
            uint num17 = 0;
            TechProto techProto = dataArray4[index];
            Sprite iconSprite = techProto.iconSprite;
            if (iconSprite != null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num17 = __instance.spriteIndexMap[iconSprite];
                } else {
                    if (techProto.ID > 2000 && techProto.ID == lastTechId - 1) {
                        lastTechId = techProto.ID;
                        continue;
                    }
                    lastTechId = techProto.ID;
                    if (iconPathSet.Contains(dataArray4[index].IconPath)) {
                        continue;
                    }
                    num17 = ++num1;
                    if (num17 >= 625U) {
                        num17 = 0U;
                        Debug.LogWarning("图标图集空间不足！");
                    } else {
                        int num18 = (int)num17 % 25;
                        int num19 = (int)num17 / 25;
                        Graphics.CopyTexture(techProto.iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/, 80/*0x50*/,
                            __instance.texture, 0, 0, num18 * 80/*0x50*/, num19 * 80/*0x50*/);
                    }
                    __instance.spriteIndexMap[iconSprite] = num17;
                }
                if (!string.IsNullOrEmpty(techProto.IconTag)) {
                    int num20 = (int)num17 % 25;
                    int num21 = (int)num17 / 25;
                    TextIconMapping.IconConfig iconConfig = new TextIconMapping.IconConfig(techProto.name,
                        techProto.IconTag, new(num20 / 25f, num21 / 25f),
                        new((num20 + 1) / 25f, (num21 + 1) / 25f), new(24f, 24f), true);
                    iconConfigList.Add(iconConfig);
                }
            }
            __instance.techIconIndex[dataArray4[index].ID] = num17;
            __instance.signalIconIndex[dataArray4[index].ID + 40000] = num17;
        }
        __instance.techIconIndexBuffer.SetData(__instance.techIconIndex);
        SignalProto[] dataArray5 = LDB.signals.dataArray;
        int length5 = dataArray5.Length;
        for (int index = 0; index < length5; ++index) {
            uint num22 = 0;
            SignalProto signalProto = dataArray5[index];
            Sprite iconSprite = signalProto.iconSprite;
            if (iconSprite != null) {
                if (__instance.spriteIndexMap.ContainsKey(iconSprite)) {
                    num22 = __instance.spriteIndexMap[iconSprite];
                } else {
                    num22 = ++num1;
                    if (num22 >= 625U) {
                        num22 = 0U;
                        Debug.LogWarning("图标图集空间不足！");
                    } else {
                        int num23 = (int)num22 % 25;
                        int num24 = (int)num22 / 25;
                        Graphics.CopyTexture(signalProto.iconSprite.texture, 0, 0, 0, 0, 80/*0x50*/,
                            80/*0x50*/, __instance.texture, 0, 0, num23 * 80/*0x50*/, num24 * 80/*0x50*/);
                    }
                    __instance.spriteIndexMap[iconSprite] = num22;
                }
                if (!string.IsNullOrEmpty(signalProto.IconTag)) {
                    int num25 = (int)num22 % 25;
                    int num26 = (int)num22 / 25;
                    TextIconMapping.IconConfig iconConfig = new TextIconMapping.IconConfig(signalProto.name,
                        signalProto.IconTag, new(num25 / 25f, num26 / 25f),
                        new((num25 + 1) / 25f, (num26 + 1) / 25f), new(24f, 24f), true);
                    iconConfigList.Add(iconConfig);
                }
            }
            __instance.signalIconIndex[dataArray5[index].ID] = num22;
        }
        __instance.signalIconIndexBuffer.SetData(__instance.signalIconIndex);
        __instance.texture.Apply(true);
        Array.Clear(__instance.itemDescArr, 0, __instance.itemDescArr.Length);
        TextAsset textAsset = Resources.Load<TextAsset>(IconSet.iconDescPath);
        if (textAsset != null) {
            using (MemoryStream input = new MemoryStream(textAsset.bytes)) {
                using (BinaryReader binaryReader = new BinaryReader(input)) {
                    binaryReader.ReadInt32();
                    int num27 = binaryReader.ReadInt32();
                    for (int index1 = 0; index1 < num27; ++index1) {
                        binaryReader.ReadInt32();
                        int index2 = binaryReader.ReadInt32();
                        uint num28 = index2 >= 12000 || index2 <= 0 ? 0U : __instance.itemIconIndex[index2];
                        for (int index3 = 0; index3 < 40; ++index3) {
                            float num29 = binaryReader.ReadSingle();
                            if (num28 > 0U)
                                __instance.itemDescArr[num28 * 40U + index3] = num29;
                        }
                    }
                }
            }
        }
        __instance.itemIconDescBuffer.SetData(__instance.itemDescArr);
        __instance.loaded = true;
        Debug.Log($"Icon set generated. Time cost: {highStopwatch.duration:0.000} s");
        Debug.Log($"共计添加{num1}个图标到图集。只有图标数目<=625时，才能看到除屏蔽之外的所有图标。");
        TextIconMapping generalIconMapping = Configs.builtin.generalIconMapping;
        generalIconMapping.iconMappings = iconConfigList.ToArray();
        generalIconMapping.Refresh();
        Shader.SetGlobalTexture("_Global_IconSet_Tex", __instance.texture);
        return false;
    }
}
