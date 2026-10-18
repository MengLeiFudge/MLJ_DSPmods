using System.Runtime.CompilerServices;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Monument;
using FE.UI.MainPanel.Theme;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Patches.Common;

/// <summary>射线接收窗口底部的独立模式按钮；只延伸射线窗口，不覆盖原有发电/光子操作。</summary>
internal static class MonumentReceiverWindow {
    private sealed class Controls { internal RectTransform Root; internal Text Label; internal Button Button; }
    private static readonly ConditionalWeakTable<UIPowerGeneratorWindow, Controls> Views = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIPowerGeneratorWindow), nameof(UIPowerGeneratorWindow._OnUpdate))]
    private static void UIPowerGeneratorWindow_OnUpdate_Postfix(UIPowerGeneratorWindow __instance) {
        UIPowerGeneratorWindow window = __instance;
        bool gamma = window.factory != null && window.generatorId > 0 && window.generatorId < window.powerSystem.genCursor
            && window.powerSystem.genPool[window.generatorId].id == window.generatorId && window.powerSystem.genPool[window.generatorId].gamma;
        if (!gamma) { if (Views.TryGetValue(window, out Controls hidden)) { hidden.Root.gameObject.SetActive(false); } return; }
        Controls view = Views.GetValue(window, Create);
        view.Root.gameObject.SetActive(true);
        ref PowerGeneratorComponent receiver = ref window.powerSystem.genPool[window.generatorId];
        bool selected = receiver.productId == IFE丰碑结晶;
        view.Label.text = (selected ? "FE.Monument.StopReceiving" : "FE.Monument.Receive").Translate();
        view.Button.interactable = receiver.productCount < 1f;
        view.Root.anchoredPosition = new Vector2(18, -window.defaultHeight - 4);
        view.Root.sizeDelta = new Vector2(window.rectTransform.sizeDelta.x - 36, 32);
        window.rectTransform.sizeDelta = new Vector2(window.rectTransform.sizeDelta.x, window.defaultHeight + 44);
        if (selected) {
            window.productSpeedText.text = string.Format("FE.Monument.PerMinute".Translate(), MonumentReceivers.GetRate(window.factory.planetId, receiver.entityId));
            window.gammaReqText.text = "—";
        }
    }

    private static Controls Create(UIPowerGeneratorWindow window) {
        RectTransform root = PageLayout.CreateContentCard(window.rectTransform, "fe-monument-receiver", 18, window.defaultHeight + 4,
            window.rectTransform.sizeDelta.x - 36, 32);
        Image image = root.GetComponent<Image>(); image.raycastTarget = true;
        Button button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text label = PageLayout.AddCenteredText(root, "", 14, Color.white, TextAnchor.MiddleCenter, 0, 0,
            root.sizeDelta.x, 32, "label", true);
        button.onClick.AddListener(() => {
            if (window.factory == null || window.generatorId <= 0 || window.generatorId >= window.powerSystem.genCursor) { return; }
            ref PowerGeneratorComponent receiver = ref window.powerSystem.genPool[window.generatorId];
            if (receiver.id != window.generatorId || !receiver.gamma) { return; }
            MonumentNetwork.RequestReceiver(window.factory, receiver.entityId, receiver.productId != IFE丰碑结晶);
        });
        return new Controls { Root = root, Label = label, Button = button };
    }
}
