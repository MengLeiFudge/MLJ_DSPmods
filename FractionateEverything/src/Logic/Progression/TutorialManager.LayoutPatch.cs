using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

public static partial class TutorialManager {
    static MethodInfo genesisBookIsLayoutMethod;
    static MethodInfo genesisBookGetLayoutMethod;
    static bool genesisBookLayoutMethodsInitialized;

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

        TryMarkCurrentTutorialViewedToBottom(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow.OnTutorialChange))]
    private static void UITutorialWindow_OnTutorialChange_Postfix(UITutorialWindow __instance) {
        TryMarkCurrentTutorialViewedToBottom(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow._OnUpdate))]
    private static void UITutorialWindow_OnUpdate_Postfix(UITutorialWindow __instance) {
        TryMarkCurrentTutorialViewedToBottom(__instance);
    }

    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow.OnTutorialChange))]
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore(GenesisBook.GUID)]
    public static IEnumerable<CodeInstruction> UITutorialWindow_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) {
        var instructionList = instructions as List<CodeInstruction> ?? [.. instructions];
        var useCustomLayoutParserMethod = AccessTools.Method(typeof(TutorialManager), nameof(UseCustomLayoutParser));
        if (instructionList.Any(i => i.opcode == OpCodes.Call && Equals(i.operand, useCustomLayoutParserMethod))) {
            return instructionList;
        }

        var matcher = new CodeMatcher(instructionList, ilGenerator);

        /*
            string layoutStr = UILayoutParserManager.GetLayoutStr(UITutorialWindow.textFolder, this.tutorialProto.LayoutFileName);

            Ldarg_0
            Ldfld tutorialProto
            Call IsFELayout
            Brfalse_S originalLogicLabel

            Ldarg_0
            Ldfld tutorialProto
            Call GetLayoutStr
            Br_S endLabel

            IL_0027: ldsfld       string UITutorialWindow::textFolder // originalLogicLabel
            IL_002c: ldarg.0      // this
            IL_002d: ldfld        class TutorialProto UITutorialWindow::tutorialProto
            IL_0032: ldfld        string TutorialProto::LayoutFileName
            IL_0037: call         string UILayoutParserManager::GetLayoutStr(string, string)
            IL_003c: stloc.0      // layoutStr // endLabel

         */

        matcher.MatchForward(false, new CodeMatch(OpCodes.Stloc_0));
        if (matcher.IsInvalid) {
            LogError("TutorialManager.UITutorialWindow_Transpiler failed: cannot find stloc.0 anchor.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out var endLabel);

        matcher.MatchBack(false,
            new CodeMatch(OpCodes.Ldsfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.textFolder))));
        if (matcher.IsInvalid) {
            LogError("TutorialManager.UITutorialWindow_Transpiler failed: cannot find textFolder anchor.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out var originalLogicLabel);

        // 插入预加载和判断
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(TutorialManager), nameof(UseCustomLayoutParser))),
            new CodeInstruction(OpCodes.Brfalse_S, originalLogicLabel)
        );

        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(TutorialManager), nameof(GetLayoutStr))),
            new CodeInstruction(OpCodes.Br_S, endLabel)
        );

        return matcher.InstructionEnumeration();
    }

    public static bool IsFELayout(TutorialProto proto) {
        var layoutFileName = proto.LayoutFileName;
        return !string.IsNullOrEmpty(layoutFileName) && layoutFileName.StartsWith(FeTutorialLayoutPrefix);
    }

    public static bool UseCustomLayoutParser(TutorialProto proto) {
        return IsFELayout(proto) || IsGenesisBookLayout(proto);
    }

    const string preText =
        "{$Text|fontsize=16;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,30}\n";
    const string postText =
        "{$Text|fontsize=14;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,20}\n";

    public static string GetLayoutStr(TutorialProto proto) {
        if (IsGenesisBookLayout(proto)) {
            return GetGenesisBookLayoutStr(proto);
        }

        string protoName = proto.Name;
        if (!protoName.EndsWith("标题")) {
            return string.Empty;
        }
        var text = protoName.Replace("标题", "前字");
        return $"{preText}{protoName.Translate()}{postText}{text.Translate()}";
    }

    static bool IsGenesisBookLayout(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return false;
        }

        return genesisBookIsLayoutMethod.Invoke(null, [proto]) is bool isGenesisBookLayout && isGenesisBookLayout;
    }

    static string GetGenesisBookLayoutStr(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return string.Empty;
        }

        return genesisBookGetLayoutMethod.Invoke(null, [proto]) as string ?? string.Empty;
    }

    static bool TryInitGenesisBookLayoutMethods() {
        if (genesisBookLayoutMethodsInitialized) {
            return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
        }

        genesisBookLayoutMethodsInitialized = true;
        var tutorialPatchType = AccessTools.TypeByName("ProjectGenesis.Patches.UITutorialWindowPatches");
        if (tutorialPatchType == null) {
            return false;
        }

        genesisBookIsLayoutMethod =
            AccessTools.Method(tutorialPatchType, "IsGenesisBookLayout", [typeof(TutorialProto)]);
        genesisBookGetLayoutMethod =
            AccessTools.Method(tutorialPatchType, "GetGenesisBookLayoutStr", [typeof(TutorialProto)]);
        return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
    }
}
