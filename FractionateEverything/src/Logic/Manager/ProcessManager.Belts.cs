using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.View.ProgressTask;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class ProcessManager {
    private static ProductOutputInfo FindProduct(List<ProductOutputInfo> products, int itemId, bool mainOnly = false) {
        foreach (ProductOutputInfo product in products) {
            if (product.itemId != itemId) {
                continue;
            }
            if (mainOnly && !product.isMainOutput) {
                continue;
            }
            return product;
        }
        return null;
    }

    private static ProductOutputInfo SelectByNormalOutputPriority(ProductOutputInfo bestSideProduct,
        ProductOutputInfo bestMainProduct, int productStack) {
        ProductOutputInfo product = bestSideProduct;
        if (product == null || product.count < productStack) {
            if (bestMainProduct != null && (product == null || bestMainProduct.count > product.count)) {
                product = bestMainProduct;
            }
        }
        return product;
    }

    private static ProductOutputInfo SelectProductForBeltOutput(List<ProductOutputInfo> products, int productStack,
        int lockedOutputId, out bool flushNonLockedProduct) {
        ProductOutputInfo bestSideProduct = null;
        ProductOutputInfo bestMainProduct = null;
        ProductOutputInfo bestNonLockedSideProduct = null;
        ProductOutputInfo bestNonLockedMainProduct = null;
        foreach (ProductOutputInfo p in products) {
            if (p.count <= 0) {
                continue;
            }
            if (p.isMainOutput) {
                if (bestMainProduct == null || p.count > bestMainProduct.count) {
                    bestMainProduct = p;
                }
                if (lockedOutputId != 0
                    && p.itemId != lockedOutputId
                    && (bestNonLockedMainProduct == null || p.count > bestNonLockedMainProduct.count)) {
                    bestNonLockedMainProduct = p;
                }
            } else {
                if (bestSideProduct == null || p.count > bestSideProduct.count) {
                    bestSideProduct = p;
                }
                if (lockedOutputId != 0
                    && p.itemId != lockedOutputId
                    && (bestNonLockedSideProduct == null || p.count > bestNonLockedSideProduct.count)) {
                    bestNonLockedSideProduct = p;
                }
            }
        }

        ProductOutputInfo nonLockedProduct = SelectByNormalOutputPriority(bestNonLockedSideProduct,
            bestNonLockedMainProduct, productStack);
        if (nonLockedProduct != null) {
            flushNonLockedProduct = true;
            return nonLockedProduct;
        }

        flushNonLockedProduct = false;
        return SelectByNormalOutputPriority(bestSideProduct, bestMainProduct, productStack);
    }

    private static bool MatchesRecipeOutputs(List<ProductOutputInfo> products, BaseRecipe recipe) {
        int expectedCount = recipe.OutputMain.Count + recipe.OutputAppend.Count;
        if (products.Count != expectedCount) {
            return false;
        }

        int productIndex = 0;
        for (int i = 0; i < recipe.OutputMain.Count; i++, productIndex++) {
            ProductOutputInfo product = products[productIndex];
            if (!product.isMainOutput || product.itemId != recipe.OutputMain[i].OutputID) {
                return false;
            }
        }

        for (int i = 0; i < recipe.OutputAppend.Count; i++, productIndex++) {
            ProductOutputInfo product = products[productIndex];
            if (product.isMainOutput || product.itemId != recipe.OutputAppend[i].OutputID) {
                return false;
            }
        }

        return true;
    }

    private static void NotifyProductCountIncreased(BuildingManager.FractionatorExtraState extraState,
        int productCount, int productOutputMax, ref bool hasFullProduct) {

        extraState.InvalidateFullProductCache();
        if (productCount >= productOutputMax) {
            hasFullProduct = true;
            extraState.MarkFullProductCache(productOutputMax);
        }
    }

    private static bool AreAllProductsEmpty(List<ProductOutputInfo> products) {
        foreach (ProductOutputInfo product in products) {
            if (product.count > 0) {
                return false;
            }
        }
        return true;
    }

    private static int GetFluidOutputStackToMove(FractionatorComponent fractionator, int preferredStack) {
        if (fractionator.fluidOutputCount >= preferredStack) {
            return preferredStack;
        }
        // 输入已空时释放不足一组的尾料，避免旧 fluidId 被残留流动输出卡住。
        return fractionator.fluidInputCount == 0 ? fractionator.fluidOutputCount : 0;
    }

    private static int GetFluidOutputIncAvg(FractionatorComponent fractionator, int buildingID, int outputStack) {
        if (outputStack <= 0 || fractionator.fluidOutputCount <= 0) {
            return 0;
        }
        if (buildingID == IFE点数聚集塔) {
            return fractionator.fluidOutputInc >= 4 * outputStack ? 4 : 0;
        }
        return fractionator.fluidOutputInc / fractionator.fluidOutputCount;
    }

    private static void RemoveFluidOutput(ref FractionatorComponent fractionator, int outputStack, int incAvg) {
        fractionator.fluidOutputCount -= outputStack;
        fractionator.fluidOutputInc -= incAvg * outputStack;
        if (fractionator.fluidOutputCount <= 0) {
            fractionator.fluidOutputCount = 0;
            fractionator.fluidOutputInc = 0;
        } else if (fractionator.fluidOutputInc < 0) {
            fractionator.fluidOutputInc = 0;
        }
    }

    private static void TryOutputFluidToBelt(ref FractionatorComponent fractionator, int buildingID,
        bool enableFluidEnhancement, int fluidStack, CargoTraffic cargoTraffic, int beltId,
        float fluidInputCountPerCargo) {
        if (beltId <= 0 || fractionator.fluidOutputCount <= 0) {
            return;
        }

        if (enableFluidEnhancement) {
            for (int i = 0; i < MaxOutputTimes && fractionator.fluidOutputCount > 0; i++) {
                int outputStack = GetFluidOutputStackToMove(fractionator, fluidStack);
                if (outputStack <= 0) {
                    break;
                }
                int fluidOutputIncAvg = GetFluidOutputIncAvg(fractionator, buildingID, outputStack);
                if (!cargoTraffic.TryInsertItemAtHead(beltId, fractionator.fluidId, (byte)outputStack,
                        (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                    break;
                }
                RemoveFluidOutput(ref fractionator, outputStack, fluidOutputIncAvg);
            }
            return;
        }

        CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        if (cargoPath == null) {
            return;
        }
        int preferredStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));
        for (int i = 0; i < MaxOutputTimes && fractionator.fluidOutputCount > 0; i++) {
            int outputStack = GetFluidOutputStackToMove(fractionator, preferredStack);
            if (outputStack <= 0) {
                break;
            }
            int fluidOutputIncAvg = GetFluidOutputIncAvg(fractionator, buildingID, outputStack);
            if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fractionator.fluidId,
                    Mathf.CeilToInt((float)(fluidInputCountPerCargo / outputStack - 0.1)),
                    (byte)outputStack,
                    (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                break;
            }
            RemoveFluidOutput(ref fractionator, outputStack, fluidOutputIncAvg);
        }
    }}
