using System.Collections.Generic;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.UI.Foundation;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 以四条并列纵向主干展示塔型科技的固定前置关系。
/// </summary>
public static class AncientTechTreePage {
    private const int TowerCount = 4;
    private const int NodesPerTower = 5;

    private sealed class NodeRefs {
        public RectTransform Card;
        public Text Detail;
        public UIButton Purchase;
        public Image IncomingLine;
    }

    private static readonly string[] TowerNames = ["交互塔", "资源塔", "转化塔", "解析塔"];
    private static readonly NodeRefs[] nodeRefs = CreateNodeRefs();
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text footerText;

    public static void AddTranslations() {
        Register("远古科技树", "Ancient Technology Tree");
        Register("远古科技树摘要", "Spend one shared point type; branch choice determines which tower improves first.",
            "只使用一种科技点；分支选择决定哪一种塔优先获得产线结构能力。");
        Register("交互塔", "Interaction Tower");
        Register("资源塔", "Resource Tower");
        Register("转化塔", "Conversion Tower");
        Register("解析塔", "Analysis Tower");
        AddTowerTranslations("交互塔", "Interaction Tower");
        AddTowerTranslations("资源塔", "Resource Tower");
        AddTowerTranslations("转化塔", "Conversion Tower");
        AddTowerTranslations("解析塔", "Analysis Tower");
        Register("购买节点", "Purchase");
        Register("节点已解锁", "Unlocked");
        Register("科技点不足", "Insufficient points");
        Register("前置节点未解锁", "Prerequisite locked");
        Register("文明阶段未完成", "Complete a civilization stage first");
        Register("运行能力尚未接入", "Runtime support pending");
        Register("可用科技点", "Available Points");
        Register("累计获得", "Total Earned");
        Register("累计投入", "Total Spent");
        Register("流动输出堆叠效果", "Side output leaves in full stacks up to the current tower limit.",
            "流动物品按当前塔的堆叠上限整组输出。");
        Register("产物输出堆叠效果", "Products leave in full stacks up to the current tower limit.",
            "主产物与副产物按当前塔的堆叠上限整组输出。");
        Register("分馏永动效果", "Fractionation continues while a product cache is blocked.",
            "某一路产物缓存阻塞时仍可继续处理流动输入。");
        Register("主路锁定效果", "Select one main output for recipes with multiple main products.",
            "多主产物配方可在单座塔上选择固定主产物。");
        Register("副产物弃置效果", "Disable selected byproducts on an individual tower.",
            "单座塔可关闭不需要的副产物输出。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(rows: [Px(PageLayout.HeaderHeight), 1, Px(PageLayout.FooterHeight)], rowGap: PageLayout.Gap,
                children: [
                    Header("远古科技树", "远古科技树摘要", pos: (0, 0), objectName: "ancient-tech-header",
                        onBuilt: refs => header = refs),
                    Grid(pos: (1, 0), cols: [1, 1, 1, 1], columnGap: 12f,
                        children: BuildTowerPaths(), objectName: "ancient-tech-paths"),
                    FooterCard(pos: (2, 0), objectName: "ancient-tech-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                            onBuilt: text => footerText = text, objectName: "ancient-tech-footer-text"),
                    ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "远古科技树".Translate().WithColor(Orange);
        header.Summary.text = "远古科技树摘要".Translate().WithColor(White);
        int nodeCount = AncientTechTreeCatalog.All.Count;
        for (int i = 0; i < nodeRefs.Length && i < nodeCount; i++) {
            AncientTechNodeDefinition node = AncientTechTreeCatalog.All[i];
            bool unlocked = AncientTechTreeState.GetLevel(node.NodeKey) > 0;
            bool canPurchase = AncientTechTreeService.CanPurchase(node.NodeKey);
            nodeRefs[i].Detail.text =
                $"{node.DisplayNameKey.Translate().WithColor(Orange)}\n{GetEffectDescription(node.EffectType).Translate()}";
            SetPurchaseState(nodeRefs[i].Purchase, node, unlocked, canPurchase);
            PageLayout.SetCardState(nodeRefs[i].Card,
                unlocked ? CardVisualState.Selected : canPurchase ? CardVisualState.Strong : CardVisualState.Normal);
            if (nodeRefs[i].IncomingLine != null) {
                bool prerequisiteUnlocked = node.PrerequisiteNodeKey != null
                                            && AncientTechTreeState.GetLevel(node.PrerequisiteNodeKey) > 0;
                nodeRefs[i].IncomingLine.color = prerequisiteUnlocked
                    ? PageLayout.StrongAccentColor
                    : PageLayout.CardBorderColor;
            }
        }

        footerText.text =
            $"{"可用科技点".Translate()}：{AncientTechTreeState.AvailablePoints}  "
            + $"{"累计获得".Translate()}：{AncientTechTreeState.TotalPointsEarned}  "
            + $"{"累计投入".Translate()}：{AncientTechTreeState.TotalPointsSpent}";
    }

    private static IReadOnlyList<LayoutNode> BuildTowerPaths() {
        var paths = new List<LayoutNode>(TowerCount);
        for (int towerIndex = 0; towerIndex < TowerCount; towerIndex++) {
            int index = towerIndex;
            paths.Add(Grid(pos: (0, towerIndex), rows: BuildPathRows(),
                children: BuildPathNodes(index), objectName: $"ancient-tech-path-{towerIndex}"));
        }
        return paths;
    }

    private static IReadOnlyList<LayoutTrack> BuildPathRows() {
        var rows = new List<LayoutTrack> { Px(30f) };
        for (int i = 0; i < NodesPerTower; i++) {
            rows.Add(Px(94f));
            if (i < NodesPerTower - 1) {
                rows.Add(Px(14f));
            }
        }
        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildPathNodes(int towerIndex) {
        var nodes = new List<LayoutNode> {
            TextNode(TowerNames[towerIndex], 16, Orange, TextAnchor.MiddleCenter,
                pos: (0, 0), objectName: $"ancient-tech-tower-title-{towerIndex}"),
        };
        for (int localIndex = 0; localIndex < NodesPerTower; localIndex++) {
            int globalIndex = towerIndex * NodesPerTower + localIndex;
            int row = 1 + localIndex * 2;
            nodes.Add(ContentCard(pos: (row, 0), objectName: $"ancient-tech-node-{globalIndex}",
                rows: [1, Px(28f)], rowGap: 4f, padding: Inset(8f),
                onBuilt: card => nodeRefs[globalIndex].Card = card,
                children: [
                    TextNode("", 11, White, TextAnchor.UpperLeft, wrap: true, pos: (0, 0),
                        onBuilt: text => {
                            nodeRefs[globalIndex].Detail = text;
                            text.supportRichText = true;
                        }, objectName: $"ancient-tech-node-detail-{globalIndex}"),
                    ButtonNode("购买节点", () => Purchase(globalIndex), fontSize: 11,
                        onBuilt: button => nodeRefs[globalIndex].Purchase = button,
                        pos: (1, 0), objectName: $"ancient-tech-node-purchase-{globalIndex}"),
                ]));
            if (localIndex < NodesPerTower - 1) {
                int nextGlobalIndex = globalIndex + 1;
                nodes.Add(Node(pos: (row + 1, 0), objectName: $"ancient-tech-line-{globalIndex}",
                    build: (_, root) => nodeRefs[nextGlobalIndex].IncomingLine = CreateConnector(root,
                        $"ancient-tech-line-image-{globalIndex}")));
            }
        }
        return nodes;
    }

    private static Image CreateConnector(RectTransform parent, string objectName) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(3f, 0f);
        Image image = obj.GetComponent<Image>();
        image.color = PageLayout.CardBorderColor;
        image.raycastTarget = false;
        return image;
    }

    private static void SetPurchaseState(UIButton button, AncientTechNodeDefinition node, bool unlocked,
        bool canPurchase) {
        string text;
        if (unlocked) {
            text = "节点已解锁".Translate();
        } else if (!node.RuntimeImplemented) {
            text = "运行能力尚未接入".Translate();
        } else if (ProtocolCatalog.GetCompletedStageCount() <= 0) {
            text = "文明阶段未完成".Translate();
        } else if (node.PrerequisiteNodeKey != null
                   && AncientTechTreeState.GetLevel(node.PrerequisiteNodeKey) <= 0) {
            text = "前置节点未解锁".Translate();
        } else if (AncientTechTreeState.AvailablePoints < node.Cost) {
            text = "科技点不足".Translate();
        } else {
            text = $"{"购买节点".Translate()} ({node.Cost})";
        }
        button.SetText(text);
        button.button.interactable = canPurchase;
    }

    private static string GetEffectDescription(AncientTechEffectType effectType) {
        return effectType switch {
            AncientTechEffectType.FluidOutputStacking => "流动输出堆叠效果",
            AncientTechEffectType.ProductOutputStacking => "产物输出堆叠效果",
            AncientTechEffectType.FractionationForever => "分馏永动效果",
            AncientTechEffectType.MainOutputLock => "主路锁定效果",
            AncientTechEffectType.ByproductDiscard => "副产物弃置效果",
            _ => string.Empty,
        };
    }

    private static void Purchase(int index) {
        if (index >= 0 && index < AncientTechTreeCatalog.All.Count) {
            AncientTechTreeService.TryPurchase(AncientTechTreeCatalog.All[index].NodeKey);
        }
        UpdateUI();
    }

    private static NodeRefs[] CreateNodeRefs() {
        var refs = new NodeRefs[TowerCount * NodesPerTower];
        for (int i = 0; i < refs.Length; i++) {
            refs[i] = new NodeRefs();
        }
        return refs;
    }

    private static void AddTowerTranslations(string towerName, string towerNameEn) {
        Register($"远古科技-{towerName}流动输出堆叠", $"{towerNameEn}: Fluid Output Stacking");
        Register($"远古科技-{towerName}产物输出堆叠", $"{towerNameEn}: Product Output Stacking");
        Register($"远古科技-{towerName}分馏永动", $"{towerNameEn}: Continuous Fractionation");
        Register($"远古科技-{towerName}主路锁定", $"{towerNameEn}: Main Output Lock");
        Register($"远古科技-{towerName}副产物弃置", $"{towerNameEn}: Byproduct Discard");
    }
}
