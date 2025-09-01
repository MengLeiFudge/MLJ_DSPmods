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
            AddRecipe(new QuantumCopyRecipe(item.ID, 0.055f,
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
        float essenceCost = itemValue[IFE复制精华] + itemValue[IFE点金精华] + itemValue[IFE分解精华] + itemValue[IFE转化精华];
        EssenceCost = itemValue[InputID] * 3 / essenceCost;
    }

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public override float MainOutputCountInc => (Progress - 0.56f) / 0.88f;

    /// <summary>
    /// 精华消耗基础值
    /// </summary>
    public float EssenceCost { get; }

    /// <summary>
    /// 精华消耗削弱
    /// </summary>
    public float EssenceDec => IsMaxQuality ? 0.05f * Level : 0;

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="pointsBonus">增产剂加成</param>
    /// <param name="buffBonus1">强化对配方成功率加成</param>
    /// <param name="buffBonus2">强化对主产物数目加成</param>
    /// <param name="consumeRegister">全局消耗统计</param>
    /// <param name="notEnoughEssence">精华是否不足</param>
    /// <returns>损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</returns>
    public List<ProductOutputInfo> GetOutputs(ref uint seed, float pointsBonus, float buffBonus1, float buffBonus2,
        int[] consumeRegister, out bool notEnoughEssence) {
        notEnoughEssence = false;
        //损毁
        if (GetRandDouble(ref seed) < DestroyRate) {
            AddExp((float)Math.Log10(1 + itemValue[InputID]));
            return null;
        }
        //无变化，量子复制时增产剂不影响此概率，强化等级影响此概率
        if (GetRandDouble(ref seed) >= SuccessRate * (1 + buffBonus1)) {
            return ProcessManager.emptyOutputs;
        }
        //成功产出
        List<ProductOutputInfo> list = [];
        //主输出判定，量子复制配方主输出必定是第一个，无附加输出，所以删去不必要的条件
        OutputInfo outputInfo = OutputMain[0];
        //整数部分必定输出，小数部分根据概率判定确定是否输出
        float countAvg = outputInfo.OutputCount * (1 + MainOutputCountInc + buffBonus2);
        int countReal = (int)countAvg;
        countAvg -= countReal;
        if (countAvg > 0.0001) {
            if (GetRandDouble(ref seed) < countAvg) {
                countReal++;
            }
        }
        //根据有没有精华判定是否成功输出
        float EssenceDec2 = pointsBonus * 0.5f / (float)ProcessManager.MaxTableMilli(10);
        float essenceCostAvg = EssenceCost * (1 - EssenceDec) * (1 - EssenceDec2);
        int essenceCostReal = (int)essenceCostAvg;
        essenceCostAvg -= essenceCostReal;
        if (essenceCostAvg > 0.0001) {
            if (GetRandDouble(ref seed) < essenceCostAvg) {
                essenceCostReal++;
            }
        }
        if (essenceCostReal > 0 && !TakeEssenceFromModData(essenceCostReal, consumeRegister)) {
            notEnoughEssence = true;
            return ProcessManager.emptyOutputs;
        }
        list.Add(new(true, outputInfo.OutputID, countReal));
        outputInfo.OutputTotalCount += countReal;
        AddExp((float)(Math.Log10(1 + itemValue[outputInfo.OutputID]) * countReal * 0.2));
        //如果仍然没有产出（例如产物数目<1且小数判定未通过），由于原料已消耗，应该返回损毁而非空列表
        return list.Count == 0 ? null : list;
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
