using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FE.Compatibility.Mods;
using HarmonyLib;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>在关键发现时提供 G 手册叙事，并接入原版及兼容模组的教程布局。</summary>
public static class TutorialManager {
    private const int FirstTutorialId = 201;
    private const string FeTutorialLayoutPrefix = "tutorial-fe-";
    private const string PreText =
        "{$Text|fontsize=16;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,30}\n";
    private const string PostText =
        "{$Text|fontsize=14;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,20}\n";
    private static readonly string[] TutorialNames = ["神秘信号？", "沉默的装置", "它们还活着", "留下来的光", "遥远的回应"];
    private static MethodInfo genesisBookIsLayoutMethod;
    private static MethodInfo genesisBookGetLayoutMethod;
    private static bool genesisBookLayoutMethodsInitialized;

    /// <summary>注册由实际发现触发的叙事条目，解锁状态仍由原版手册保存。</summary>
    public static void AddTutorials() {
        for (int index = 0; index < TutorialNames.Length; index++) {
            int id = FirstTutorialId + index;
            string key = $"{TutorialNames[index]}标题";
            TutorialProto proto = new() {
                ID = id,
                SID = string.Empty,
                Name = key,
                name = key,
                LayoutFileName = $"{FeTutorialLayoutPrefix}{id}",
                DeterminatorName = string.Empty,
                DeterminatorParams = [],
            };
            LDBTool.PreAddProto(proto);
            proto.Preload();
        }
    }

    /// <summary>根据已经发生的发现触发原版手册提示，不提前介绍后续机制或奖励。</summary>
    public static void UpdateMilestoneTips() {
        TutorialLogic logic = GameMain.gameScenario?.tutorialLogic;
        if (logic == null || !GameMain.data.guideComplete || GameMain.gameTick < 600) { return; }
        bool[] reached = [
            GameMain.mainPlayer?.controller?.actionWalk?.isGrounded == true,
            ExplorationManager.FirstRelicClaimed,
            ExplorationManager.StoryCompleted >= 4,
            ResearchManager.SacrificeUnlocked,
            ResearchManager.IsContentUnlocked(GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId)
                || ResearchManager.IsContentUnlocked(GrowthTechnologyManager.InterstellarInteractionTechnologyContentId),
        ];
        for (int index = 0; index < reached.Length; index++) {
            int id = FirstTutorialId + index;
            if (reached[index] && logic.TutorialNotUnlocked(id) && logic.NoPopOrder(id)) {
                logic.AddPopOrder(1, id);
            }
        }
    }

    /// <summary>让教程列表支持垂直滚动。</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow._OnOpen))]
    private static void UITutorialWindow_OnOpen_Postfix(UITutorialWindow __instance) {
        if (__instance.entryList.VertScroll) { return; }
        __instance.entryList.VertScroll = true;
        __instance.entryList.m_ScrollRect.vertical = true;
        __instance.entryList.m_ScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        __instance.entryList.m_ScrollRect.enabled = false;
        __instance.entryList.m_ScrollRect.enabled = true;
    }

    /// <summary>将 FE 与兼容模组的动态教程正文接入原版教程窗口。</summary>
    /// <param name="instructions">原方法指令。</param>
    /// <param name="ilGenerator">标签生成器。</param>
    /// <returns>接入动态布局后的指令。</returns>
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow.OnTutorialChange))]
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore(GenesisBook.GUID, OrbitalRing.GUID)]
    public static IEnumerable<CodeInstruction> UITutorialWindow_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) {
        var instructionList = instructions as List<CodeInstruction> ?? [.. instructions];
        MethodInfo dispatchMethod = AccessTools.Method(typeof(TutorialManager), nameof(UseCustomLayoutParser));
        if (instructionList.Any(instruction =>
                instruction.opcode == OpCodes.Call && Equals(instruction.operand, dispatchMethod))) {
            return instructionList;
        }

        var matcher = new CodeMatcher(instructionList, ilGenerator);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Stloc_0));
        if (matcher.IsInvalid) {
            LogError("TutorialManager transpiler failed: layout result anchor was not found.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out Label endLabel);
        matcher.MatchBack(false, new CodeMatch(OpCodes.Ldsfld,
            AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.textFolder))));
        if (matcher.IsInvalid) {
            LogError("TutorialManager transpiler failed: vanilla layout anchor was not found.");
            return instructionList;
        }
        matcher.CreateLabelAt(matcher.Pos, out Label originalLogicLabel);
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call, dispatchMethod),
            new CodeInstruction(OpCodes.Brfalse_S, originalLogicLabel));
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TutorialManager), nameof(GetLayoutStr))),
            new CodeInstruction(OpCodes.Br_S, endLabel));
        return matcher.InstructionEnumeration();
    }

    /// <summary>判断教程是否使用 FE 动态布局。</summary>
    /// <param name="proto">教程原型。</param>
    /// <returns>是否由 FE 提供布局。</returns>
    public static bool IsFELayout(TutorialProto proto) {
        string layoutFileName = proto?.LayoutFileName;
        return !string.IsNullOrEmpty(layoutFileName) && layoutFileName.StartsWith(FeTutorialLayoutPrefix);
    }

    /// <summary>判断当前教程是否由 FE 或已启用的兼容模组生成正文。</summary>
    /// <param name="proto">教程原型。</param>
    /// <returns>是否应分派到动态解析器。</returns>
    public static bool UseCustomLayoutParser(TutorialProto proto) =>
        IsFELayout(proto) || IsGenesisBookLayout(proto) || OrbitalRing.IsTutorialLayout(proto);

    /// <summary>获取 FE 或兼容模组教程的动态布局正文。</summary>
    /// <param name="proto">教程原型。</param>
    /// <returns>原版解析器可读取的布局文本。</returns>
    public static string GetLayoutStr(TutorialProto proto) {
        if (IsGenesisBookLayout(proto)) { return GetGenesisBookLayoutStr(proto); }
        if (OrbitalRing.IsTutorialLayout(proto)) { return OrbitalRing.GetTutorialLayoutStr(proto); }
        string protoName = proto?.Name;
        if (string.IsNullOrEmpty(protoName) || !protoName.EndsWith("标题")) { return string.Empty; }
        string bodyKey = protoName.Replace("标题", "前字");
        return $"{PreText}{protoName.Translate()}{PostText}{bodyKey.Translate()}";
    }

    private static bool IsGenesisBookLayout(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) { return false; }
        return genesisBookIsLayoutMethod.Invoke(null, [proto]) is bool result && result;
    }

    private static string GetGenesisBookLayoutStr(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) { return string.Empty; }
        return genesisBookGetLayoutMethod.Invoke(null, [proto]) as string ?? string.Empty;
    }

    private static bool TryInitGenesisBookLayoutMethods() {
        if (genesisBookLayoutMethodsInitialized) {
            return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
        }
        genesisBookLayoutMethodsInitialized = true;
        Type tutorialPatchType = AccessTools.TypeByName("ProjectGenesis.Patches.UITutorialWindowPatches");
        if (tutorialPatchType == null) { return false; }
        genesisBookIsLayoutMethod = AccessTools.Method(tutorialPatchType, "IsGenesisBookLayout", [typeof(TutorialProto)]);
        genesisBookGetLayoutMethod = AccessTools.Method(tutorialPatchType, "GetLayoutStr", [typeof(TutorialProto)]);
        return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
    }
}
