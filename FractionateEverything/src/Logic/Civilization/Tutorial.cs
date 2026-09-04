using static FE.Utils.Utils;
using xiaoye97;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FE.Compatibility.Mods;
using HarmonyLib;
using UnityEngine.UI;

namespace FE.Logic.Civilization;

#region 教程管理器

/// <summary>
/// 注册 FE 教程及其解锁条件；教程阅读不再承载旧成就状态。
/// </summary>
public static partial class TutorialManager {
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
    private static readonly TutorialRegistration[] tutorialRegistrations = BuildTutorialRegistrations();

    public static void AddTutorials() {
        foreach (TutorialRegistration registration in tutorialRegistrations) {
            AddTutorial(registration);
        }
    }

    private static TutorialRegistration[] BuildTutorialRegistrations() {
        int nextId = FirstTutorialId;
        return [
            new(nextId++, "万物分馏简介", "TOR_GameSecond", [10]),
            new(nextId++, "分馏数据中心", "TOR_TechUnlocked", [TFE分馏数据中心, 4]),
            new(nextId++, "分馏塔使用指南", "TOR_OnBuild",
                [IFE培养塔, IFE解析塔, IFE复制塔, IFE转化塔]),
            new(nextId++, "物流交互站使用指南", "TOR_OnBuild",
                [IFE行星内物流交互站, IFE星际物流交互站]),
        ];
    }

    private static void AddTutorial(TutorialRegistration registration) {
        TutorialProto proto = new() {
            ID = registration.Id,
            SID = "",
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

#endregion // 教程管理器

#region 教程文本

/// <summary>
/// 教程系统翻译文本注册入口。
/// </summary>
public static partial class TutorialManager {
    public static void AddTranslations() {
        Register("万物分馏简介标题", "Fractionate Everything", "万物分馏简介");
        Register("万物分馏简介前字",
            $"""
             An abnormal old-civilization signal has been detected near the landing zone. The first contact is unstable, but the recovered starter kit contains a data-centre communication channel, initial tower protos, and one Cultivation Tower.

             Here is a short early guide:
             1. Follow the abnormal signal and recover the starter kit. After the data-centre communication channel is established, press {"[Shift + F] key".WithColor(Orange)} to connect to the Fractionation Data Centre.
             2. Open the Recovery Guide page in the panel and follow it step by step to learn how to use the Cultivation Tower to cultivate protos into new Fractionation Towers, as well as how to upload items.
             There will also be a new guide that explains how to use the Cultivation Tower after you build it.
             3. Cultivate common protos until you obtain a new Cultivation Tower, then upload it to the Fractionation Data Centre to restore the Cultivation Tower protocol.
             This is an extremely powerful support feature that defies space limitations and can be thought of as an external backpack with unlimited capacity.
             4. Build a matrix production line and process matrices or dark-fog samples in the Analysis Tower to produce Frag. Upload Frag denomination items to credit the data centre, then spend Frag on protocol retrieval to discover and complete recipe protocols.

             By the way, there is one thing you should remember: you can always revisit all the guidelines by pressing {"[G] key".WithColor(Orange)}.

             {"Have fun with fractionation!".WithColor(Blue)}
             """,
            $"""
             着陆区附近探测到了异常旧文明信号。第一次接触并不稳定，但回收得到的启动套件里包含数据中心通信方式、初始分馏塔原胚和一座培养塔。

             以下是一个简短的前期指引：
             1.按异常信号提示前往信号点，回收旧文明启动套件。数据中心通信建立后，按{"[Shift + F]键".WithColor(Orange)}即可连接到分馏数据中心。
             2.打开分馏数据中心的“恢复手册”页面，按步骤学习如何使用培养塔将原胚培养为新的分馏塔，以及如何使用培养塔上传物品。
             建设培养塔之后，也会有新的指引对此进行讲解。
             3.持续培养通用原胚，直到获得新的培养塔；将其上传至分馏数据中心，恢复“培养塔”协议。
             这是一项极其强大的辅助功能，无视空间限制，你可以将它视为具有无限容量的外部背包。
             4.搭建矩阵产线，用解析塔处理矩阵或黑雾样本产出残片（Frag）；将残片面额实体上传入账后，直接消耗残片进行协议检索，用于发现并补全配方协议。

             对了，有一件事情你要记住：你可以随时按{"[G]键".WithColor(Orange)}重新查阅所有指引。

             {"尽情享受分馏的乐趣吧！".WithColor(Blue)}
             """);
        Register("万物分馏简介后字", "", "");

        Register("分馏数据中心标题", "Fractionation data centre", "分馏数据中心");
        Register("分馏数据中心前字",
            $"""
             You can now connect to the Fractionation Data Centre by pressing {"[Shift + F] key".WithColor(Orange)}.
             This recovered old-civilization panel manages physical resources, protocol recovery, the ancient technology tree, and achievements.

             {"[Core Loop]".WithColor(Blue)}
             1. Build a matrix production line.
             2. Feed matrices or dark-fog samples into the Analysis Tower to produce Frag. Memory is derived from Frag yield.
             3. Upload Frag/Memory denomination items through a Cultivation Tower or another data-centre upload path to credit the balances.
             4. Open Protocol Recovery and start with broad retrieval, which spends Frag directly. When no eligible incomplete candidates remain globally, broad retrieval contributes base Frag cost to deep analysis. Use directional or higher-cost anchored retrieval as needed.
             5. At 100% completeness you only gain purchase eligibility. Formal unlock still requires Memory plus the required physical items. You may prioritise one discovered protocol; batch retrieval still settles one step at a time under the same rules.

             {"[Ancient Technology]".WithColor(Blue)}
             When broad retrieval finds no eligible incomplete candidates, the spent base Frag cost accumulates into deep analysis. Deep analysis produces the single shared Ancient Technology Point type.
             Spend these points on the four tower branches. Each branch unlocks flow stacking, product stacking, continuous fractionation, main-output locking, and byproduct discard in order. The last two also require actual recipe calibration and a per-building setting.

             {"[Recipe Operation]".WithColor(Blue)}
             The Recipe page displays output structure and actual running parameters. Replication and Conversion recipes display protocol discovery and completeness instead of legacy recipe levels.
             Equivalent Output estimates expected production after recipe, tower, proliferator, and achievement modifiers.

             {"[Item Interaction]".WithColor(Blue)}
             Item Interaction is an extremely powerful and convenient protocol, much loved by various Icarus.

             After the Cultivation Tower protocol is restored, most actions can be performed using items stored in the Fractionation Data Centre, rather than having to extract them from the backpack and use them again. Examples include:
             Fabrication Table to manually craft items; Quick Build Bar to select a building; Tech to manually research; Building TAB to fill items; Fuel Fill; Warp Fill; Ammo Fill; Drone Fill ...... and many more.
             However, to use these handy features, you need to restore the Cultivation Tower protocol before storing a sufficient number of items to the Fractionation Data Centre.

             How to upload items:
             1. The Cultivation Tower will upload items entered on the front interface. Check the [Fractionation Tower User Guide] guide for more information.
             2. The Logistics Interactive Station will upload items that meet certain conditions. Check the 'Logistics Interactive Station User's Guide' for more information.
             3. When you double-click the backpack button, all items in your backpack will be uploaded (the Cultivation Tower protocol must be restored first, and your logistics backpack will not be affected).

             How to download items:
             1. On the Item Interaction or Important Items page, left-click or right-click the corresponding item to extract it. You can set the number of groups to be extracted on the Miscellaneous Settings page.
             2. Logistics Interactive Station downloads items that meet certain conditions. For more information, refer to the 'Logistics Interactive Station User's Guide'.
             3. Various fill operations (manual creation, manual research, TAB fill, fuel fill, etc.) will automatically download items and use them.

             {"[Achievements]".WithColor(Blue)}
             Achievements are independent fixed single-save milestones. They consume no technology points and apply their rewards automatically when their production or progression conditions are met.
             """,
            $"""
             现在，你可以按{"[Shift + F]键".WithColor(Orange)}连接到分馏数据中心。
             这是一个已恢复的旧文明总控面板，用于管理实体资源、协议恢复、远古科技树和单存档成就。

             {"【核心流程】".WithColor(Blue)}
             1.搭建矩阵生产线。
             2.将矩阵或黑雾样本输入解析塔，产出残片（Frag）；记忆源点（Memory）按残片产量派生。
             3.通过培养塔或其他数据中心上传入口，将残片/记忆源点面额实体上传入账。
             4.进入“文明协议恢复”后先使用广域检索，直接消耗残片。当全局已无合格且未满的候选协议时，广域检索消耗的基础残片成本才累计进入深层解析。之后可按需使用方向检索或成本更高的锚定检索。
             5.协议完整度达到 100% 仅取得购买资格；正式解锁仍需记忆源点与所需实物。已发现协议可以设为优先目标；批量检索仍按同一单次规则逐次结算。

             {"【远古科技】".WithColor(Blue)}
             当广域检索找不到合格且未满的候选协议时，消耗的基础残片成本会累计进入深层解析。深层解析只产出一种“远古文明科技点”。
             科技点可以投入培养塔、解析塔、复制塔和转化塔四条主干。每条主干依次解锁流动输出堆叠、产物输出堆叠、分馏永动、主路锁定和副产物弃置；后两项还要求当前配方完成实际生产校准，并由单塔保存设置。

             {"【配方操作】".WithColor(Blue)}
             配方页面用于查看产物结构和实际运行参数。复制与转化配方的右侧信息改为协议发现状态和完整度，不再显示旧配方等级。
             “等效输出”会综合配方、塔型、增产剂和成就加成，用于估算平均产出。

             {"【物品交互】".WithColor(Blue)}
             物品交互是一项极其强大的便利协议，深受各个伊卡洛斯的喜爱。

             在培养塔协议恢复后，绝大多数操作都可以直接使用分馏数据中心存储的物品，而不需要提取物品到背包再使用。例如：
             制造台手动制作物品；快捷建造栏选择建筑；科技手动研究；建筑TAB填充物品；燃料填充；翘曲填充；弹药填充；无人机填充……等等。
             不过，要想使用这些便利的功能，你需要先恢复培养塔协议，再向分馏数据中心存储足够数目的物品。

             如何上传物品：
             1.培养塔会上传正面接口输入的物品。查阅【分馏塔使用指南】指引以了解更多信息。
             2.物流交互站会上传满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.双击整理背包按钮时，背包内的物品会全部上传（需恢复“培养塔”协议，物流背包不受影响）。

             如何下载物品：
             1.在物品交互页面，左键或右键点击对应物品即可提取。可以在杂项设置页面设定提取的组数。
             2.物流交互站会下载满足一定条件的物品。查阅【物流交互站使用指南】指引以了解更多信息。
             3.各种填充操作（手动制作、手动研究、TAB填充、燃料填充等等）会自动下载物品并使用。

             {"【成就】".WithColor(Blue)}
             成就是单存档固定里程碑，不消耗科技点。达到生产或探索条件后会自动完成，并把固定奖励投影到分馏运行逻辑。
             """
        );
        Register("分馏数据中心后字", "", "");


        Register("分馏塔使用指南标题", "Fractionator guidelines", "分馏塔使用指南");
        Register("分馏塔使用指南前字",
            $"""
             {"[Cultivate Fractionation Tower]".WithColor(Blue)}
             In the old-civilization recovery system, Fractionation Towers are no longer obtained by manufacturing, but mainly by cultivating them in Cultivation Towers.
             A common proto randomly cultivates into one of four tower types. A dedicated Cultivation, Analysis, Replication, or Conversion proto always cultivates into its matching tower.
             The Analysis Tower can differentiate common protos into dedicated lineages. After enough dedicated cultivation and protocol recovery, a Cultivation Tower can reverse a built tower into its matching dedicated proto.
             Note that {"Only one type of item can be processed by any Fractionation Tower at any one time".WithColor(Orange)}, so don't mix different proto types in one tower!
             Items can be uploaded to the Fractionation Data Centre by feeding the output Fractionation Tower through a conveyor belt to the front interface of another Cultivation Tower, thus restoring the corresponding old-civilization protocol.
             Uploading different Fractionation Towers registers their tower type. For example, uploading a Replication Tower restores its control protocol, while uploading a Cultivation Tower restores the Cultivation Tower protocol.
             Note that only Cultivation Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. This means that there can be no items inside the tower, and the left and right ports cannot be connected to a conveyor belt.

             {"[Cultivation Tower]".WithColor(Blue)}
             The Cultivation Tower has two functions: to grow embryos into various fractionation towers, and to upload items.
             Only Cultivation Towers in 'Item Interaction' mode can upload positively entered items to the Fractionation Data Centre. There is no limit to the upload rate, meaning that a single tower can upload a full belt of items.

             {"[Replication Tower]".WithColor(Blue)}
             Replication Towers replicate raw resources. Their recipe protocols are recovered through civilization analysis.

             {"[Analysis Tower]".WithColor(Blue)}
             The Analysis Tower processes matrices and dark-fog samples into Frag. Memory is derived from Frag yield. Upload the denomination items to credit the data centre, then spend Frag on protocol retrieval.

             {"[Conversion Tower]".WithColor(Blue)}
             The Conversion Tower can convert items into other related items.
             """,
            $"""
             {"【培养分馏塔】".WithColor(Blue)}
             在旧文明恢复体系中，分馏塔不再通过制造得到，而是主要通过培养塔培养得到。
             通用原胚会随机培养为培养、解析、复制或转化塔；四种专属原胚会稳定培养为对应塔型。解析塔可以把通用原胚分化为专属谱系；专属培养达到资格并恢复返祖协议后，培养塔还能把实体塔还原为对应专属原胚。
             注意，{"任何分馏塔同一时间只能处理一种物品".WithColor(Orange)}，所以不同类型的原胚不要混投！
             将产出的分馏塔通过传送带输入至另一个培养塔的正面接口，即可上传物品至分馏数据中心，从而恢复对应的旧文明协议。
             上传不同分馏塔会完成对应塔型注册。例如，上传复制塔会恢复复制塔的旧文明控制协议，上传培养塔会恢复培养塔协议。
             注意，只有处于“物品交互”模式下的培养塔才能上传正面输入的物品到分馏数据中心。也就是说，培养塔内部不能有物品，并且左右接口不能与传送带连接。

             如果你不确定第一轮该怎么搭建，可以打开分馏数据中心的“恢复手册”页面。推荐顺序是：先放下培养塔，将左右口接成环，正面输出接临时箱子；持续输入通用原胚并保留随机产出的塔，直到获得第二台培养塔；再拆掉临时箱子，把第一台塔的产物接入第二台塔的正面入口完成上传。

             {"【培养塔】".WithColor(Blue)}
             培养塔有两个功能：将原胚培养为各种分馏塔，以及上传物品。
             只有处于“物品交互”模式下的培养塔才能上传正面输入的物品到分馏数据中心。上传速率没有限制，也就是说，一个培养塔可以上传一满带的物品。

             {"【复制塔】".WithColor(Blue)}
             复制塔用于复制原矿资源，对应配方协议通过文明解析逐步恢复。

             {"【解析塔】".WithColor(Blue)}
             解析塔处理矩阵与黑雾样本产出残片（Frag）；记忆源点（Memory）按残片产量派生。将面额实体上传入账后，直接消耗残片进行协议检索。

             {"【转化塔】".WithColor(Blue)}
             转化塔可以将物品转化为其他相关的物品。
             """
        );
        Register("分馏塔使用指南后字", "", "");

        Register("物流交互站使用指南标题", "Interaction station guidelines", "物流交互站使用指南");
        Register("物流交互站使用指南前字",
            $"""
             The Interaction Station is a logistic station that can interact with the Fractionation Data Centre.

             The Interaction Station has multiple modes to adapt to different scenarios.
             1. Items can be downloaded from the data centre when supply is unlocked or demand is locked. Items will no longer be downloaded after the slots are above the set threshold (initial value 20%).
             2. When supply is locked or demand is unlocked, items can be uploaded to the data centre. Items will no longer be uploaded after the items in the slot are below the set threshold (80% of the initial value). After a certain number of items have been stored in the Fractionation Data Centre (10 groups for buildings, 100 groups for non-buildings), items can no longer be uploaded in this way.
             The threshold value can be modified on the Miscellaneous Settings page.
             3. When storage is unlocked, the number of items will be maintained at half of the slot limit as much as possible. There is no limit to the number of items that can be uploaded in this way.
             4. When storage is locked, the number of items will be kept as close as possible to the number of items currently stored in the Fractionation Data Centre.
             When the interstellar policy and local policy are different, they will take effect at the same time; when the interstellar policy is storage, only the local policy will be considered.

             Uploading and downloading items consumes power from the Interaction Station. The higher the value of the item, the more power it consumes.
             The update frequency of the Interaction Station is 30 ticks (0.5s). When uploading or downloading items, individual slots consume up to '1/number of slots' of the Interaction Station's current power each time.

             You can use the lift function to replace a logistic station with a corresponding interaction station. For example, Planetary Logistic Station can be upgraded to Planetary Interaction Station.
             """,
            $"""
             物流交互站是可以与分馏数据中心进行物品交互的物流运输站。

             物流交互站具有多种模式，以便于适配不同的场景。
             1.供应无锁或需求锁定时，可从数据中心下载物品。槽位的物品高于设定的阈值（初始值20%）之后，物品将不再下载。
             2.供应锁定或需求无锁时，可上传物品至数据中心。槽位的物品低于设定的阈值（初始值80%）之后，物品将不再上传。当分馏数据中心存储的物品达到一定数目后（建筑10组，非建筑100组），无法再通过此方式上传物品。
             阈值可以在杂项设置页面修改。
             3.仓储无锁时，物品数目将尽量维持在槽位上限的一半。此方式上传物品没有数目限制。
             4.仓储锁定时，物品数目将尽量与分馏数据中心当前存储的物品数目保持一致。
             当星际策略和本地策略不同时，它们将同时生效；当星际策略为仓储时，仅考虑本地策略。

             上传、下载物品都会消耗物流交互站的电力。物品价值越高，消耗的电力越大。
             物流交互站的更新频率是30tick（0.5s）。上传或下载物品时，单个槽位每次至多消耗物流交互站当前电量的“1/槽位数目”。

             你可以使用升降级功能，将物流运输站与对应的物流交互站替换。例如，行星内物流运输站可以升级为行星内物流交互站。
             """
        );
        Register("物流交互站使用指南后字", "", "");
    }
}

#endregion // 教程文本

#region 教程窗口补丁

/// <summary>
/// 教程窗口布局兼容补丁。
/// </summary>
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
            // 触发 ScrollRect.OnEnable() 以添加监听器
            __instance.entryList.m_ScrollRect.enabled = false;
            __instance.entryList.m_ScrollRect.enabled = true;
        }
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

            IL_0027: ldsfld       string UITutorialWindow::textFolder // originalLogicLabel（原逻辑标签）
            IL_002c: ldarg.0      // this（当前实例）
            IL_002d: ldfld        class TutorialProto UITutorialWindow::tutorialProto
            IL_0032: ldfld        string TutorialProto::LayoutFileName
            IL_0037: call         string UILayoutParserManager::GetLayoutStr(string, string)
            IL_003c: stloc.0      // layoutStr（布局字符串） // endLabel（结束标签）

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

#endregion // 教程窗口补丁
