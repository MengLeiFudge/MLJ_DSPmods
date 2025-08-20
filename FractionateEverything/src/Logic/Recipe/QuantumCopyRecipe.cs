using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 量子复制塔配方类（可复制除建筑外的所有物品）
/// </summary>
public class QuantumCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有量子复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方通用核心
                || item.ID == IFE分馏塔增幅芯片
                || !item.GridIndexValid()
                || item.ID == I沙土) {
                continue;
            }
            //量子复制塔不能处理建筑
            if (item.BuildMode != 0) {
                continue;
            }
            AddRecipe(new QuantumCopyRecipe(item.ID, itemRatio[item.ID],
                [
                    new OutputInfo(1.000f, item.ID, 2),
                ],
                []));
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.QuantumCopy;

    /// <summary>
    /// 创建量子复制塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public QuantumCopyRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) {
        EssenceCost = (float)(0.01 * Math.Pow(itemValue[InputID], Math.Log(2, 3)));
    }

    /// <summary>
    /// 消耗精华数目
    /// </summary>
    public float EssenceCost { get; private set; }

    /// <summary>
    /// 精华消耗减少
    /// </summary>
    public float EssenceCostDec => 1.0f - (IsMaxQuality ? 0.08f * Level : 0);

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="successRatePlus">增产剂对成功率的加成</param>
    /// <param name="consumeRegister">全局消耗统计</param>
    /// <returns>损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</returns>
    public override List<ProductOutputInfo> GetOutputs(ref uint seed, float successRatePlus, int[] consumeRegister) {
        //损毁
        if (GetRandDouble(ref seed) < DestroyRate) {
            AddExp((float)(Math.Log10(1 + itemValue[OutputMain[0].OutputID]) * 0.1));
            return null;
        }
        //无变化
        if (GetRandDouble(ref seed) >= SuccessRate * successRatePlus) {
            return ProcessManager.emptyOutputs;
        }
        //成功产出
        List<ProductOutputInfo> list = [];
        //主输出判定，由于主输出概率之和为100%，所以必定输出且只会输出其中一个
        double ratio = GetRandDouble(ref seed);
        float ratioMain = 0.0f;//用于累计概率
        foreach (var outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRate;
            if (ratio <= ratioMain) {
                //整数部分必定输出，小数部分根据概率判定确定是否输出
                float countAvg = outputInfo.OutputCount * MainOutputCountInc;
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                //根据有没有精华判定是否成功输出
                float essenceCountAvg = EssenceCost * EssenceCostDec;
                int essenceCountReal = (int)essenceCountAvg;
                essenceCountAvg -= essenceCountReal;
                if (essenceCountAvg > 0) {
                    if (GetRandDouble(ref seed) < essenceCountAvg) {
                        essenceCountReal++;
                    }
                }
                if (essenceCountReal > 0 && !TakeEssenceFromModData(essenceCountReal, consumeRegister)) {
                    return ProcessManager.emptyOutputs;
                }
                list.Add(new(true, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                AddExp((float)(Math.Log10(1 + itemValue[outputInfo.OutputID]) * countReal * 0.2));
                break;
            }
        }
        //附加输出判定，每一项依次判定，互不影响
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed) <= outputInfo.SuccessRate) {
                float countAvg = outputInfo.OutputCount * AppendOutputCountInc;
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(false, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                //附加输出无经验
            }
        }
        return list;
    }

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        int version = r.ReadInt32();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.Write(1);
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
