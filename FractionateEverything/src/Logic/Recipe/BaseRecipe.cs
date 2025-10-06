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
    /// 配方层次对应的矩阵ID
    /// </summary>
    public int MatrixID = 0;

    /// <summary>
    /// 通过品质和等级得到的综合进度，范围为0.56~1.00
    /// </summary>
    public float Progress => IsMaxQuality
        ? 0.928f + 0.008f * (Level - 1)
        : 0.536f + 0.016f * Quality + 0.008f * Quality * Quality + 0.008f * (Level - 1);

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
    public virtual float MainOutputCountInc => 0.0f;

    /// <summary>
    /// 附加产物概率增幅
    /// </summary>
    public virtual float AppendOutputRatioInc => (Quality - 1) * 0.25f;

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="pointsBonus">增产剂加成</param>
    /// <param name="buffBonus1">强化对配方成功率加成</param>
    /// <param name="buffBonus2">强化对主产物数目加成</param>
    /// <param name="buffBonus3">强化对副产物概率加成</param>
    /// <returns>损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</returns>
    public List<ProductOutputInfo> GetOutputs(ref uint seed, float pointsBonus,
        float buffBonus1, float buffBonus2, float buffBonus3) {
        //损毁
        if (GetRandDouble(ref seed) < DestroyRate) {
            AddExp(1 + itemValue[InputID] / 100 * ExpFix * 2);
            return null;
        }
        //无变化
        if (GetRandDouble(ref seed) >= SuccessRate * (1 + pointsBonus) * (1 + buffBonus1)) {
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
                float countAvg = outputInfo.OutputCount * (1 + MainOutputCountInc + buffBonus2);
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(true, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                AddExp(1 + itemValue[outputInfo.OutputID] / 100 * countReal * ExpFix);
                break;
            }
        }
        //附加输出判定，每一项依次判定，互不影响
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed) <= outputInfo.SuccessRate * (1 + AppendOutputRatioInc) * (1 + buffBonus3)) {
                float countAvg = outputInfo.OutputCount;
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(false, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                //附加输出无经验
            }
        }
        //如果仍然没有产出（例如产物数目<1且小数判定未通过），由于原料已消耗，应该返回损毁而非空列表
        return list.Count == 0 ? null : list;
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
    public int PreviousQuality => Math.Max(1, Quality == MaxQuality ? Quality - 2 : Quality - 1);
    /// <summary>
    /// 下一品质
    /// </summary>
    public int NextQuality => Math.Min(MaxQuality, Quality == MaxQuality - 2 ? MaxQuality : Quality + 1);
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
    public int Echo { get; private set; } = 0;
    /// <summary>
    /// 突破上一品质需要的回响数目
    /// </summary>
    public int BreakPreviousQualityNeedEcho => Math.Max(0, Quality - 2);
    /// <summary>
    /// 突破当前品质需要的回响数目
    /// </summary>
    public int BreakCurrQualityNeedEcho => Math.Max(0, NextQuality - 2);
    /// <summary>
    /// 回响数目是否已达到突破当前品质所需的数目
    /// </summary>
    public bool IsEnoughEchoToBreak => Echo >= BreakCurrQualityNeedEcho;
    /// <summary>
    /// 最高回响数目
    /// </summary>
    public int MaxEcho => MaxQuality - 2;
    /// <summary>
    /// 回响数目是否已达到上限
    /// </summary>
    public bool IsMaxEcho => Echo >= MaxEcho;

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
    public int CurrQualityCurrLevelExp => GetExp(Quality, Level);
    /// <summary>
    /// 不同配方获取经验效率不同
    /// </summary>
    public virtual float ExpFix => 1.0f;

    public int GetExp(int quality, int level) {
        return (int)(40 * Math.Pow(quality * 2 + level + 2, 2.0));
    }

    public float GetExpToNextLevel() {
        return Math.Max(0, CurrQualityCurrLevelExp - Exp);
    }

    public float GetExpToMaxLevel() {
        if (Level == CurrQualityMaxLevel) {
            return 0;
        }
        float ret = CurrQualityCurrLevelExp - Exp;
        for (int i = Level + 1; i < CurrQualityMaxLevel; i++) {
            ret += GetExp(Quality, i);
        }
        return ret;
    }

    /// <summary>
    /// 经验是否达到当前品质、当前等级的上限
    /// </summary>
    public bool IsCurrQualityCurrLevelMaxExp => (Quality == MaxQuality && Level == CurrQualityMaxLevel)
                                                || Exp >= CurrQualityCurrLevelExp;

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
            Echo++;
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
            while (!IsMaxQuality && IsCurrQualityMaxLevel && IsCurrQualityCurrLevelMaxExp && IsEnoughEchoToBreak) {
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

    /// <summary>
    /// 沙盒模式下，将配方升一级/降一级
    /// </summary>
    public void SandBoxUpDowngrade(bool up) {
        if (up) {
            if (FullUpgrade) {
                //do nothing
            } else if (Locked) {
                Quality = 1;
                Level = 1;
            } else if (IsCurrQualityMaxLevel) {
                Quality = NextQuality;
                Level = 1;
            } else {
                Level++;
            }
        } else {
            if (Level > 1) {
                Level--;
            } else if (Locked) {
                //do nothing
            } else if (Quality == 1 && Level == 1) {
                Quality = 0;
                Level = 0;
            } else {
                Quality = PreviousQuality;
                Level = CurrQualityMaxLevel;
            }
        }
        Echo = BreakPreviousQualityNeedEcho;
        Exp = 0;
    }

    /// <summary>
    /// 沙盒模式下，将配方升到最高品质、最高级/重置到最低品质、最低级
    /// </summary>
    public void SandBoxMaxUpDowngrade(bool up) {
        if (up) {
            Quality = MaxQuality;
            Level = CurrQualityMaxLevel;
            Echo = MaxEcho;
        } else {
            Quality = 0;
            Level = 0;
            Echo = 0;
        }
        Exp = 0;
    }

    #endregion

    public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name}";
    public string TypeNameWC => TypeName.WithColor(Quality);
    public string LvExp => $"Lv{Level} ({Exp:F0} / {(FullUpgrade ? "∞" : CurrQualityCurrLevelExp)})";
    public string LvExpWC => LvExp.WithColor(Quality);

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
        Quality = r.ReadInt32();
        Level = r.ReadInt32();
        Exp = r.ReadSingle();
        Echo = r.ReadInt32();
        if (Quality < 0) {
            Quality = 0;
            Level = 0;
        } else if (Quality == MaxQuality - 1) {
            Quality++;
        } else if (Quality > MaxQuality) {
            Quality = MaxQuality;
        }
        if (Level == 0) {
            if (Quality > 0.0001) {
                Level = 1;
            }
        } else if (Level > CurrQualityMaxLevel) {
            Level = CurrQualityMaxLevel;
        }
        if (Exp < 0) {
            Exp = 0;
        }
        if (Echo < 0) {
            Echo = 0;
        } else if (Echo > MaxEcho) {
            Echo = MaxEcho;
        }
        if (Echo < BreakPreviousQualityNeedEcho) {
            Echo = BreakPreviousQualityNeedEcho;
        }
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
        w.Write(Quality);
        w.Write(Level);
        w.Write(Exp);
        w.Write(Echo);
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
        Echo = 0;
    }

    #endregion
}
