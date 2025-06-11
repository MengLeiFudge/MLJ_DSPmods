using FE.Logic.Manager;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 交互塔，此类不需要配方
/// </summary>
public static class InteractionTower {
    /// <summary>
    /// 创建交互塔
    /// </summary>
    /// <returns>创建的交互塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "交互塔", RFE交互塔, IFE交互塔, MFE交互塔,
            [IFE分馏原胚定向], [1], [1],
            3101, new(0.8f, 0.3f, 0.6f), -50, 2.5f, TFE交互塔
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {

        // 没电就不工作
        if (power < 0.1) {
            __result = 0;
            return;
        }

        // 计算输入缓存区物品的平均堆叠
        double itemStackAvg = 1.0;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0.0f;
        else
            itemStackAvg = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / (double)__instance.fluidInputCargoCount
                : 4.0;

        // 交互塔：快速处理分馏原胚和破损分馏原胚，转换为残片
        if (__instance.fluidInputCount > 0
            && __instance.productOutputCount < __instance.productOutputMax) {

            // 交互塔的处理速度特别快
            __instance.progress += (int)(power
                                         * (1200.0 / 3.0)// 比原版快得多
                                         * (__instance.fluidInputCargoCount < 30.0
                                             ? __instance.fluidInputCargoCount
                                             : 30.0)
                                         * itemStackAvg
                                         + 0.75);

            // 限制最大进度
            if (__instance.progress > 100000)
                __instance.progress = 100000;

            // 处理每次交互
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                // 计算平均每个物品携带的增产点数
                int itemIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;

                if (!__instance.incUsed)
                    __instance.incUsed = itemIncAvg > 0;

                // 交互塔总是成功处理
                __instance.fractionSuccess = true;

                // 处理交互结果
                ++__instance.productOutputCount;
                ++__instance.productOutputTotal;
                lock (productRegister)
                    ++productRegister[__instance.productId];
                lock (consumeRegister)
                    ++consumeRegister[__instance.fluidId];

                // 消耗输入物品
                --__instance.fluidInputCount;
                __instance.fluidInputInc -= itemIncAvg;
                __instance.fluidInputCargoCount -= (float)(1.0 / itemStackAvg);
                if (__instance.fluidInputCargoCount < 0.0)
                    __instance.fluidInputCargoCount = 0.0f;
            }
        } else {
            __instance.fractionSuccess = false;
        }

        // 处理传送带交互
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc1;

        // 处理belt1和belt2（两侧接口）
        // 两侧接口只处理输入，不处理输出
        if (__instance.belt1 > 0
            && !__instance.isOutput1
            && __instance.fluidInputCargoCount < (double)__instance.fluidInputMax) {
            if (__instance.fluidId > 0) {
                if (cargoTraffic.TryPickItemAtRear(__instance.belt1, __instance.fluidId, null, out stack, out inc1)
                    > 0) {
                    __instance.fluidInputCount += (int)stack;
                    __instance.fluidInputInc += (int)inc1;
                    ++__instance.fluidInputCargoCount;
                }
            } else {
                // 交互塔只接受分馏原胚和破损分馏原胚
                int[] interactionNeeds = new int[] { 9500, 9501 };// 假设ID为9500和9501
                int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, interactionNeeds, out stack, out inc1);
                if (needId > 0) {
                    __instance.fluidInputCount += (int)stack;
                    __instance.fluidInputInc += (int)inc1;
                    ++__instance.fluidInputCargoCount;
                    __instance.SetRecipe(needId, signPool);
                }
            }
        }

        if (__instance.belt2 > 0
            && !__instance.isOutput2
            && __instance.fluidInputCargoCount < (double)__instance.fluidInputMax) {
            if (__instance.fluidId > 0) {
                if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null, out stack, out inc1)
                    > 0) {
                    __instance.fluidInputCount += (int)stack;
                    __instance.fluidInputInc += (int)inc1;
                    ++__instance.fluidInputCargoCount;
                }
            } else {
                int[] interactionNeeds = new int[] { 9500, 9501 };// 假设ID为9500和9501
                int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, interactionNeeds, out stack, out inc1);
                if (needId > 0) {
                    __instance.fluidInputCount += (int)stack;
                    __instance.fluidInputInc += (int)inc1;
                    ++__instance.fluidInputCargoCount;
                    __instance.SetRecipe(needId, signPool);
                }
            }
        }

        // 处理belt0（正面输出口）
        if (__instance.belt0 > 0
            && __instance.isOutput0
            && __instance.productOutputCount > 0
            && cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId, (byte)1, (byte)0)) {
            --__instance.productOutputCount;
        }

        // 如果缓存区全部清空，重置输入id
        if (__instance.fluidInputCount == 0 && __instance.productOutputCount == 0)
            __instance.fluidId = 0;

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }
}
