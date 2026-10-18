using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter.Assembly;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.ResourceInteraction;

/// <summary>仓储页的装配覆盖面板；草稿持有实际预留，关闭释放草稿，确认订单继续运行。</summary>
public sealed class AssemblyPanel : MonoBehaviour {
    private sealed class DemandRow {
        public RectTransform Root;
        public Text Name, Counts, RecipeLabel, StockLabel;
        public Button Recipe, Stock;
        public AssemblyDemand Demand;
    }
    private sealed class QueueRow {
        public RectTransform Root;
        public Text Name, State;
        public Button Cancel;
        public string Id;
    }
    private readonly List<DemandRow> demands = [];
    private readonly List<QueueRow> queue = [];
    private readonly Dictionary<string, AssemblyChoice> choices = [];
    private RectTransform tree, orders;
    private Text selected, hint;
    private Button confirm;
    private InputField amount;
    private int itemId = I电路板;
    private string draftId;
    private long revision;
    private float refreshAt, heartbeatAt;

    public static void Create(MyWindow window, RectTransform parent) {
        RectTransform root = PageLayout.CreateContentCard(parent, "assembly-panel", 0, 0, 1082, 767, true);
        root.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.085f, 1);
        root.GetComponent<Image>().raycastTarget = true;
        root.gameObject.SetActive(false);
        AssemblyPanel panel = root.gameObject.AddComponent<AssemblyPanel>();
        panel.Build(window, root);
        AddButton(parent, "FE.Assembly.Open".Translate(), 934, 24, 142, () => {
            root.SetAsLastSibling(); root.gameObject.SetActive(true); panel.Begin();
        });
    }

    private void Build(MyWindow window, RectTransform root) {
        Label(root, "FE.Assembly.Open".Translate(), 16, 10, 800, 32, 20);
        AddButton(root, "关闭".Translate(), 938, 10, 126, () => gameObject.SetActive(false));
        Button pick = AddButton(root, "", 16, 54, 340, () => UIItemPickerExtension.Popup(new Vector2(-420, 250), item => {
            if (item == null) { return; }
            itemId = item.ID; choices.Clear(); Change();
        }, true, item => item.ID > 0 && item.ID < 12000));
        selected = pick.GetComponentInChildren<Text>();
        Label(root, "FE.Assembly.Amount".Translate(), 374, 54, 100, 36);
        amount = window.AddInputField(480, 54, root, "100", objName: "assembly-amount", onEditEnd: _ => Change());
        NormalizeRectWithTopLeft(amount, 480, 54, root).sizeDelta = new Vector2(180, 36);
        amount.contentType = InputField.ContentType.IntegerNumber; amount.characterLimit = 7;
        confirm = AddButton(root, "FE.Assembly.Confirm".Translate(), 750, 54, 314, () => {
            if (draftId == null) { return; }
            AssemblyNetwork.Confirm(draftId, revision);
            draftId = null; refreshAt = 0;
        });
        hint = Label(root, "", 16, 96, 1048, 30, 13);
        tree = PageLayout.CreateScrollableArea(root, "assembly-tree", 16, 130, 1048, 300, 300);
        Label(root, "FE.Assembly.Queue".Translate(), 16, 442, 1048, 30, 18);
        orders = PageLayout.CreateScrollableArea(root, "assembly-queue", 16, 480, 1048, 268, 268);
    }

    private void Begin() { draftId = null; revision = 0; Change(); }
    private void Change() {
        if (!int.TryParse(amount.text, out int count) || count is < 1 or > 1000000) {
            confirm.interactable = false; hint.text = "FE.Assembly.InvalidAmount".Translate(); return;
        }
        draftId ??= Guid.NewGuid().ToString("N");
        AssemblyNetwork.Preview(draftId, ++revision, itemId, count, choices);
        refreshAt = 0;
    }
    private void OnDisable() { AssemblyNetwork.Cancel(draftId); draftId = null; }
    private void Update() {
        if (Time.unscaledTime >= heartbeatAt) { heartbeatAt = Time.unscaledTime + 10; AssemblyNetwork.Touch(draftId); }
        if (Time.unscaledTime < refreshAt) { return; }
        refreshAt = Time.unscaledTime + 0.3f;
        AssemblyOrderView[] all = AssemblyManager.GetOrders();
        AssemblyOrderView draft = all.FirstOrDefault(order => order.Id == draftId && order.Draft && order.Revision == revision);
        selected.text = LDB.items.Select(itemId)?.name ?? "?";
        bool validAmount = int.TryParse(amount.text, out int quantity) && quantity is >= 1 and <= 1000000;
        confirm.interactable = draft != null && draft.Status != AssemblyStatus.TooComplex && validAmount;
        hint.text = !validAmount ? "FE.Assembly.InvalidAmount".Translate()
            : draft?.Status == AssemblyStatus.TooComplex ? "FE.Assembly.TooComplex".Translate()
            : draftId != null && draft == null ? "FE.Assembly.Waiting".Translate() : "FE.Assembly.Help".Translate();
        RenderTree(draft?.Rows ?? []);
        RenderQueue(all.Where(order => !order.Draft).ToArray());
    }

    private void RenderTree(AssemblyDemand[] rows) {
        while (demands.Count < rows.Length) {
            var row = new DemandRow { Root = PageLayout.CreateContentCard(tree, "demand", 0, demands.Count * 58, 1028, 54) };
            row.Name = Label(row.Root, "", 8, 2, 330, 25);
            row.Counts = Label(row.Root, "", 8, 27, 690, 24, 12);
            row.Stock = AddButton(row.Root, "", 706, 9, 145, () => {
                AssemblyDemand demand = row.Demand;
                if (demand == null) { return; }
                choices[demand.Path] = new() { StockOnly = !demand.StockOnly, RecipeId = demand.RecipeId }; Change();
            });
            row.StockLabel = row.Stock.GetComponentInChildren<Text>();
            row.Recipe = AddButton(row.Root, "", 859, 9, 161, () => {
                AssemblyDemand demand = row.Demand;
                if (demand == null) { return; }
                RecipeProto[] candidates = AssemblyPlanner.GetCandidates(demand.ItemId);
                if (candidates.Length == 0) { return; }
                int index = Array.FindIndex(candidates, recipe => recipe.ID == demand.RecipeId);
                choices[demand.Path] = new() { RecipeId = candidates[(index + 1) % candidates.Length].ID }; Change();
            });
            row.RecipeLabel = row.Recipe.GetComponentInChildren<Text>(); demands.Add(row);
        }
        for (int i = 0; i < demands.Count; i++) {
            DemandRow row = demands[i]; row.Root.gameObject.SetActive(i < rows.Length);
            if (i >= rows.Length) { row.Demand = null; continue; }
            AssemblyDemand demand = rows[i]; row.Demand = demand;
            row.Name.text = new string(' ', Math.Min(20, demand.Depth * 2)) + (LDB.items.Select(demand.ItemId)?.name ?? "?");
            row.Counts.text = string.Format("FE.Assembly.Counts".Translate(), demand.Required, demand.Stock, demand.Planned, demand.Missing);
            row.Counts.color = demand.Missing > 0 ? new Color(1f, 0.45f, 0.4f) : Color.white;
            row.StockLabel.text = (demand.StockOnly ? "FE.Assembly.StockOnly" : "FE.Assembly.Auto").Translate();
            row.RecipeLabel.text = demand.RecipeId > 0 ? LDB.recipes.Select(demand.RecipeId)?.name ?? "?" : "FE.Assembly.NoRecipe".Translate();
            row.Recipe.interactable = AssemblyPlanner.GetCandidates(demand.ItemId).Length > 1;
        }
        tree.sizeDelta = new Vector2(0, Math.Max(300, rows.Length * 58));
    }

    private void RenderQueue(AssemblyOrderView[] views) {
        while (queue.Count < views.Length) {
            var row = new QueueRow { Root = PageLayout.CreateContentCard(orders, "order", 0, queue.Count * 64, 1028, 60) };
            row.Name = Label(row.Root, "", 10, 3, 720, 26);
            row.State = Label(row.Root, "", 10, 30, 850, 25, 13);
            row.Cancel = AddButton(row.Root, "取消".Translate(), 885, 12, 130, () => AssemblyNetwork.Cancel(row.Id));
            queue.Add(row);
        }
        for (int i = 0; i < queue.Count; i++) {
            QueueRow row = queue[i]; row.Root.gameObject.SetActive(i < views.Length);
            if (i >= views.Length) { row.Id = null; continue; }
            AssemblyOrderView order = views[i]; row.Id = order.Id;
            row.Name.text = string.Format("FE.Assembly.Order".Translate(), LDB.items.Select(order.ItemId)?.name ?? "?", order.Count - order.Remaining, order.Count);
            row.State.text = ("FE.Assembly.Status." + order.Status).Translate()
                + (order.Status == AssemblyStatus.Working ? $"  {order.WorkProgress:P0}" : "");
            row.Cancel.interactable = order.Owner == AssemblyNetwork.LocalOwner || !NebulaMultiplayerModAPI.IsClient;
        }
        orders.sizeDelta = new Vector2(0, Math.Max(268, views.Length * 64));
    }

    private static Text Label(RectTransform parent, string text, float x, float y, float width, float height, int size = 15) =>
        PageLayout.AddCenteredText(parent, text, size, Color.white, TextAnchor.MiddleLeft, x, y, width, height, "label", true);
    private static Button AddButton(RectTransform parent, string text, float x, float y, float width, Action action) {
        RectTransform rect = PageLayout.CreateContentCard(parent, "action", x, y, width, 36);
        Image image = rect.GetComponent<Image>(); image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        PageLayout.AddCenteredText(rect, text, 13, Color.white, TextAnchor.MiddleCenter, 4, 0, width - 8, 36, "label", true);
        return button;
    }
}
