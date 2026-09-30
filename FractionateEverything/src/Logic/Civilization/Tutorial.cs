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

/// <summary>
/// 注册五篇当前教程、正文和教程窗口布局兼容补丁。
/// </summary>
public static class TutorialManager {
    private readonly struct TutorialRegistration(
        int id,
        string baseName,
        string determinatorName,
        long[] determinatorParams) {
        public readonly int Id = id;
        public readonly string BaseName = baseName;
        public readonly string DeterminatorName = determinatorName;
        public readonly long[] DeterminatorParams = determinatorParams;
    }

    private const int FirstTutorialId = 201;
    private const string FeTutorialLayoutPrefix = "tutorial-fe-";
    private const string PreText =
        "{$Text|fontsize=16;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,30}\n";
    private const string PostText =
        "{$Text|fontsize=14;linespacing=1.1;textalignment=0,1;color=#FFFFFF52;material=UI/Materials/widget-text-alpha-5x-thick;margins=20,20,20,20}\n";

    private static readonly TutorialRegistration[] tutorialRegistrations = BuildTutorialRegistrations();
    private static MethodInfo genesisBookIsLayoutMethod;
    private static MethodInfo genesisBookGetLayoutMethod;
    private static bool genesisBookLayoutMethodsInitialized;

    /// <summary>
    /// 注册全部 FE 教程原型。
    /// </summary>
    public static void AddTutorials() {
        foreach (TutorialRegistration registration in tutorialRegistrations) {
            TutorialProto proto = new() {
                ID = registration.Id,
                SID = string.Empty,
                Name = $"{registration.BaseName}标题",
                name = $"{registration.BaseName}标题",
                LayoutFileName = $"{FeTutorialLayoutPrefix}{registration.Id}",
                DeterminatorName = registration.DeterminatorName,
                DeterminatorParams = registration.DeterminatorParams,
            };
            LDBTool.PreAddProto(proto);
            proto.Preload();
        }
    }

    /// <summary>
    /// 注册五篇教程的中英文正文。
    /// </summary>
    public static void AddTranslations() {
        RegisterTutorial("万物分馏简介", "Fractionate Everything", "万物分馏简介",
            """
            Producing the first Electromagnetic Matrix triggers a strong pulse, unlocks the Relic Detector, and creates Key Signal 1 on the birth planet. Follow the directionless percentage on the left HUD; FE does not start navigation or control the mecha. Reach the signal to establish the fractionation subspace and receive ten Basic Mineral Towers, ten Basic Analysis Towers, and twenty Universal Tower Embryos.

            After warp flight is available, Key Signal 2 names another planet in the birth system. Recover it to restore the Planet Detector. Ordinary broadcasts then show separate planet and relic percentages without revealing their star system, planet, direction, distance, contents, or coordinates. The HUD keeps one mandatory key signal and four ordinary pinned slots; manage ordinary pins in Relic Exploration.

            Press [Shift + F] after the first recovery to open the subspace panel. Advanced living towers are later discovered through item-direction research and cultivated from Universal Tower Embryos. Continue exploring resource caches or fighting the Dark Fog to recover more embryos; the starter supply is not the only source. Press [G] at any time to revisit these guides.
            """,
            """
            首次生产电磁矩阵会触发一次强烈脉冲，解锁遗物探测器，并在出生星生成“关键信号1”。根据左侧 HUD 中无方向的百分比寻找信号；FE 不会启动导航或控制机甲。抵达后即可建立分馏亚空间，并获得 10 座基础矿物塔、10 座基础解析塔和 20 个通用原胚。

            取得曲速飞行能力后，“关键信号2”会公开出生星系内另一颗目标星球。回收后恢复星球探测器，普通广播才开始分别显示星球与遗物百分比，且不会公开星系、星球、方向、距离、内容或坐标。HUD 保留 1 个不可取消的主线槽和 4 个普通固定槽；普通信号可在“遗物探索”页固定或取消。

            首次回收后按 [Shift + F] 打开亚空间面板。后续通过实地探索具体内容、再用对应物品完成解析来发现高级能力，并由通用原胚培育三类高级生命塔。持续探索资源藏点或击杀黑雾可以取得更多原胚，首批补给不是唯一来源。随时可以按 [G] 重新查看这些指引。
            """);

        RegisterTutorial("亚空间与研究", "Subspace and Research", "亚空间与研究",
            """
            The first relic directly establishes a shared subspace inventory. Each item starts with an independent capacity of ten stacks; Subspace Capacity can add ten stacks per level through level 10,000. Relic rewards may temporarily exceed the normal limit.

            Exploration signals reveal one concrete recipe, tower trait, or technology direction at a time. The signal also identifies which item carries it. Every item has one shared analysis-progress slot; when that slot fills, one of that item's explored but unresolved contents is selected. Resolving a technology direction reveals its entire upgrade row, which then uses matrices for normal research.

            Feed items into the front port of either Basic Tower to upload them. Logistics Interaction Stations can exchange items automatically at an energy cost. Advanced Towers are stored normally; when one type first reaches 1,000 stored towers, its trait becomes eligible to appear in exploration but is not granted directly.

            An Advanced Tower with no explored result remaining is sacrificed instead, with a 2% chance to produce Memory. The average sacrifice rate over the latest 60 seconds raises the same tower type's success rate in 5% steps, up to +75%. Memory carries technology directions; after the Planet Detector is restored, analysing Frag may reveal an additional permanent broadcast.
            """,
            """
            首个遗物会直接建立共享亚空间库存。每种物品初始拥有独立的 10 组容量；“亚空间扩容”每级增加 10 组，最高可研究至 10000 级。遗物奖励允许暂时突破常规上限。

            探索信号每次发现一项具体配方、塔特质或科技方向，并同时确定承载它的解析物品。每种物品只有一个共享解析进度槽；槽满时，从该物品已经探索但尚未解析的内容中选出一项。科技方向解析完成后，整条升级行同时显现，再使用矩阵进行正常研究。

            将物品送入任一基础塔的正面端口即可上传。物流交互站可以消耗电力自动存取物品。高级塔也会正常入库；某类高级塔首次达到 1000 座库存时，只会让对应特质进入探索候选，不会直接授予解析资格。

            某类高级塔没有已探索的剩余研究内容时，继续输入会转为献祭，并以 2% 概率产出 Memory。最近 60 秒内同类高级塔的平均献祭速率会以 5% 为一档提高该类塔的成功率，最多提高 75%。Memory 承载科技方向；星球探测器恢复后，解析 Frag 可能发现一个额外的永久广播。
            """);

        RegisterTutorial("基础分馏塔使用指南", "Basic Fractionator Guide", "基础分馏塔使用指南",
            """
            [Basic Mineral Tower]
            Feed a supported ordinary resource through a side input. A successful 5% cycle consumes one item and outputs two of the same item; failed items continue through the flow output. The front port uploads items when the tower is not processing a side input.

            [Basic Analysis Tower]
            Feed an item with explored unresolved content through a side input. Each item has one shared progress slot and a 2% progress roll per unit; ordinary discoveries add 5% per success, while Advanced Tower traits add 1%. When the slot fills, one explored unresolved result is selected. Storing 1,000 towers of one type only allows its trait to appear in exploration; that trait must still be explored and analysed. After no explored result remains, further Advanced Tower inputs become sacrifices. Frag rolls for broadcasts only after the Planet Detector is restored; before that it passes through unchanged. Other items also pass through unchanged.

            Both Basic Towers use the power grid, process global cargo stacks, continue while one output is blocked, and expose flow and product buffers of 20 current cargo stacks.
            """,
            """
            【基础矿物塔】
            从侧面输入受支持的普通资源。每轮有 5% 成功率；成功时消耗 1 件并产出 2 件同种物品，失败物品从流动输出继续离开。未从侧面处理物品时，正面端口用于上传物品。

            【基础解析塔】
            从侧面输入已经探索到未解析内容的物品。每种物品共用一个进度槽，每件物品进行一次 2% 的进度判定；普通内容每次成功增加 5%，高级塔特质每次成功增加 1%。槽满时，从已经探索但尚未解析的结果中选出一项。某类高级塔达到 1000 座库存只会让特质进入探索候选，该特质仍需完成探索和解析；没有已探索的剩余内容后，继续输入高级塔才会转为献祭。星球探测器恢复后，Frag 才会判定普通广播；在此之前会原样流出。其他物品也会原样流出。

            两座基础塔接入电网，按全局堆叠处理货物，在一路输出阻塞时仍可继续工作，流动与产物缓存均为当前货物堆叠的 20 堆。
            """);

        RegisterTutorial("高级生命塔使用指南", "Advanced Living Tower Guide", "高级生命塔使用指南",
            """
            Advanced Towers do not connect to the power grid. Select one discovered recipe in the tower window, then feed all required inputs through any input port. Matrices are consumed as nutrition: one equivalent processing round consumes 0.02 nutrition, idling consumes 0.002 per second, and the six matrix tiers provide 1.0x to 1.5x speed in 0.1x steps.

            Nutrition above half restores durability; zero nutrition causes continuous decay. Construction drones cannot repair living towers. Dismantling returns cached items, but destruction loses them. Every input, product, and flow buffer holds at most 20 items.

            Advanced Mineral Towers use rare minerals as catalysts to multiply rare resources. Advanced Interaction Towers cultivate Advanced Towers and breed Universal Tower Embryos. Advanced Conversion Towers replace selected fuels or machines and recover half of the replaced components before trait bonuses.
            """,
            """
            高级塔不接入电网。在塔窗口中选择一张已发现配方，再从任意输入端送入全部所需物品。矩阵会转化为营养：每个等效加工轮消耗 0.02 营养，待机每秒消耗 0.002 营养；六档矩阵依次提供 1.0 至 1.5 倍速度，每档提高 0.1 倍。

            营养高于一半时恢复耐久，营养归零后持续衰减。建造无人机无法维修生命塔。主动拆除会返还缓存物品，损毁则不会返还。每种输入、产物和流动输出缓存最多容纳 20 件。

            高级矿物塔以珍奇矿物为催化剂增殖珍奇资源；高级交互塔培育高级塔并繁育通用原胚；高级转化塔置换指定燃料或机器，并在特质加成前回收被替换部件的 50%。
            """);

        RegisterTutorial("物流交互站使用指南", "Logistics Interaction Station Guide", "物流交互站使用指南",
            """
            Logistics Interaction Stations exchange ordinary station inventory with the fractionation subspace. Supply and demand lock modes determine upload or download direction; storage modes maintain their configured target quantities.

            Every transfer consumes station energy according to item value. Subspace Transfer Efficiency reduces this cost by 10% per level, to a maximum reduction of 60%. Normal uploads stop at each item's current subspace capacity; relic rewards remain extractable even when they exceed that limit.
            """,
            """
            物流交互站会在普通物流槽位与分馏亚空间之间交换物品。供应、需求的锁定状态决定上传或下载方向，仓储模式则维持相应的目标数量。

            每次传输都会按物品价值消耗物流站电力。“亚空间存取能效”每级降低 10% 消耗，最多降低 60%。普通上传会在对应物品达到当前亚空间容量时停止；超过上限的遗物奖励仍然可以正常提取。
            """);
    }

    private static TutorialRegistration[] BuildTutorialRegistrations() {
        int nextId = FirstTutorialId;
        return [
            new(nextId++, "万物分馏简介", "TOR_GameSecond", [10]),
            new(nextId++, "亚空间与研究", "TOR_OnBuild", [IFE基础解析塔]),
            new(nextId++, "基础分馏塔使用指南", "TOR_OnBuild", [IFE基础矿物塔, IFE基础解析塔]),
            new(nextId++, "高级生命塔使用指南", "TOR_OnBuild",
                [IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔]),
            new(nextId++, "物流交互站使用指南", "TOR_OnBuild",
                [IFE行星内物流交互站, IFE星际物流交互站]),
        ];
    }

    private static void RegisterTutorial(string baseName, string englishTitle, string chineseTitle,
        string englishBody, string chineseBody) {
        Register($"{baseName}标题", englishTitle, chineseTitle);
        Register($"{baseName}前字", englishBody, chineseBody);
    }

    /// <summary>
    /// 让教程列表支持垂直滚动。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITutorialWindow), nameof(UITutorialWindow._OnOpen))]
    private static void UITutorialWindow_OnOpen_Postfix(UITutorialWindow __instance) {
        if (__instance.entryList.VertScroll) {
            return;
        }
        __instance.entryList.VertScroll = true;
        __instance.entryList.m_ScrollRect.vertical = true;
        __instance.entryList.m_ScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        __instance.entryList.m_ScrollRect.enabled = false;
        __instance.entryList.m_ScrollRect.enabled = true;
    }

    /// <summary>
    /// 将 FE 与兼容模组的动态教程正文接入原版教程窗口。
    /// </summary>
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
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call, dispatchMethod),
            new CodeInstruction(OpCodes.Brfalse_S, originalLogicLabel));
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(UITutorialWindow), nameof(UITutorialWindow.tutorialProto))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(TutorialManager), nameof(GetLayoutStr))),
            new CodeInstruction(OpCodes.Br_S, endLabel));
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 判断教程是否使用 FE 动态布局。
    /// </summary>
    public static bool IsFELayout(TutorialProto proto) {
        string layoutFileName = proto?.LayoutFileName;
        return !string.IsNullOrEmpty(layoutFileName) && layoutFileName.StartsWith(FeTutorialLayoutPrefix);
    }

    /// <summary>
    /// 判断当前教程是否由 FE 或已启用的兼容模组生成正文。
    /// </summary>
    public static bool UseCustomLayoutParser(TutorialProto proto) =>
        IsFELayout(proto) || IsGenesisBookLayout(proto) || OrbitalRing.IsTutorialLayout(proto);

    /// <summary>
    /// 获取 FE 或兼容模组教程的动态布局正文。
    /// </summary>
    public static string GetLayoutStr(TutorialProto proto) {
        if (IsGenesisBookLayout(proto)) {
            return GetGenesisBookLayoutStr(proto);
        }
        if (OrbitalRing.IsTutorialLayout(proto)) {
            return OrbitalRing.GetTutorialLayoutStr(proto);
        }

        string protoName = proto?.Name;
        if (string.IsNullOrEmpty(protoName) || !protoName.EndsWith("标题")) {
            return string.Empty;
        }
        string bodyKey = protoName.Replace("标题", "前字");
        return $"{PreText}{protoName.Translate()}{PostText}{bodyKey.Translate()}";
    }

    private static bool IsGenesisBookLayout(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return false;
        }
        return genesisBookIsLayoutMethod.Invoke(null, [proto]) is bool result && result;
    }

    private static string GetGenesisBookLayoutStr(TutorialProto proto) {
        if (!GenesisBook.Enable || !TryInitGenesisBookLayoutMethods()) {
            return string.Empty;
        }
        return genesisBookGetLayoutMethod.Invoke(null, [proto]) as string ?? string.Empty;
    }

    private static bool TryInitGenesisBookLayoutMethods() {
        if (genesisBookLayoutMethodsInitialized) {
            return genesisBookIsLayoutMethod != null && genesisBookGetLayoutMethod != null;
        }

        genesisBookLayoutMethodsInitialized = true;
        Type tutorialPatchType = AccessTools.TypeByName("ProjectGenesis.Patches.UITutorialWindowPatches");
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
