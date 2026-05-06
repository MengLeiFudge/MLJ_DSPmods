using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Fractionation.Presentation;

/// <summary>
/// 分馏塔窗口打开、关闭和生命周期接管逻辑。
/// </summary>
public static partial class FractionatorWindow {
    // ===== _OnOpen Postfix：让原版正常执行，然后切换显示 =====
    // 关键原理：
    //   ManualBehaviour._Open() 先设 active=true，再调用 _OnOpen()。
    //   让原版 _OnOpen 执行，它会正确设置 factory/player 等所有字段。
    //   执行完后，在 Postfix 里隐藏 originalWindow（但保持 active=true），
    //   并设置 unsafeGameObjectState=true，让游戏的 _UpdateArray 继续驱动其 _Update，
    //   从而触发我们的 _OnUpdate Prefix，进而更新 modWindow。

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnOpen))]
    public static void OnWindowOpen(UIFractionatorWindow __instance) {
        if (__instance == modWindow) return;// modWindow 自己不处理

        RectTransform sourceRect = __instance.GetComponent<RectTransform>();

        // factory 由原版 _OnOpen 设置
        PlanetFactory factory = __instance.factory;
        if (!IsModFractionator(__instance.fractionatorId, factory)) {
            if (modWindowGo != null && modWindowGo.activeSelf && sourceRect != null && modRootRect != null) {
                AlignTopLeft(sourceRect, modRootRect);
            }

            UnbindSlotClickHandlers();
            if (modWindowGo != null && modWindowGo.activeSelf) {
                modWindowGo.SetActive(false);
            }
            if (modWindow != null) modWindow.active = false;
            __instance.unsafeGameObjectState = false;
            if (!__instance.gameObject.activeSelf) {
                __instance.gameObject.SetActive(true);
            }
            sourceWindow = null;
            return;
        }

        // 模组建筑：
        if (sourceRect != null && modRootRect != null) {
            AlignTopLeft(modRootRect, sourceRect);
        }

        // 1. 隐藏 originalWindow 但保持 active=true，让游戏继续驱动其 _Update
        __instance.gameObject.SetActive(false);
        __instance.unsafeGameObjectState = true;
        sourceWindow = __instance;

        // 2. 显示 modWindow
        modWindowGo.SetActive(true);
        modWindow.active = true;

        // 3. 注册自定义槽位事件
        BindSlotClickHandlers();
    }

    // ===== _OnClose Prefix：清理自定义状态，让原版正常清理字段 =====

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnClose))]
    public static bool OnWindowClose(UIFractionatorWindow __instance) {
        // modWindow 不应通过 _Close() 关闭（只用 SetActive 管理），
        // 但如果发生了也要避免 player NPE
        if (__instance == modWindow) {
            UnbindSlotClickHandlers();
            if (modWindowGo != null && modWindowGo.activeSelf) {
                modWindowGo.SetActive(false);
            }
            modWindow.active = false;

            UIFractionatorWindow src = sourceWindow;
            sourceWindow = null;
            if (src != null && src.active) {
                src._Close();
            }
            return false;
        }

        // 原版窗口关闭时，同步清理 modWindow
        UnbindSlotClickHandlers();
        if (modWindowGo != null && modWindowGo.activeSelf) {
            modWindowGo.SetActive(false);
        }
        if (modWindow != null) modWindow.active = false;
        __instance.unsafeGameObjectState = false;
        if (sourceWindow == __instance) sourceWindow = null;

        return true;// 让原版 _OnClose 正常执行，清理 factory/player/button 等
    }

    // ===== _OnUpdate Prefix：拦截原版调用，驱动 modWindow 更新 =====
    // 关键原理：
    //   原版 originalWindow 的 active=true，游戏继续调用 originalWindow._Update()，
    //   触发 originalWindow._OnUpdate()，我们在 Prefix 里拦截，执行 modWindow 更新，
    //   return false 跳过原版的显示逻辑（避免原版代码重新显示 oriProductBox/productBox 等）。

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static bool OnWindowUpdate(UIFractionatorWindow __instance) {
        if (__instance == modWindow) return false;// 防御：modWindow 不被游戏驱动

        if (modWindowGo == null) return true;

        RectTransform sourceRect = __instance.GetComponent<RectTransform>();

        bool isModBuilding = IsModFractionator(__instance.fractionatorId, __instance.factory);
        if (isModBuilding) {
            bool enteringModView = !modWindowGo.activeSelf;
            if (sourceRect != null && modRootRect != null) {
                if (enteringModView) {
                    AlignTopLeft(modRootRect, sourceRect);
                } else {
                    AlignTopLeft(sourceRect, modRootRect);
                }
            }
            sourceWindow = __instance;
            __instance.unsafeGameObjectState = true;
            if (__instance.gameObject.activeSelf) {
                __instance.gameObject.SetActive(false);
            }
            if (!modWindowGo.activeSelf) {
                modWindowGo.SetActive(true);
            }
            modWindow.active = true;
            BindSlotClickHandlers();
            DoModWindowUpdate(__instance);
            return false;
        }

        if (modWindowGo.activeSelf) {
            if (sourceRect != null && modRootRect != null) {
                AlignTopLeft(sourceRect, modRootRect);
            }
            modWindowGo.SetActive(false);
        }
        if (modWindow != null) modWindow.active = false;
        UnbindSlotClickHandlers();
        __instance.unsafeGameObjectState = false;
        if (__instance.active && !__instance.gameObject.activeSelf) {
            __instance.gameObject.SetActive(true);
        }
        sourceWindow = null;
        return true;
    }
}
