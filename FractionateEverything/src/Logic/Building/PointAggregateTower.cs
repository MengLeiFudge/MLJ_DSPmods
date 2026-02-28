using System;
using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Manager;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 点数聚集塔
/// </summary>
public static class PointAggregateTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.2509f, 0.8392f, 1.0f);

    public static int Level = 0;
    public static bool EnableFluidEnhancement => Level >= 3;
    public static int MaxProductOutputStack => Level switch {
        < 9 => 1,
        _ => 4,
    };
    public static int MaxInc => Math.Min(Level + 4, 10);
    public static float PlrRatio => Level switch {
        < 1 => 1.0f,
        < 4 => 1.1f,
        < 7 => 1.3f,
        < 10 => 1.6f,
        _ => 2.0f,
    };
    public static float EnergyRatio => Level switch {
        < 2 => 1.0f,
        < 5 => 0.95f,
        < 8 => 0.85f,
        < 11 => 0.7f,
        _ => 0.5f,
    };
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }
    public static long idleEnergyPerTick {
        get => model.prefabDesc.idleEnergyPerTick;
        set => model.prefabDesc.idleEnergyPerTick = value;
    }

    public static void AddTranslations() {
        Register("点数聚集塔", "Points Aggregate Tower");
        Register("I点数聚集塔",
            "Concentrate proliferator points onto specific items to produce goods carrying greater proliferator points. Requires upgrading the proliferator point aggregation efficiency tier at the fractionation data centre.",
            "将增产点数集中到部分物品上，从而产出携带更多的增产点数的物品。需要在分馏数据中心升级点数聚集效率层次。");
    }

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE点数聚集塔, "点数聚集塔", "I点数聚集塔",
            "Assets/fe/point-aggregate-tower", tab分馏 * 1000 + 303, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE点数聚集塔,
            ERecipeType.Assemble, 60, [IFE分馏塔定向原胚], [2], [IFE点数聚集塔], [3],
            "I点数聚集塔", TFE增产点数聚集, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "dsjjt";
        recipe.IconTag = "dsjjt";
        model = ProtoRegistry.RegisterModel(MFE点数聚集塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
    }

    public static void SetMaterial() {
        Material m_main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
        Material m_black = model.prefabDesc.lodMaterials[0][1];
        Material m_glass = model.prefabDesc.lodMaterials[0][2];
        Material m_glass1 = model.prefabDesc.lodMaterials[0][3];
        Material m_lod = new(model.prefabDesc.lodMaterials[1][0]) { color = color };
        Material m_lod2 = new(model.prefabDesc.lodMaterials[2][0]) { color = color };
        model.prefabDesc.materials = [m_main, m_black];
        model.prefabDesc.lodMaterials = [
            [m_main, m_black, m_glass, m_glass1],
            [m_lod, m_black, m_glass, m_glass1],
            [m_lod2, m_black, m_glass, m_glass1],
            null,
        ];
    }

    public static void UpdateHpAndEnergy() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ModelProto fractionatorModel = LDB.models.Select(M分馏塔);
        workEnergyPerTick = (long)(fractionatorModel.prefabDesc.workEnergyPerTick * EnergyRatio);
        idleEnergyPerTick = (long)(fractionatorModel.prefabDesc.idleEnergyPerTick * EnergyRatio);
    }

    public static void InternalUpdate(ref FractionatorComponent __instance, PlanetFactory factory,
        float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result) {
        if (power < 0.1) {
            __result = 0;
            return;
        }
        float fluidInputCountPerCargo = 1.0f;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                : 4f;
        int fluidId = __instance.fluidId;
        int buildingID = factory.entityPool[__instance.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        int fluidInputMax = building.FluidInputMax();
        int productOutputMax = building.ProductOutputMax();
        int fluidOutputMax = building.FluidOutputMax();
        bool enableFracForever = building.EnableFluidEnhancement();
        if (__instance.fluidInputCount > 0
            && (__instance.productOutputCount < productOutputMax || enableFracForever)
            && __instance.fluidOutputCount < fluidOutputMax) {
            __instance.progress += (int)(power
                                         * (500.0 / 3.0)
                                         * (__instance.fluidInputCargoCount < MaxBeltSpeed
                                             ? __instance.fluidInputCargoCount
                                             : MaxBeltSpeed)
                                         * fluidInputCountPerCargo
                                         + 0.75);
            if (__instance.progress > 100000)
                __instance.progress = 100000;
            //是否直接将输入搬运到输出，不进行任何处理
            bool moveDirectly = false;
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                MoveDirectly:
                if (moveDirectly) {
                    //直接将输入搬运到输出，不进行任何处理
                    __instance.fractionSuccess = false;
                    __instance.fluidInputCount--;
                    __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) {
                        __instance.fluidInputCargoCount = 0f;
                    }
                    __instance.fluidInputInc -= fluidInputIncAvg;
                    __instance.fluidOutputCount++;
                    __instance.fluidOutputInc += fluidInputIncAvg;
                    continue;
                }
                //如果已研究分馏永动，判断分馏塔是否进入分馏永动状态
                if (enableFracForever
                    && __instance.productOutputCount >= productOutputMax
                    && __instance.fluidOutputCount < fluidOutputMax) {
                    moveDirectly = true;
                    goto MoveDirectly;
                }
                //正常处理，获取处理结果
                float rate = __instance.fluidInputInc >= MaxInc ? (Level / 20.0f) : 0;
                __instance.fractionSuccess = GetRandDouble(ref __instance.seed) < rate;
                if (__instance.fractionSuccess) {
                    __instance.fluidInputInc -= MaxInc;
                    __instance.productOutputCount++;
                    __instance.productOutputTotal++;
                } else {
                    __instance.fluidInputInc -= fluidInputIncAvg;
                    __instance.fluidOutputCount++;
                    __instance.fluidOutputTotal++;
                    __instance.fluidOutputInc += fluidInputIncAvg;
                }
                __instance.fluidInputCount--;
                __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                if (__instance.fluidInputCargoCount < 0f) {
                    __instance.fluidInputCargoCount = 0f;
                }
            }
        } else {
            __instance.fractionSuccess = false;
        }
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                if (__instance.fluidOutputCount > 0) {
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                    if (cargoPath != null) {
                        //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
                        //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
                        //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
                        int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        if (!building.EnableFluidEnhancement()) {
                            //未研究流动输出集装科技，根据传送带最大速率每帧判定2-4次
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (buildingID == IFE点数聚集塔
                                    && fluidOutputIncAvg < 4
                                    && __instance.fluidOutputCount > 1) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                }
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputIncAvg)) {
                                    break;
                                }
                                __instance.fluidOutputCount--;
                                __instance.fluidOutputInc -= fluidOutputIncAvg;
                            }
                        } else {
                            //已研究流动输出集装科技
                            if (__instance.fluidOutputCount >= 4) {
                                //超过4个，则输出4个
                                if (buildingID == IFE点数聚集塔 && fluidOutputIncAvg < 4) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                }
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, 4, (byte)(fluidOutputIncAvg * 4))) {
                                    __instance.fluidOutputCount -= 4;
                                    __instance.fluidOutputInc -= fluidOutputIncAvg * 4;
                                }
                            } else if (__instance.fluidInputCount == 0) {
                                //未超过4个且输入为空，剩几个输出几个
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, (byte)__instance.fluidOutputCount,
                                        (byte)__instance.fluidOutputInc)) {
                                    __instance.fluidOutputCount = 0;
                                    __instance.fluidOutputInc = 0;
                                }
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt1, fluidId, null, out stack, out inc) > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        __instance.productId = needId;
                        __instance.produceProb = 0.01f;
                        signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                        signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                    }
                }
            }
        }
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                if (__instance.fluidOutputCount > 0) {
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                    if (cargoPath != null) {
                        int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        if (!building.EnableFluidEnhancement()) {
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (buildingID == IFE点数聚集塔
                                    && fluidOutputIncAvg < 4
                                    && __instance.fluidOutputCount > 1) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                }
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputIncAvg)) {
                                    break;
                                }
                                __instance.fluidOutputCount--;
                                __instance.fluidOutputInc -= fluidOutputIncAvg;
                            }
                        } else {
                            if (__instance.fluidOutputCount >= 4) {
                                if (buildingID == IFE点数聚集塔 && fluidOutputIncAvg < 4) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                }
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, 4, (byte)(fluidOutputIncAvg * 4))) {
                                    __instance.fluidOutputCount -= 4;
                                    __instance.fluidOutputInc -= fluidOutputIncAvg * 4;
                                }
                            } else if (__instance.fluidInputCount == 0) {
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, (byte)__instance.fluidOutputCount,
                                        (byte)__instance.fluidOutputInc)) {
                                    __instance.fluidOutputCount = 0;
                                    __instance.fluidOutputInc = 0;
                                }
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt2, fluidId, null, out stack, out inc) > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        __instance.productId = needId;
                        __instance.produceProb = 0.01f;
                        signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                        signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                    }
                }
            }
        }
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                //输出主产物
                int productStack = building.MaxProductOutputStack();
                for (int i = 0; i < MaxOutputTimes; i++) {
                    //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                    if (__instance.productOutputCount >= productStack) {
                        //产物达到最大堆叠数目，直接尝试输出
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)productStack, (byte)(productStack * MaxInc))) {
                            break;
                        }
                        __instance.productOutputCount -= productStack;
                    } else if (__instance.productOutputCount > 0 && __instance.fluidInputCount == 0) {
                        //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)__instance.productOutputCount, (byte)(__instance.productOutputCount * MaxInc))) {
                            break;
                        }
                        __instance.productOutputCount = 0;
                    } else {
                        break;
                    }
                }
            }
        }

        // 如果缓存区全部清空，重置输入id
        if (__instance.fluidInputCount == 0
            && __instance.fluidOutputCount == 0
            && __instance.productOutputCount == 0) {
            __instance.fluidId = 0;
            __instance.productId = 0;
            signPool[__instance.entityId].iconId0 = 0;
            signPool[__instance.entityId].iconType = 0U;
        }

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < productOutputMax
                               && __instance.fluidOutputCount < fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        if (version < 2) {
            r.ReadBoolean();
            r.ReadInt32();
            r.ReadBoolean();
        }
        Level = r.ReadInt32();
        if (Level < 0) {
            Level = 0;
        } else if (Level > MaxLevel) {
            Level = MaxLevel;
        }
        UpdateHpAndEnergy();
        if (version < 2) {
            r.ReadInt32();
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(Level);
    }

    public static void IntoOtherSave() {
        Level = 0;
        UpdateHpAndEnergy();
    }

    #endregion
}
