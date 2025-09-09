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
    public static void AddTranslations() {
        Register("点数聚集塔", "Points Aggregate Tower");
        Register("I点数聚集塔",
            "Concentrate proliferator points on certain items so that they carry more than 4 proliferator points.",
            "将增产点数集中到部分物品上，从而使物品携带超过4点增产点数。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.2509f, 0.8392f, 1.0f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;
    public static int ReinforcementLevel = 0;
    private static float ReinforcementBonus => ReinforcementBonusArr[ReinforcementLevel];
    public static float ReinforcementSuccessRate => ReinforcementSuccessRateArr[ReinforcementLevel];
    public static float ReinforcementBonusDurability => ReinforcementBonus * 4;
    public static float ReinforcementBonusEnergy => ReinforcementBonus;
    public static float ReinforcementBonusFracSuccess => ReinforcementBonus;
    public static float ReinforcementBonusMainOutputCount => 0;
    public static float ReinforcementBonusAppendOutputRate => 0;
    private static readonly float propertyRatio = 2.0f;
    public static long workEnergyPerTick => model.prefabDesc.workEnergyPerTick;
    public static long idleEnergyPerTick => model.prefabDesc.idleEnergyPerTick;
    /// <summary>
    /// 建筑等级，1-7。
    /// </summary>
    public static int Level = 1;
    public static int MaxLevel => 7;
    public static bool IsMaxLevel => Level == MaxLevel;
    /// <summary>
    /// 产出物品的最大增产点数，4-10。
    /// </summary>
    public static int MaxInc => Math.Min(10, Level + 3);
    /// <summary>
    /// 产出物品的概率。
    /// </summary>
    public static float SuccessRate => 0.11f + Level * 0.02f;

    public static string Lv => $"Lv{Level}";
    public static string LvWC => Lv.WithPALvColor(Level);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE点数聚集塔, "点数聚集塔", "I点数聚集塔",
            "Assets/fe/point-aggregate-tower", tab分馏 * 1000 + 303, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE点数聚集塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [2], [IFE点数聚集塔], [1],
            "I点数聚集塔", TFE增产点数聚集, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE点数聚集塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(Cosmogenesis.Enable ? 6 : 5, item.GridIndex % 10, true);
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
        model.HpMax = (int)(fractionatorModel.HpMax * propertyRatio * (1 + ReinforcementBonusDurability));
        double energyRatio = propertyRatio * (1 + ReinforcementBonusEnergy);
        model.prefabDesc.workEnergyPerTick = (long)(fractionatorModel.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(fractionatorModel.prefabDesc.idleEnergyPerTick * energyRatio);
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
        bool enableFracForever = building.EnableFracForever();
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
                float rate = __instance.fluidInputInc >= MaxInc ? SuccessRate * (1 + ReinforcementBonusFracSuccess) : 0;
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
                        if (!building.EnableFluidOutputStack()) {
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
                        if (!building.EnableFluidOutputStack()) {
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
        EnableFluidOutputStack = r.ReadBoolean();
        MaxProductOutputStack = r.ReadInt32();
        if (MaxProductOutputStack < 0) {
            MaxProductOutputStack = 0;
        } else if (MaxProductOutputStack > 4) {
            MaxProductOutputStack = 4;
        }
        EnableFracForever = r.ReadBoolean();
        if (version < 2) {
            ReinforcementLevel = 0;
        } else {
            ReinforcementLevel = r.ReadInt32();
            if (ReinforcementLevel < 0) {
                ReinforcementLevel = 0;
            } else if (ReinforcementLevel > MaxReinforcementLevel) {
                ReinforcementLevel = MaxReinforcementLevel;
            }
        }
        UpdateHpAndEnergy();
        Level = r.ReadInt32();
        if (Level < 1) {
            Level = 1;
        } else if (Level > 7) {
            Level = 7;
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(EnableFluidOutputStack);
        w.Write(MaxProductOutputStack);
        w.Write(EnableFracForever);
        w.Write(ReinforcementLevel);
        w.Write(Level);
    }

    public static void IntoOtherSave() {
        EnableFluidOutputStack = false;
        MaxProductOutputStack = 1;
        EnableFracForever = false;
        ReinforcementLevel = 0;
        UpdateHpAndEnergy();
        Level = 1;
    }

    #endregion
}
