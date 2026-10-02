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

    /// <summary>注册关键节点的中英文叙事，详细操作由模组面板中的使用指南承担。</summary>
    public static void AddTranslations() {
        RegisterTutorial("神秘信号？", "A Mysterious Signal?",
            "Another routine expedition. Yet after landing, Icarus picked up a powerful signal that was nowhere in the survey records. It grew stronger with every step in one direction… Perhaps it was worth a look.",
            "这次也是照常的开拓任务，但是落地之后，伊卡洛斯感觉到了一些强烈的信号。它们不在任务记录里，也不像黑雾的通讯。\n\n换个方向走了几步，读数竟然更强了……也许应该去看看。");
        RegisterTutorial("沉默的装置", "The Silent Machines",
            "The buried container opened to reveal several unfamiliar towers. There were no operating instructions, only a few mineral samples and faint traces on a damaged record.\n\nThe machines still respond to power. Perhaps sending those samples inside will tell Icarus what they were built to do. The recovered supplies can be found in the new panel [Shift + F].",
            "埋在尘土下的容器终于打开了。里面是几座陌生的塔，旁边散落着几种再普通不过的矿石。没有制造者的署名，也没有完整的操作说明。\n\n接通电源后，塔内重新亮起了微光。将那些矿石送进去，也许就能弄清它们曾经在做什么。\n\n回收的装置和记录已经整理在新接通的面板里，按 [Shift + F] 可以查看。");
        RegisterTutorial("它们还活着", "Still Alive",
            "These towers did not respond to the power grid. Icarus almost mistook them for broken machines, until a matrix passed through the opening and the cracks in their shells began to close.\n\nThey had not been waiting for electricity. They had been waiting for food.",
            "这次找到的塔没有接入电网。伊卡洛斯原以为它们已经坏了，直到一枚矩阵滑进塔内，外壳上的裂纹开始缓缓合拢。\n\n原来它们等待的不是电力。\n\n它们饿了。哪怕生产暂时停下，也该为这些新伙伴留下一条不断供的矩阵传送带。");
        RegisterTutorial("留下来的光", "The Light Left Behind",
            "As the signal faded, a small bright core remained. The recovered record called it Memory. Something of the tower still lived inside it.\n\nIcarus brought it to the analysis tower. The scattered patterns on the old records began to stir.",
            "信号平息后，塔体原来的位置留下了一团微光。记录中把它称作“源点”。那些曾经在塔内流动的东西，似乎还没有完全消失。\n\n伊卡洛斯将它带到解析塔前，残缺的记录再次有了回应。它们留下的经历，也许能帮助仍在运转的同伴。");
        RegisterTutorial("遥远的回应", "An Answer from Afar",
            "The old interface answered a request before any vessel could have arrived. The item had crossed the distance without a visible journey.\n\nThe recovered schematics might allow a logistics station to speak to the same storehouse. Icarus marked a place for it beside the production line.",
            "接口发出应答时，附近并没有运输船经过。请求的物品却已经出现在眼前，仿佛距离从未存在。\n\n恢复出的结构图或许能让物流站接入同一座库房。伊卡洛斯在产线旁留出了一块空地，准备试一试这种古老的运输方式。");
    }

    /// <summary>注册一篇叙事的标题与正文。</summary>
    /// <param name="name">中文标题，同时作为本地化键。</param>
    /// <param name="englishTitle">英文标题。</param>
    /// <param name="englishBody">英文正文。</param>
    /// <param name="chineseBody">中文正文。</param>
    private static void RegisterTutorial(string name, string englishTitle, string englishBody, string chineseBody) {
        Register($"{name}标题", englishTitle, name);
        Register($"{name}前字", englishBody, chineseBody);
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
