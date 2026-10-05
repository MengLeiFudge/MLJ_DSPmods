using System;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>原版分馏塔逐帧更新后的输出补放，复用全局堆叠配置。</summary>
public static partial class ProcessManager {
    /// <summary>
    /// 按原版规则计算原版分馏塔流动输出的堆叠：输入货物的平均堆叠，向上取整。
    /// </summary>
    /// <param name="fractionator">原版更新前的分馏塔状态；原版也在处理前取这个值。</param>
    /// <returns>流动输出单个货物允许的最大件数，至少为 1。</returns>
    private static int GetVanillaFluidOutputStack(in FractionatorComponent fractionator) {
        double averageStack = fractionator.fluidInputCount == 0
            ? 1.0
            : fractionator.fluidInputCargoCount > 0.0001f
                ? fractionator.fluidInputCount / fractionator.fluidInputCargoCount
                : 4.0;
        return Math.Max(1, Mathf.CeilToInt((float)(averageStack - 0.1)));
    }

    /// <summary>
    /// 原版分馏塔更新后，把流动输出和产物继续叠到输出带头部的货物上。
    /// </summary>
    /// <remarks>
    /// <para>原版每帧每条流动输出带最多放 2 件、产物每帧放 1 件且不叠堆，高堆叠满带时会让输出缓存堆满而停机。</para>
    /// <para>流动输出沿用原版堆叠规则，产物堆叠等于当前全局最大堆叠。只填充带头货物，不等待凑满；带子堵住时带头货物停留会被一直填到满堆。</para>
    /// </remarks>
    /// <param name="fractionator">原版分馏塔组件。</param>
    /// <param name="cargoTraffic">所属星球的传送带系统。</param>
    /// <param name="fluidOutputStack">流动输出单个货物的最大件数。</param>
    /// <returns>补放后重新计算的工作状态码。</returns>
    private static uint TopUpVanillaOutputs(ref FractionatorComponent fractionator, CargoTraffic cargoTraffic,
        int fluidOutputStack) {
        if (fractionator.fluidId > 0) {
            if (fractionator.isOutput1) {
                TopUpVanillaFluidOutput(ref fractionator, cargoTraffic, fractionator.belt1, fluidOutputStack);
            }
            if (fractionator.isOutput2) {
                TopUpVanillaFluidOutput(ref fractionator, cargoTraffic, fractionator.belt2, fluidOutputStack);
            }
        }
        if (fractionator.belt0 > 0 && fractionator.isOutput0 && fractionator.productOutputCount > 0) {
            CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[fractionator.belt0].segPathId);
            fractionator.productOutputCount -= TryStackAtBeltHead(path, fractionator.productId, globalMaxStack,
                fractionator.productOutputCount, 0, out _);
        }

        // 补放后计数可能变化，按原版规则重新判断清空与工作状态。
        if (fractionator.fluidInputCount == 0 && fractionator.fluidOutputCount == 0
            && fractionator.productOutputCount == 0) {
            fractionator.fluidId = 0;
        }
        fractionator.isWorking = fractionator.fluidInputCount > 0
                                 && fractionator.productOutputCount < fractionator.productOutputMax
                                 && fractionator.fluidOutputCount < fractionator.fluidOutputMax;
        return fractionator.isWorking ? 1U : 0U;
    }

    /// <summary>将一个原版侧端口的流动物及其增产点继续送到带头。</summary>
    /// <param name="fractionator">原版分馏塔组件。</param>
    /// <param name="cargoTraffic">所属传送带系统。</param>
    /// <param name="beltId">目标侧面输出带 ID。</param>
    /// <param name="maxStack">每份货物的堆叠上限。</param>
    private static void TopUpVanillaFluidOutput(ref FractionatorComponent fractionator, CargoTraffic cargoTraffic,
        int beltId, int maxStack) {
        if (beltId <= 0 || fractionator.fluidOutputCount <= 0) {
            return;
        }
        CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        int moved = TryStackAtBeltHead(path, fractionator.fluidId, maxStack, fractionator.fluidOutputCount,
            fractionator.fluidOutputInc, out int movedInc);
        fractionator.fluidOutputCount -= moved;
        fractionator.fluidOutputInc -= movedInc;
    }

    /// <summary>
    /// 把物品叠到传送带头部的同种货物上；头部有空位时新建一个货物。
    /// </summary>
    /// <param name="path">输出带所在的货物路径；为空时不处理。</param>
    /// <param name="itemId">要输出的物品。</param>
    /// <param name="maxStack">带头货物允许达到的最大件数。</param>
    /// <param name="count">塔内待输出的件数。</param>
    /// <param name="inc">待输出物品的增产点总数；产物没有增产点时传 0。</param>
    /// <param name="movedInc">实际随物品输出的增产点。</param>
    /// <returns>实际输出的件数；带头是别的物品或已满堆时为 0。</returns>
    private static int TryStackAtBeltHead(CargoPath path, int itemId, int maxStack, int count, int inc,
        out int movedInc) {
        movedInc = 0;
        if (path == null || itemId <= 0 || count <= 0) {
            return 0;
        }
        // 带头状态只用来估算可叠数量；真正写入时 TryUpdateItemAtHeadAndFillBlank 会在锁内重新校验。
        int room = maxStack;
        if (path.TestBlankAtHead() < 0) {
            int cargoId = path.GetCargoIdAtIndex(0, 10);
            if (cargoId < 0 || cargoId >= path.cargoContainer.cargoPool.Length) {
                return 0;
            }
            Cargo headCargo = path.cargoContainer.cargoPool[cargoId];
            if (headCargo.item != itemId) {
                return 0;
            }
            room = maxStack - headCargo.stack;
        }
        int moved = Math.Min(Math.Min(count, room), byte.MaxValue);
        if (moved <= 0) {
            return 0;
        }
        int remainingCount = count;
        int remainingInc = Math.Max(0, inc);
        int splitInc = remainingInc > 0 ? split_inc(ref remainingCount, ref remainingInc, moved) : 0;
        // 货物增产点是 byte，超出部分留在塔内，避免账面扣减多于实际随物品离开的数量。
        byte cargoInc = (byte)Math.Min(byte.MaxValue, splitInc);
        if (!path.TryUpdateItemAtHeadAndFillBlank(itemId, maxStack, (byte)moved, cargoInc)) {
            return 0;
        }
        movedInc = cargoInc;
        return moved;
    }
}
