using System;
using FE.Logic.Fractionation.State;
using System.Collections.Generic;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using HarmonyLib;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.Logic.Fractionation.Presentation;
/// <summary>
/// FractionatorWindow 类型。
/// </summary>
public static partial class FractionatorWindow {
    // ===== OnProductUIButtonClick 拦截 =====

    private static ProductOutputInfo FindProductByItemId(List<ProductOutputInfo> products, int itemId) {
        if (products == null) {
            return null;
        }
        for (int i = 0; i < products.Count; i++) {
            ProductOutputInfo product = products[i];
            if (product.itemId == itemId) {
                return product;
            }
        }
        return null;
    }

    private static ProductOutputInfo FindProductBySlot(List<ProductOutputInfo> products, ProductSlot slot,
        int itemId) {
        if (products == null || slot == null || slot.kind == ProductSlotKind.Fluid) {
            return null;
        }
        bool isMainOutput = slot.kind == ProductSlotKind.Main;
        for (int i = 0; i < products.Count; i++) {
            ProductOutputInfo product = products[i];
            if (product.itemId == itemId && product.isMainOutput == isMainOutput) {
                return product;
            }
        }
        return null;
    }

    private static bool IsFluidSlot(FractionatorComponent fractionator, ProductSlot slot, int itemId) {
        return slot != null ? slot.kind == ProductSlotKind.Fluid : itemId == fractionator.fluidId;
    }

    private static int GetModSlotCount(FractionatorComponent fractionator, List<ProductOutputInfo> products,
        int itemId, ProductSlot slot = null) {
        if (slot != null) {
            if (slot.kind == ProductSlotKind.Fluid) {
                return itemId == fractionator.fluidId ? fractionator.fluidOutputCount : 0;
            }
            return FindProductBySlot(products, slot, itemId)?.count ?? 0;
        }
        if (itemId == fractionator.productId) {
            return fractionator.productOutputCount;
        }
        if (itemId == fractionator.fluidId) {
            return fractionator.fluidOutputCount;
        }
        ProductOutputInfo product = FindProductByItemId(products, itemId);
        return product?.count ?? 0;
    }

    private static void SetModSlotCount(FractionatorComponent fractionator, List<ProductOutputInfo> products,
        int itemId,
        int count, ProductSlot slot = null) {
        if (slot != null) {
            if (slot.kind == ProductSlotKind.Fluid) {
                if (itemId == fractionator.fluidId) {
                    fractionator.fluidOutputCount = count;
                    if (count <= 0) {
                        fractionator.fluidOutputInc = 0;
                    }
                }
                return;
            }
            ProductOutputInfo slotProduct = FindProductBySlot(products, slot, itemId);
            if (slotProduct != null) {
                slotProduct.count = count;
                if (slotProduct.itemId == fractionator.productId && slotProduct.isMainOutput) {
                    fractionator.productOutputCount = count;
                }
            }
            return;
        }
        if (itemId == fractionator.productId) {
            fractionator.productOutputCount = count;
        }
        if (itemId == fractionator.fluidId) {
            fractionator.fluidOutputCount = count;
            if (count <= 0) {
                fractionator.fluidOutputInc = 0;
            }
        } else {
            ProductOutputInfo product = FindProductByItemId(products, itemId);
            if (product != null) {
                product.count = count;
            }
        }
    }

    private static int GetModSlotMax(FractionatorComponent fractionator, int itemId, ProductSlot slot = null) {
        return IsFluidSlot(fractionator, slot, itemId) ? fractionator.fluidOutputMax : fractionator.productOutputMax;
    }

    private static bool BlockSingleLockManualInsert(UIFractionatorWindow window,
        FractionatorComponent fractionator) {
        if (window?.factory == null || !ConversionTower.EnableSingleLock) {
            return false;
        }

        int buildingId = window.factory.entityPool[fractionator.entityId].protoId;
        return buildingId == IFE转化塔 && fractionator.GetNormalizedLockedOutput(window.factory) != 0;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnProductUIButtonClick))]
    public static bool OnProductUIButtonClick_Prefix(UIFractionatorWindow __instance, int itemId) {
        return HandleProductSlotClick(__instance, itemId, null);
    }

    private static bool HandleProductSlotClick(UIFractionatorWindow __instance, int itemId, ProductSlot slot) {
        if (__instance.fractionatorId == 0 || __instance.factory == null || __instance.player == null) {
            return true;
        }
        FractionatorComponent fractionator = __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId
            || !IsModFractionator(__instance.fractionatorId, __instance.factory)) {
            return true;
        }

        // 额外主产物/副产物复用了原版回调；如果不在这里完全接管，
        // 原版会把“非第一主产物”错误走到流体输出分支，导致错取和无限取。
        // 自定义槽位需要额外带上槽位身份，避免单锁产物只靠 itemId 反查到错误缓存。
        Player player = __instance.player;
        List<ProductOutputInfo> products = fractionator.products(__instance.factory);
        int currentCount = GetModSlotCount(fractionator, products, itemId, slot);
        bool isFluidSlot = IsFluidSlot(fractionator, slot, itemId);

        if (player.inhandItemId > 0 && player.inhandItemCount == 0) {
            player.SetHandItems(0, 0);
            return false;
        }

        if (player.inhandItemId > 0 && player.inhandItemCount > 0) {
            // 单锁后的输出槽只允许取出/清空，不再作为手动塞回缓存入口。
            if (BlockSingleLockManualInsert(__instance, fractionator)) {
                return false;
            }
            if (player.inhandItemId != itemId) {
                return false;
            }
            int canAdd = GetModSlotMax(fractionator, itemId, slot) - currentCount;
            if (canAdd < 0) {
                canAdd = 0;
            }
            int add = Math.Min(player.inhandItemCount, canAdd);
            if (add <= 0) {
                UIRealtimeTip.Popup("栏位已满".Translate());
                return false;
            }
            int handCount = player.inhandItemCount;
            int handInc = player.inhandItemInc;
            int takeInc = isFluidSlot ? split_inc(ref handCount, ref handInc, add) : 0;

            SetModSlotCount(fractionator, products, itemId, currentCount + add, slot);
            fractionator.GetExtraState(__instance.factory).InvalidateFullProductCache();
            player.AddHandItemCount_Unsafe(-add);
            if (isFluidSlot) {
                player.SetHandItemInc_Unsafe(player.inhandItemInc - takeInc);
                fractionator.fluidOutputInc += takeInc;
            }
            if (player.inhandItemCount <= 0) {
                player.SetHandItemId_Unsafe(0);
                player.SetHandItemCount_Unsafe(0);
                player.SetHandItemInc_Unsafe(0);
            }
            return false;
        }

        if (player.inhandItemId != 0 || player.inhandItemCount != 0 || currentCount == 0) {
            return false;
        }

        int currentInc = isFluidSlot ? fractionator.fluidOutputInc : 0;
        if (VFInput.control || VFInput.shift) {
            int added = player.TryAddItemToPackage(itemId, currentCount, currentInc, throwTrash: false);
            if (added > 0) UIItemup.Up(itemId, added);
        } else {
            player.SetHandItemId_Unsafe(itemId);
            player.SetHandItemCount_Unsafe(currentCount);
            player.SetHandItemInc_Unsafe(currentInc);
        }
        SetModSlotCount(fractionator, products, itemId, 0, slot);
        fractionator.GetExtraState(__instance.factory).InvalidateFullProductCache();
        if (isFluidSlot) {
            fractionator.fluidOutputInc = 0;
        }
        return false;
    }
}
