using System.IO;
using BepInEx.Configuration;
using FE.Logic.DataCenter;
using FE.Logic.Civilization;
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
/// 旧文明恢复手册页面：上方“当前目标”按真实里程碑动态显示，下方为始终可回看的静态三组搭建步骤。
/// 不含任何手动确认控件、Manual/Auto 类型列或伪完成状态；进度全部由真实游戏状态自动判定。
/// </summary>
public static class RecoveryGuide {
    /// <summary>
    /// 旧版手动步骤存档块标签。仅用于读取并忽略旧存档内容；新导出不再写入该块。
    /// </summary>
    private const string ManualStepsBlockTag = "ManualStepsV1";

    /// <summary>
    /// 上方当前目标卡片：三个真实里程碑对应的标题与说明翻译键。
    /// </summary>
    private static readonly string[] milestoneHintKeys = [
        "恢复手册-目标-启动套件",
        "恢复手册-目标-培养闭环",
        "恢复手册-目标-协议已恢复",
    ];

    /// <summary>
    /// 上方当前目标卡片三个真实里程碑对应的详细说明翻译键。
    /// </summary>
    private static readonly string[] milestoneDetailKeys = [
        "恢复手册-当前说明-启动套件",
        "恢复手册-当前说明-培养闭环",
        "恢复手册-当前说明-协议已恢复",
    ];

    /// <summary>
    /// 下方静态搭建步骤的三个分组：组标题键 + 组内项目符号步骤标题键。
    /// 不随里程碑变化，始终完整可回看。
    /// </summary>
    private static readonly (string groupKey, string[] bulletKeys)[] bottomGroups = [
        ("恢复手册-分组-启动准备",
            new[] { "恢复手册步骤-启动套件", "恢复手册步骤-打开面板" }),
        ("恢复手册-分组-培养闭环",
            new[] {
                "恢复手册步骤-放置首塔",
                "恢复手册步骤-左右成环",
                "恢复手册步骤-临时箱子",
                "恢复手册步骤-输入原胚",
                "恢复手册步骤-获得第二塔",
            }),
        ("恢复手册-分组-协议接入",
            new[] { "恢复手册步骤-接入上传", "恢复手册步骤-恢复协议" }),
    ];

    /// <summary>
    /// 下方主卡内除总标题外的行数：每个分组贡献 1 行组标题 + 其项目符号行数。
    /// </summary>
    private static readonly int bottomRowCount = ComputeBottomRowCount();

    /// <summary>
    /// 后续章节卡片使用的短标题键；刻意保持单行以避免多语言换行重叠。
    /// </summary>
    private static readonly string[] laterChapterKeys = [
        "恢复手册章节短-数据中心",
        "恢复手册章节短-协议检索",
        "恢复手册章节短-塔型注册",
        "恢复手册章节短-物流交互",
        "恢复手册章节短-文明解析",
    ];

    /// <summary>
    /// 当前恢复手册页的根节点，用于避免离屏刷新。
    /// </summary>
    private static RectTransform tab;

    /// <summary>
    /// 页面页头标题和摘要文本引用。
    /// </summary>
    private static PageLayout.HeaderRefs header;

    /// <summary>
    /// 当前真实里程碑的短标题文本。
    /// </summary>
    private static Text currentHintText;

    /// <summary>
    /// 当前真实里程碑的操作说明文本。
    /// </summary>
    private static Text currentDetailText;

    /// <summary>
    /// 说明自动目标与静态步骤职责边界的页脚文本。
    /// </summary>
    private static Text footerText;

    /// <summary>
    /// 注册恢复手册的当前目标、静态分组步骤和短章节标题文本。
    /// </summary>
    public static void AddTranslations() {
        Register("恢复手册", "Recovery Manual", "恢复手册");
        Register("恢复手册页头摘要",
            "Old-civilization recovery manual: current objective, cultivation loop, and later chapters.",
            "旧文明恢复手册：当前目标、培养闭环与后续章节。");
        Register("恢复手册-当前目标", "Current Objective", "当前目标");
        Register("恢复手册-后续章节", "Later Chapters", "后续章节");
        Register("恢复手册-原胚培养", "Initial Proto Cultivation", "初始原胚培养");
        Register("恢复手册-页脚",
            "Current Objective follows real game state; the grouped steps below remain available for review.",
            "当前目标跟随真实游戏状态；下方分组步骤始终可回看。");

        Register("恢复手册-目标-启动套件", "Recover Starter Kit", "回收启动套件");
        Register("恢复手册-目标-培养闭环", "Build Cultivation Loop", "搭建培养闭环");
        Register("恢复手册-目标-协议已恢复", "Protocol Restored", "培养塔协议已恢复");

        Register("恢复手册-当前说明-启动套件",
            "Follow the abnormal old-civilization signal on the birth planet, recover the starter kit, then open the data centre.",
            "前往出生星上的异常旧文明信号点回收启动套件，再打开分馏数据中心。");
        Register("恢复手册-当前说明-培养闭环",
            "Build the first cultivation loop so a Cultivation Tower can be produced and uploaded.",
            "搭建第一个培养闭环，产出并上传一座培养塔。");
        Register("恢复手册-当前说明-协议已恢复",
            "The cultivation tower protocol is restored. Continue with protocol recovery and tower registration.",
            "培养塔协议已恢复，可继续进行协议恢复与塔型注册。");

        Register("恢复手册-分组-启动准备", "Starter Preparation", "启动准备");
        Register("恢复手册-分组-培养闭环", "Cultivation Loop", "培养闭环");
        Register("恢复手册-分组-协议接入", "Protocol Integration", "协议接入");

        Register("恢复手册步骤-启动套件", "Recover the starter kit", "回收启动套件");
        Register("恢复手册步骤-打开面板", "Open the data centre", "打开分馏数据中心");
        Register("恢复手册步骤-放置首塔", "Place the first Cultivation Tower", "放置第一个培养塔");
        Register("恢复手册步骤-左右成环", "Connect left and right ports into a loop", "左右口连接成环");
        Register("恢复手册步骤-临时箱子", "Send the front output to a temporary box", "正面输出先接临时箱子");
        Register("恢复手册步骤-输入原胚", "Feed common protos into the loop", "向环输入通用原胚");
        Register("恢复手册步骤-获得第二塔", "Produce and place the second Cultivation Tower", "获得并放置第二个培养塔");
        Register("恢复手册步骤-接入上传", "Remove the box and feed the second tower front port", "拆箱并接入第二塔正面");
        Register("恢复手册步骤-恢复协议", "Recover the cultivation tower protocol", "恢复培养塔协议");

        Register("恢复手册章节短-数据中心", "Data Centre", "数据中心");
        Register("恢复手册章节短-协议检索", "Protocol Retrieval", "协议检索");
        Register("恢复手册章节短-塔型注册", "Tower Registration", "塔型注册");
        Register("恢复手册章节短-物流交互", "Logistics Interaction", "物流交互");
        Register("恢复手册章节短-文明解析", "Civilization Analysis", "文明解析");
    }

    /// <summary>
    /// 恢复手册当前没有可配置项；保留入口以满足主面板页面合同。
    /// </summary>
    /// <param name="configFile">FE 配置文件，本页不读取。</param>
    public static void LoadConfig(ConfigFile configFile) { }

    /// <summary>
    /// 创建当前目标、后续章节、静态培养步骤和页脚四个区域。
    /// </summary>
    /// <param name="wnd">提供原版风格控件克隆能力的主窗口。</param>
    /// <param name="trans">承载恢复手册页的根节点。</param>
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
                            TextNode("", 15, Orange, wrap: true,
                                onBuilt: text => {
                                    currentHintText = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 0), objectName: "recovery-current-hint"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => {
                                    currentDetailText = text;
                                    text.supportRichText = true;
                                },
                                pos: (2, 0), objectName: "recovery-current-detail"),
                        ]),
                    ContentCard(pos: (1, 1), objectName: "recovery-chapters-card",
                        rows: BuildLaterChapterRows(),
                        rowGap: 4f,
                        children: BuildLaterChapterNodes()),
                    ContentCard(pos: (2, 0), span: (1, 2), objectName: "recovery-proto-card", strong: true,
                        rows: BuildMainRows(),
                        rowGap: 4f,
                        children: BuildMainChildren()),
                    FooterCard(pos: (3, 0), span: (1, 2), objectName: "recovery-footer-card",
                        children: [
                            TextNode("", 12, Gray, wrap: true,
                                onBuilt: text => {
                                    footerText = text;
                                    text.supportRichText = true;
                                },
                                pos: (0, 0), objectName: "recovery-footer-text"),
                        ]),
                ]));
        UpdateUI();
    }

    /// <summary>
    /// 仅刷新由真实游戏状态驱动的当前目标；静态分组步骤始终保持完整可见。
    /// </summary>
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        int milestone = CurrentMilestone();

        header.Title.text = "恢复手册".Translate().WithColor(Orange);
        header.Summary.text = "恢复手册页头摘要".Translate().WithColor(White);
        footerText.text = "恢复手册-页脚".Translate().WithColor(Gray);
        currentHintText.text = milestoneHintKeys[milestone].Translate();
        currentDetailText.text = milestoneDetailKeys[milestone].Translate();
    }

    /// <summary>
    /// 读取旧版 ManualStepsV1 块并忽略其内容；不保留任何手动完成状态。
    /// </summary>
    /// <param name="r">存档读取器。</param>
    public static void Import(BinaryReader r) {
        IntoOtherSave();
        r.ReadBlocks(
            (ManualStepsBlockTag, _ => { })
        );
    }

    /// <summary>
    /// 新导出不再写入 ManualStepsV1 块（写入空块列表），维持页面存档接口合同。
    /// </summary>
    /// <param name="w">存档写入器。</param>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    /// <summary>
    /// 切档时重置手册运行态；当前实现无任何可变手动状态，保留为空实现以维持页面接口合同。
    /// </summary>
    public static void IntoOtherSave() {
    }

    /// <summary>
    /// 构造后续章节的固定单行轨道，避免文本换行挤入相邻章节。
    /// </summary>
    /// <returns>标题行与五个短章节行的轨道。</returns>
    private static LayoutTrack[] BuildLaterChapterRows() {
        var rows = new LayoutTrack[laterChapterKeys.Length + 1];
        rows[0] = Px(28f);
        for (int i = 1; i < rows.Length; i++) {
            rows[i] = Px(24f);
        }
        return rows;
    }

    /// <summary>
    /// 构造后续章节标题和项目符号文本节点。
    /// </summary>
    /// <returns>与 <see cref="BuildLaterChapterRows"/> 一一对应的节点。</returns>
    private static LayoutNode[] BuildLaterChapterNodes() {
        var nodes = new LayoutNode[laterChapterKeys.Length + 1];
        nodes[0] = CardTitleNode("恢复手册-后续章节", pos: (0, 0), objectName: "recovery-chapters-title");
        for (int i = 0; i < laterChapterKeys.Length; i++) {
            string key = laterChapterKeys[i];
            nodes[i + 1] = TextNode("", 14, White, wrap: false,
                onBuilt: text => {
                    text.supportRichText = true;
                    text.text = $"• {key.Translate()}";
                },
                pos: (i + 1, 0), objectName: $"recovery-chapter-{i}");
        }
        return nodes;
    }

    /// <summary>
    /// 构造静态培养步骤卡的总标题、三组标题和九个项目符号轨道。
    /// </summary>
    /// <returns>总高 364px、可放入 381px 内容区的固定轨道。</returns>
    private static LayoutTrack[] BuildMainRows() {
        var rows = new LayoutTrack[bottomRowCount + 1];
        rows[0] = Px(28f);
        for (int i = 1; i < rows.Length; i++) {
            rows[i] = Px(24f);
        }
        return rows;
    }

    /// <summary>
    /// 构造始终可回看的三组静态培养步骤，并缩进项目符号形成原版目标式层级。
    /// </summary>
    /// <returns>与 <see cref="BuildMainRows"/> 一一对应的节点。</returns>
    private static LayoutNode[] BuildMainChildren() {
        var nodes = new LayoutNode[bottomRowCount + 1];
        nodes[0] = CardTitleNode("恢复手册-原胚培养", 16, pos: (0, 0),
            objectName: "recovery-proto-master-title");
        int index = 1;
        int row = 1;
        for (int g = 0; g < bottomGroups.Length; g++) {
            string groupKey = bottomGroups[g].groupKey;
            nodes[index] = CardTitleNode(groupKey, pos: (row, 0), objectName: $"recovery-proto-group-{g}");
            index++;
            row++;
            foreach (string bulletKey in bottomGroups[g].bulletKeys) {
                string key = bulletKey;
                nodes[index] = TextNode("", 14, White, wrap: false,
                    onBuilt: text => {
                        text.supportRichText = true;
                        text.text = $"• {key.Translate()}";
                    },
                    pos: (row, 0), margin: new LayoutInsets(14f, 0f, 0f, 0f),
                    objectName: $"recovery-proto-bullet-{row}");
                index++;
                row++;
            }
        }
        return nodes;
    }

    /// <summary>
    /// 计算下方主卡除总标题外的固定行数（每组 1 行组标题 + 其项目符号行数）。
    /// </summary>
    /// <returns>组标题与项目符号行数总和。</returns>
    private static int ComputeBottomRowCount() {
        int total = 0;
        foreach (var (_, bullets) in bottomGroups) {
            total += 1 + bullets.Length;
        }
        return total;
    }

    /// <summary>
    /// 根据可观测的真实游戏状态判定当前里程碑。
    /// </summary>
    /// <returns>0=未回收启动套件，1=已回收但培养塔协议未恢复，2=培养塔协议已恢复。</returns>
    private static int CurrentMilestone() {
        if (HasItemInteractionProtocol()) {
            return 2;
        }

        return HasStarterKit() ? 1 : 0;
    }

    /// <summary>
    /// 判断启动套件是否已通过通信状态或套件货物留下可观测证据。
    /// </summary>
    /// <returns>玩家已经越过启动套件回收里程碑时返回 true。</returns>
    private static bool HasStarterKit() {
        return CivilizationRecoveryManager.HasDataCenterCommunication
               || GetItemTotalCount(IFE培养塔) > 0
               || GetItemTotalCount(IFE通用原胚) > 0
               || DataCenterInventory.centerItemCount[IFE培养塔] > 0
               || DataCenterInventory.centerItemCount[IFE通用原胚] > 0;
    }

    /// <summary>
    /// 判断培养塔物品交互协议是否已由真实隐藏科技状态恢复。
    /// </summary>
    /// <returns>培养塔协议已恢复时返回 true。</returns>
    private static bool HasItemInteractionProtocol() {
        return GameMain.history != null && GameMain.history.TechUnlocked(TFE培养塔);
    }
}
