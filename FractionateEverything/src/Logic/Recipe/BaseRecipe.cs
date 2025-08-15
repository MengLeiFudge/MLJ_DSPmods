using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.UI.View.Setting.SandboxMode;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float maxSuccessRate,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    #region 配方类型、输入输出

    /// <summary>
    /// 类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 输入物品的ID
    /// </summary>
    public int InputID => inputID;

    /// <summary>
    /// 通过品质和等级得到的综合进度，范围为0.35~1.00
    /// </summary>
    private float Progress => Math.Min(1.0f,
        0.365f + (Quality - 1) * 0.11f + (Quality - 1) * (Quality - 2) * 0.0075f + (Level - 1) * 0.015f);

    /// <summary>
    /// 成功率上限
    /// </summary>
    public float MaxSuccessRate => maxSuccessRate;

    /// <summary>
    /// 实际成功率，随着等级和品质的提高而提高。
    /// </summary>
    public float SuccessRate => MaxSuccessRate * Progress;

    /// <summary>
    /// 损毁率上限
    /// </summary>
    public float MaxDestroyRate => maxSuccessRate;

    /// <summary>
    /// 损毁率，随着等级和品质的提高而降低。
    /// </summary>
    public float DestroyRate => MaxDestroyRate * (1 - Progress);

    /// <summary>
    /// 主产物信息，概率之和必须为100%。
    /// 当判定成功时，必定输出且仅输出其中一项。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputMain => outputMain;

    /// <summary>
    /// 副产物信息。
    /// 当判定成功时，该列表内每一项分别判定是否成功。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputAppend => outputAppend;

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public virtual float MainOutputCountInc => 1.0f;

    /// <summary>
    /// 附加产物数目增幅
    /// </summary>
    public virtual float AppendOutputCountInc => 1.0f;

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="successRatePlus">增产剂对成功率的加成</param>
    /// <param name="consumeRegister">全局消耗统计</param>
    /// <returns>损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</returns>
    public virtual List<ProductOutputInfo> GetOutputs(ref uint seed, float successRatePlus, int[] consumeRegister) {
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
                int count = (int)Math.Ceiling((outputInfo.OutputCount - 0.0001f) * MainOutputCountInc);
                float leftCount = outputInfo.OutputCount * MainOutputCountInc - count;
                if (leftCount > 0.0001f) {
                    if (GetRandDouble(ref seed) < leftCount) {
                        count++;
                    }
                }
                list.Add(new(true, outputInfo.OutputID, count));
                outputInfo.OutputTotalCount += count;
                AddExp((float)(Math.Log10(1 + itemValue[outputInfo.OutputID]) * count * 0.2));
                break;
            }
        }
        //附加输出判定，每一项依次判定，互不影响
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed) <= outputInfo.SuccessRate) {
                int count = (int)Math.Ceiling((outputInfo.OutputCount - 0.0001f) * AppendOutputCountInc);
                float leftCount = outputInfo.OutputCount * AppendOutputCountInc - count;
                if (leftCount > 0.0001f) {
                    if (GetRandDouble(ref seed) < leftCount) {
                        count++;
                    }
                }
                list.Add(new(false, outputInfo.OutputID, count));
                outputInfo.OutputTotalCount += count;
                //附加输出无经验
            }
        }
        return list;
    }

    #endregion

    #region 配方品质、等级

    /// <summary>
    /// 品质
    /// </summary>
    /// <details>
    /// 未解锁时为0。解锁之后，最低为1，最高为7。0灰、1白、2绿、3蓝、4紫、5红、7金。
    /// </details>
    public int Quality { get; private set; } = 0;
    /// <summary>
    /// 下一品质
    /// </summary>
    public int NextQuality => Quality == 5 ? Quality + 2 : Quality + 1;
    /// <summary>
    /// 最高品质
    /// </summary>
    public int MaxQuality => 7;
    /// <summary>
    /// 品质是否达到上限
    /// </summary>
    public bool IsMaxQuality => Quality == MaxQuality;

    /// <summary>
    /// 回响数目
    /// </summary>
    public int Memory { get; private set; } = 0;
    /// <summary>
    /// 突破当前品质需要的回响数目
    /// </summary>
    public int BreakCurrQualityNeedMemory => Math.Max(0, NextQuality - 2);
    /// <summary>
    /// 回响数目是否已达到突破当前品质所需的数目
    /// </summary>
    public bool IsEnoughMemoryToBreak => Memory >= BreakCurrQualityNeedMemory;
    /// <summary>
    /// 最高回响数目
    /// </summary>
    public int MaxMemory => MaxQuality - 2;
    /// <summary>
    /// 回响数目是否已达到上限
    /// </summary>
    public bool IsMaxMemory => Memory >= MaxMemory;

    /// <summary>
    /// 等级
    /// </summary>
    /// <details>
    /// 未解锁时为0。解锁之后，最低为1，最高为Quality + 3。
    /// </details>
    public int Level { get; private set; } = 0;
    /// <summary>
    /// 当前品质下的最高等级
    /// </summary>
    public int CurrQualityMaxLevel => Quality + 3;
    /// <summary>
    /// 等级是否达到当前品质的上限
    /// </summary>
    public bool IsCurrQualityMaxLevel => Level == CurrQualityMaxLevel;

    /// <summary>
    /// 经验值
    /// </summary>
    /// <details>
    /// 达到下一级所需经验会自动升级。突破后会扣除当前经验上限的经验。
    /// </details>
    public float Exp { get; private set; } = 0;
    /// <summary>
    /// 当前品质、当前等级下，达到多少经验可以升级
    /// </summary>
    public int CurrQualityCurrLevelExp => (int)(200 * Math.Pow(Quality * 2 + Level + 2, 2.0));
    /// <summary>
    /// 升级还需要多少经验
    /// </summary>
    public float StillNeedExp => Math.Max(0, CurrQualityCurrLevelExp - Exp);
    /// <summary>
    /// 经验是否达到当前品质、当前等级的上限
    /// </summary>
    public bool IsCurrQualityCurrLevelMaxExp => Exp >= CurrQualityCurrLevelExp;

    /// <summary>
    /// 是否未解锁
    /// </summary>
    public bool Locked => Level <= 0;
    /// <summary>
    /// 是否已解锁
    /// </summary>
    public bool Unlocked => Level > 0;
    /// <summary>
    /// 是否已达到最大升级
    /// </summary>
    public bool FullUpgrade => IsMaxQuality && IsCurrQualityMaxLevel;

    /// <summary>
    /// 添加经验，同时触发升级与突破
    /// </summary>
    public void AddExp(float exp, bool useExpMultiRate = true) {
        lock (this) {
            Exp += useExpMultiRate ? exp * ExpMultiRate : exp;
            CheckState();
        }
    }

    /// <summary>
    /// 通过某种方式（例如抽奖，科技奖励等）获取到该配方。
    /// 如果配方未解锁，则解锁此配方；如果已解锁，则回响数目+1，并检查是否可突破。
    /// </summary>
    public void RewardThis() {
        lock (this) {
            if (Locked) {
                Level = 1;
                Quality = 1;
                Exp = 0;
                return;
            }
            Memory++;
            CheckState();
        }
    }

    /// <summary>
    /// 检查配方是否可以升级与突破
    /// </summary>
    public void CheckState() {
        lock (this) {
            //是否可升级
            CheckLevel:
            while (!IsCurrQualityMaxLevel && IsCurrQualityCurrLevelMaxExp) {
                Exp -= CurrQualityCurrLevelExp;
                Level++;
            }
            //是否可突破
            while (!IsMaxQuality && IsCurrQualityMaxLevel && IsCurrQualityCurrLevelMaxExp && IsEnoughMemoryToBreak) {
                if (GetRandDouble() < 1.0f - (Quality - 1) * 0.15f) {
                    Exp -= CurrQualityCurrLevelExp;
                    Level = 1;
                    Quality = NextQuality;
                    goto CheckLevel;
                }
                Exp -= CurrQualityCurrLevelExp / 5.0f;
            }
        }
    }

    #endregion

    public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name}";
    public string TypeNameWC => TypeName.WithQualityColor(Quality);
    public string LvExp => $"Lv{Level} ({(int)Exp} / {(FullUpgrade ? "∞" : CurrQualityCurrLevelExp)})";
    public string LvExpWC => LvExp.WithQualityColor(Quality);

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int outputMainCount = r.ReadInt32();
        for (int i = 0; i < outputMainCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            var outputInfo = OutputMain.Find(info => info.OutputID == outputID);
            if (outputInfo != null) {
                outputInfo.OutputTotalCount = outputTotalCount;
            } else {
                LogWarning($"Output {outputID} not found in {TypeName} main outputs");
            }
        }
        int outputAppendCount = r.ReadInt32();
        for (int i = 0; i < outputAppendCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            var outputInfo = OutputAppend.Find(info => info.OutputID == outputID);
            if (outputInfo != null) {
                outputInfo.OutputTotalCount = outputTotalCount;
            } else {
                LogWarning($"Output {outputID} not found in {TypeName} append outputs");
            }
        }
        Quality = Math.Min(MaxQuality, r.ReadInt32());
        if (Quality == MaxQuality - 1) {
            Quality++;
        }
        Level = Math.Min(CurrQualityMaxLevel, r.ReadInt32());
        Exp = r.ReadSingle();
        Memory = Math.Min(MaxMemory, r.ReadInt32());
        AddExp(0);//触发升级、突破判断
        // 子类特定数据由重写的方法处理
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(OutputMain.Count);
        foreach (OutputInfo info in OutputMain) {
            w.Write(info.OutputID);
            w.Write(info.OutputTotalCount);
        }
        w.Write(OutputAppend.Count);
        foreach (OutputInfo info in OutputAppend) {
            w.Write(info.OutputID);
            w.Write(info.OutputTotalCount);
        }
        w.Write(Level);
        w.Write(Quality);
        w.Write(Exp);
        w.Write(Memory);
        // 子类特定数据由重写的方法处理
    }

    public virtual void IntoOtherSave() {
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
        Quality = 0;
        Level = 0;
        Exp = 0;
        Memory = 0;
    }

    #endregion
}
