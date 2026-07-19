using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Progression;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ProgressTask;

/// <summary>
/// 旧文明恢复手册页面：把 FE 前期非原版操作拆成可回看、可确认的步骤清单。
/// </summary>
public static class RecoveryGuide {
    private const int ManualStepCount = 5;
    private const string ManualStepsBlockTag = "ManualStepsV1";

    private static readonly GuideStep[] initialProtoSteps = [
        new("恢复手册步骤-启动套件", "恢复手册步骤说明-启动套件", StepKind.Auto, HasStarterKit),
        new("恢复手册步骤-打开面板", "恢复手册步骤说明-打开面板", StepKind.Auto, HasDataCenterAccess),
        new("恢复手册步骤-放置首塔", "恢复手册步骤说明-放置首塔", StepKind.Auto, HasFirstInteractionTowerReady),
        new("恢复手册步骤-左右成环", "恢复手册步骤说明-左右成环", StepKind.Manual, manualIndex: 0),
        new("恢复手册步骤-临时箱子", "恢复手册步骤说明-临时箱子", StepKind.Manual, manualIndex: 1),
        new("恢复手册步骤-输入原胚", "恢复手册步骤说明-输入原胚", StepKind.Manual, manualIndex: 2),
        new("恢复手册步骤-获得第二塔", "恢复手册步骤说明-获得第二塔", StepKind.Manual, manualIndex: 3),
        new("恢复手册步骤-接入上传", "恢复手册步骤说明-接入上传", StepKind.Manual, manualIndex: 4),
        new("恢复手册步骤-恢复协议", "恢复手册步骤说明-恢复协议", StepKind.Auto, HasItemInteractionProtocol),
    ];

    private static readonly string[] laterChapterKeys = [
        "恢复手册章节-数据中心",
        "恢复手册章节-时隧检索",
        "恢复手册章节-塔型注册",
        "恢复手册章节-物流交互",
        "恢复手册章节-精馏"
    ];

    private static readonly bool[] manualStepCompleted = new bool[ManualStepCount];

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtCurrentHint;
    private static Text txtCurrentDetail;
    private static readonly Text[] stepStateTexts = new Text[initialProtoSteps.Length];
    private static readonly Text[] stepTitleTexts = new Text[initialProtoSteps.Length];
    private static readonly Text[] stepDetailTexts = new Text[initialProtoSteps.Length];
    private static readonly MyCheckBox[] manualChecks = new MyCheckBox[ManualStepCount];
    private static readonly Text[] chapterTexts = new Text[laterChapterKeys.Length];
    private static Text txtFooter;

    private readonly struct GuideStep(
        string titleKey,
        string detailKey,
        StepKind kind,
        Func<bool> isCompleted = null,
        int manualIndex = -1) {
        public readonly string TitleKey = titleKey;
        public readonly string DetailKey = detailKey;
        public readonly StepKind Kind = kind;
        public readonly Func<bool> IsCompleted = isCompleted;
        public readonly int ManualIndex = manualIndex;
    }

    private enum StepKind {
        Auto,
        Manual,
    }

    public static void AddTranslations() {
        Register("恢复手册", "Recovery Manual", "恢复手册");
        Register("恢复手册页头摘要",
            "Checklist for old-civilization recovery, first interaction tower loop, and later system chapters.",
            "旧文明恢复、交互塔原胚孵化和后续系统章节的操作清单。");
        Register("恢复手册-当前目标", "Current Objective", "当前目标");
        Register("恢复手册-原胚孵化", "Initial Proto Incubation", "初始原胚孵化");
        Register("恢复手册-后续章节", "Later Chapters", "后续章节");
        Register("恢复手册-自动检测", "Auto", "自动");
        Register("恢复手册-玩家确认", "Confirm", "确认");
        Register("恢复手册-确认完成", "Mark Done", "确认完成");
        Register("恢复手册-已完成", "Done", "已完成");
        Register("恢复手册-未完成", "Pending", "未完成");
        Register("恢复手册-状态", "State", "状态");
        Register("恢复手册-类型", "Type", "类型");
        Register("恢复手册-步骤", "Step", "步骤");
        Register("恢复手册-当前目标完成", "Initial loop complete. Continue with retrieval and tower registration.",
            "第一轮闭环已完成。接下来继续进行文明协议恢复和塔型注册。");
        Register("恢复手册-页脚",
            "Manual confirmations only record guide progress. They do not unlock production, grant items, or replace real recovery conditions.",
            "手动确认只记录指引进度，不解锁生产、不发放物品，也不替代真实恢复条件。");

        Register("恢复手册步骤-启动套件", "Recover the starter kit", "回收启动套件");
        Register("恢复手册步骤说明-启动套件",
            "Find the abnormal old-civilization signal and recover the communication module, Interaction Tower, and starter protos.",
            "前往异常旧文明信号点，回收通信模块、交互塔和初始原胚。");
        Register("恢复手册步骤-打开面板", "Open the data centre", "打开分馏数据中心");
        Register("恢复手册步骤说明-打开面板",
            "Use Shift+F after communication is established. This manual remains available for review.",
            "通信建立后按 Shift+F 打开数据中心；本手册可随时回看。");
        Register("恢复手册步骤-放置首塔", "Place the first Interaction Tower", "放置第一个交互塔");
        Register("恢复手册步骤说明-放置首塔",
            "Use the starter Interaction Tower. It is the first machine that can incubate tower protos.",
            "使用启动套件中的交互塔；它是第一台能孵化塔型原胚的设备。");
        Register("恢复手册步骤-左右成环", "Connect left and right ports into a loop", "左右口连接成环");
        Register("恢复手册步骤说明-左右成环",
            "Build a belt loop on the left and right ports. The loop keeps the proto cycling through the tower.",
            "在左右口之间接一圈传送带，让原胚持续经过交互塔。");
        Register("恢复手册步骤-临时箱子", "Send the front output to a temporary box", "正面输出先接临时箱子");
        Register("恢复手册步骤说明-临时箱子",
            "Use a temporary box to catch the first produced Interaction Tower before the upload tower exists.",
            "先用临时箱子接住第一台产出的交互塔，因为此时还没有第二台上传塔。");
        Register("恢复手册步骤-输入原胚", "Feed Interaction Tower protos into the loop", "向环输入交互塔原胚");
        Register("恢复手册步骤说明-输入原胚",
            "Do not mix different proto types in the same tower. The Interaction Tower proto produces an Interaction Tower.",
            "不要在同一台塔里混投不同原胚；交互塔原胚会产出交互塔。");
        Register("恢复手册步骤-获得第二塔", "Produce and place the second Interaction Tower", "获得并放置第二个交互塔");
        Register("恢复手册步骤说明-获得第二塔",
            "After the first tower produces another Interaction Tower, place it next to the line as the upload tower.",
            "第一台塔产出新的交互塔后，把它放到产线旁作为上传塔。");
        Register("恢复手册步骤-接入上传", "Remove the box and feed the second tower front port", "拆箱并接入第二塔正面");
        Register("恢复手册步骤说明-接入上传",
            "Remove the temporary box, then send the first tower output into the second tower front port.",
            "拆掉临时箱子，把第一台塔的产物接到第二台塔的正面入口。");
        Register("恢复手册步骤-恢复协议", "Recover the item interaction protocol", "恢复物品交互协议");
        Register("恢复手册步骤说明-恢复协议",
            "Uploading an Interaction Tower registers the protocol and enables item interaction through the data centre.",
            "上传交互塔会注册协议，并恢复数据中心物品交互能力。");

        Register("恢复手册章节-数据中心", "Data centre storage: upload, extract, and view current stock.",
            "数据中心库存：上传、提取和查看当前储量。");
        Register("恢复手册章节-时隧检索", "Protocol recovery: upload analysis data, then spend retrieval opportunities to discover and complete recipes.",
            "协议恢复：上传解析数据，再消耗检索机会发现并补全配方协议。");
        Register("恢复手册章节-塔型注册", "Tower registration: upload each produced tower to recover its protocol.",
            "塔型注册：上传各类产出的分馏塔，恢复对应协议。");
        Register("恢复手册章节-物流交互", "Interaction stations: automate data-centre upload and download after the first loop.",
            "物流交互站：第一轮闭环后，自动化数据中心上传和下载。");
        Register("恢复手册章节-精馏", "Civilization analysis: turn matrices into physical analysis data for protocol recovery.",
            "文明解析：将矩阵转为实体解析数据，用于恢复配方协议。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(190f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                cols: [2, 1],
                columnGap: PageLayout.Gap,
                children: [
                    Header("恢复手册", "恢复手册页头摘要", objectName: "recovery-guide-header",
                        pos: (0, 0), span: (1, 2), onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "recovery-current-card", strong: true,
                        rows: [Px(28f), Px(34f), 1],
                        rowGap: 8f,
                        children: [
                            CardTitleNode("恢复手册-当前目标", pos: (0, 0),
                                objectName: "recovery-current-title"),
                            TextNode("", 15, Orange, wrap: true, onBuilt: text => {
                                    txtCurrentHint = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 0), objectName: "recovery-current-hint"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => {
                                    txtCurrentDetail = text;
                                    text.supportRichText = true;
                                },
                                pos: (2, 0), objectName: "recovery-current-detail"),
                        ]),
                    ContentCard(pos: (1, 1), objectName: "recovery-chapters-card",
                        rows: BuildLaterChapterRows(),
                        rowGap: 4f,
                        children: BuildLaterChapterNodes()),
                    ScrollableContentCard(680f, pos: (2, 0), span: (1, 2),
                        objectName: "recovery-proto-scroll", strong: true,
                        rows: BuildStepRows(),
                        cols: [Px(56f), Px(150f), 1, Px(128f)],
                        rowGap: 8f,
                        columnGap: 10f,
                        children: BuildStepNodes()),
                    FooterCard(pos: (3, 0), span: (1, 2), objectName: "recovery-footer-card",
                        children: [
                            TextNode("", 12, Gray, wrap: true, onBuilt: text => {
                                    txtFooter = text;
                                    text.supportRichText = true;
                                },
                                pos: (0, 0), objectName: "recovery-footer-text"),
                        ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "恢复手册".Translate().WithColor(Orange);
        header.Summary.text = "恢复手册页头摘要".Translate().WithColor(White);
        txtFooter.text = "恢复手册-页脚".Translate().WithColor(Gray);

        int firstIncompleteIndex = GetFirstIncompleteStepIndex();
        if (firstIncompleteIndex < 0) {
            txtCurrentHint.text = "恢复手册-当前目标完成".Translate().WithColor(Green);
            txtCurrentDetail.text = "恢复手册步骤说明-恢复协议".Translate().WithColor(White);
        } else {
            GuideStep step = initialProtoSteps[firstIncompleteIndex];
            txtCurrentHint.text = step.TitleKey.Translate().WithColor(Orange);
            txtCurrentDetail.text = step.DetailKey.Translate().WithColor(White);
        }

        for (int i = 0; i < initialProtoSteps.Length; i++) {
            GuideStep step = initialProtoSteps[i];
            bool completed = IsStepCompleted(step);
            stepStateTexts[i].text = GetStepStateText(completed);
            stepTitleTexts[i].text = step.TitleKey.Translate().WithColor(completed ? Green : Orange);
            stepDetailTexts[i].text = step.DetailKey.Translate().WithColor(White);
            if (step.Kind == StepKind.Manual && IsManualIndexValid(step.ManualIndex)) {
                MyCheckBox check = manualChecks[step.ManualIndex];
                if (check != null && check.Checked != manualStepCompleted[step.ManualIndex]) {
                    check.Checked = manualStepCompleted[step.ManualIndex];
                }
            }
        }

        for (int i = 0; i < chapterTexts.Length; i++) {
            chapterTexts[i].text = laterChapterKeys[i].Translate().WithColor(White);
        }
    }

    public static void Import(BinaryReader r) {
        IntoOtherSave();
        r.ReadBlocks(
            (ManualStepsBlockTag, br => {
                int count = Math.Min(Math.Max(0, br.ReadInt32()), manualStepCompleted.Length);
                for (int i = 0; i < count; i++) {
                    manualStepCompleted[i] = br.ReadBoolean();
                }
                for (int i = count; i < manualStepCompleted.Length; i++) {
                    manualStepCompleted[i] = false;
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            (ManualStepsBlockTag, bw => {
                bw.Write(manualStepCompleted.Length);
                foreach (bool completed in manualStepCompleted) {
                    bw.Write(completed);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(manualStepCompleted, 0, manualStepCompleted.Length);
    }

    private static LayoutTrack[] BuildLaterChapterRows() {
        var rows = new LayoutTrack[laterChapterKeys.Length + 1];
        rows[0] = Px(28f);
        for (int i = 1; i < rows.Length; i++) {
            rows[i] = Px(24f);
        }
        return rows;
    }

    private static LayoutNode[] BuildLaterChapterNodes() {
        var nodes = new LayoutNode[laterChapterKeys.Length + 1];
        nodes[0] = CardTitleNode("恢复手册-后续章节", pos: (0, 0), objectName: "recovery-chapters-title");
        for (int i = 0; i < laterChapterKeys.Length; i++) {
            int index = i;
            nodes[i + 1] = TextNode("", 12, White, wrap: true,
                onBuilt: text => {
                    chapterTexts[index] = text;
                    text.supportRichText = true;
                },
                pos: (i + 1, 0), objectName: $"recovery-chapter-{i}");
        }
        return nodes;
    }

    private static LayoutTrack[] BuildStepRows() {
        var rows = new LayoutTrack[initialProtoSteps.Length + 2];
        rows[0] = Px(30f);
        rows[1] = Px(24f);
        for (int i = 2; i < rows.Length; i++) {
            rows[i] = Px(56f);
        }
        return rows;
    }

    private static LayoutNode[] BuildStepNodes() {
        var nodes = new LayoutNode[4 + initialProtoSteps.Length * 4];
        int offset = 0;
        nodes[offset++] = CardTitleNode("恢复手册-原胚孵化", pos: (0, 0), span: (1, 4),
            objectName: "recovery-proto-title");
        nodes[offset++] = TextNode("恢复手册-状态", 12, Gray, anchor: TextAnchor.MiddleCenter,
            pos: (1, 0), objectName: "recovery-step-header-state");
        nodes[offset++] = TextNode("恢复手册-类型", 12, Gray, anchor: TextAnchor.MiddleCenter,
            pos: (1, 1), objectName: "recovery-step-header-kind");
        nodes[offset++] = TextNode("恢复手册-步骤", 12, Gray,
            pos: (1, 2), objectName: "recovery-step-header-title");

        for (int i = 0; i < initialProtoSteps.Length; i++) {
            int stepIndex = i;
            GuideStep step = initialProtoSteps[i];
            int row = i + 2;
            nodes[offset++] = TextNode("", 13, anchor: TextAnchor.MiddleCenter,
                onBuilt: text => {
                    stepStateTexts[stepIndex] = text;
                    text.supportRichText = true;
                },
                pos: (row, 0), objectName: $"recovery-step-state-{i}");
            nodes[offset++] = TextNode(GetStepKindText(step), 12, Gray, anchor: TextAnchor.MiddleCenter,
                pos: (row, 1), objectName: $"recovery-step-kind-{i}");
            nodes[offset++] = Grid(pos: (row, 2), rows: [Px(22f), 1], rowGap: 2f,
                children: [
                    TextNode("", 13, Orange, wrap: true, onBuilt: text => {
                            stepTitleTexts[stepIndex] = text;
                            text.supportRichText = true;
                        },
                        pos: (0, 0), objectName: $"recovery-step-title-{i}"),
                    TextNode("", 12, White, anchor: TextAnchor.UpperLeft, wrap: true,
                        onBuilt: text => {
                            stepDetailTexts[stepIndex] = text;
                            text.supportRichText = true;
                        },
                        pos: (1, 0), objectName: $"recovery-step-detail-{i}"),
                ]);
            nodes[offset++] = BuildActionNode(step, row, i);
        }
        return nodes;
    }

    private static LayoutNode BuildActionNode(GuideStep step, int row, int stepIndex) {
        if (step.Kind != StepKind.Manual || !IsManualIndexValid(step.ManualIndex)) {
            return TextNode("", pos: (row, 3), objectName: $"recovery-step-auto-action-{stepIndex}");
        }

        return CheckBoxNode(false, "恢复手册-确认完成", 12,
            onBuilt: check => {
                manualChecks[step.ManualIndex] = check;
                check.Checked = manualStepCompleted[step.ManualIndex];
                check.OnChecked += () => {
                    manualStepCompleted[step.ManualIndex] = check.Checked;
                    UpdateUI();
                };
            },
            pos: (row, 3), objectName: $"recovery-step-manual-{stepIndex}");
    }

    private static int GetFirstIncompleteStepIndex() {
        for (int i = 0; i < initialProtoSteps.Length; i++) {
            if (!IsStepCompleted(initialProtoSteps[i])) {
                return i;
            }
        }
        return -1;
    }

    private static bool IsStepCompleted(GuideStep step) {
        return step.Kind switch {
            StepKind.Manual => IsManualIndexValid(step.ManualIndex) && manualStepCompleted[step.ManualIndex],
            _ => step.IsCompleted?.Invoke() == true,
        };
    }

    private static bool IsManualIndexValid(int index) {
        return index >= 0 && index < manualStepCompleted.Length;
    }

    private static string GetStepKindText(GuideStep step) {
        return (step.Kind == StepKind.Manual ? "恢复手册-玩家确认" : "恢复手册-自动检测").Translate();
    }

    private static string GetStepStateText(bool completed) {
        string label = (completed ? "恢复手册-已完成" : "恢复手册-未完成").Translate();
        string marker = completed ? "✓" : "○";
        return $"{marker} {label}".WithColor(completed ? Green : Gray);
    }

    private static bool HasStarterKit() {
        return HasDataCenterAccess()
               || GetItemTotalCount(IFE交互塔) > 0
               || GetItemTotalCount(IFE交互塔原胚) > 0
               || DataCenterInventory.centerItemCount[IFE交互塔] > 0
               || DataCenterInventory.centerItemCount[IFE交互塔原胚] > 0;
    }

    private static bool HasDataCenterAccess() {
        return CivilizationRecoveryManager.HasDataCenterCommunication;
    }

    private static bool HasFirstInteractionTowerReady() {
        return GetItemTotalCount(IFE交互塔) > 0
               || DataCenterInventory.centerItemCount[IFE交互塔] > 0;
    }

    private static bool HasItemInteractionProtocol() {
        return GameMain.history != null && GameMain.history.TechUnlocked(TFE物品交互);
    }
}
