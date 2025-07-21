using System;
using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.UI.View.TabOtherSetting;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类，所有分馏塔配方的基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float maxSuccessRate,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    #region 配方类型、输入输出

    /// <summary>
    /// 配方类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 配方输入物品的ID
    /// </summary>
    public int InputID => inputID;

    /// <summary>
    /// 配方成功率上限
    /// </summary>
    public float MaxSuccessRate => maxSuccessRate;

    //todo:可以开放两种模式。一种是现在的，也就是重置等级，但是加成更高；另一种是等级影响很小，品质影响大，且效果要一直上升

    private float Progress1 => (float)(Quality + 5) / (MaxQuality + 5);

    /// <summary>
    /// 通过品质和等级得到该配方的整体进度，范围为0-1
    /// </summary>
    private float Progress => (Quality * 0.2f + Level * (0.1f + Quality * 0.02f))
                              / (MaxQuality * 0.2f + (MaxQuality + 3) * (0.1f + MaxQuality * 0.02f));

    /// <summary>
    /// 实际成功率，随着等级和品质的提高而提高。
    /// 初始为上限的40%，最终为上限。
    /// </summary>
    public float SuccessRate => MaxSuccessRate * (0.4f + 0.6f * Progress);

    /// <summary>
    /// 配方损毁率，随着等级和品质的提高而降低。
    /// 初始与物品价值有关（价值越高损毁率越高），最终为0%。
    /// </summary>
    public float DestroyRate => 0.1f * (0.2f + (float)Math.Log10(itemValue[InputID] + 1) / 4.0f) * (1 - Progress);

    /// <summary>
    /// 配方主产物信息，概率之和必须为100%。
    /// 当判定成功时，必定输出且仅输出其中一项。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputMain => outputMain;

    /// <summary>
    /// 配方副产物信息。
    /// 当判定成功时，该列表内每一项分别判定是否成功。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputAppend => outputAppend;

    /// <summary>
    /// 获取某次输出的执行结果
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="successRatePlus">增产剂对成功率的加成</param>
    /// <returns>损毁返回null，无变化反馈空字典，成功返回输出产物</returns>
    public virtual Dictionary<int, int> GetOutputs(ref uint seed, float successRatePlus) {
        if (GetRandDouble(ref seed) < DestroyRate) {
            AddExp((int)Math.Ceiling(Math.Log10(1 + itemValue[OutputMain[0].OutputID]) * 0.1));
            return null;
        }
        Dictionary<int, int> dic = [];
        if (GetRandDouble(ref seed) >= SuccessRate * successRatePlus) {
            return dic;
        }
        //主输出判定
        double ratio = GetRandDouble(ref seed);
        float ratioMain = 0.0f;//用于累计概率
        foreach (var outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRate;
            if (ratio <= ratioMain) {
                //整数部分必定输出，小数部分根据概率判定确定是否输出
                int count = (int)Math.Ceiling(outputInfo.OutputCount - 0.0001f);
                float leftCount = outputInfo.OutputCount - count;
                if (leftCount > 0.0001f) {
                    if (GetRandDouble(ref seed) < leftCount) {
                        count++;
                    }
                }
                //由于此处必定是第一个key，所以直接添加
                dic[outputInfo.OutputID] = count;
                outputInfo.OutputTotalCount += count;
                AddExp((int)Math.Ceiling(Math.Log10(1 + itemValue[outputInfo.OutputID]) * count * 0.2));
                break;
            }
        }
        //附加输出判定
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed) <= outputInfo.SuccessRate) {
                int count = (int)Math.Ceiling(outputInfo.OutputCount - 0.0001f);
                float leftCount = outputInfo.OutputCount - count;
                if (leftCount > 0.0001f) {
                    if (GetRandDouble(ref seed) < leftCount) {
                        count++;
                    }
                }
                if (dic.TryGetValue(outputInfo.OutputID, out int currentValue)) {
                    dic[outputInfo.OutputID] = currentValue + count;
                } else {
                    dic.Add(outputInfo.OutputID, count);
                }
                outputInfo.OutputTotalCount += count;
                //附加输出无经验
                // AddExp((int)Math.Ceiling(Math.Log10(1 + itemValue[outputInfo.OutputID]) * count * 0.2));
            }
        }
        return dic;
    }

    #endregion

    #region 配方解锁

    /// <summary>
    /// 解锁状态
    /// </summary>
    public bool IsUnlocked => Level > 0;

    #endregion

    #region 配方等级与星级

    /// <summary>
    /// 配方品质
    /// </summary>
    /// <details>
    /// 未解锁时为0。解锁之后，最低为1，最高为7。0灰、1白、2绿、3蓝、4紫、5红、7金。
    /// </details>
    public int Quality { get; set; } = 0;
    public int NextQuality => Quality == 5 ? Quality + 2 : Quality + 1;
    public int MaxQuality => 7;

    /// <summary>
    /// 配方等级
    /// </summary>
    /// <details>
    /// 未解锁时为0。解锁之后，最低为1，最高为Quality + 3。
    /// </details>
    public int Level { get; set; } = 0;
    public int MaxLevel => Quality + 3;

    /// <summary>
    /// 经验值
    /// </summary>
    /// <details>
    /// 达到下一级所需经验会自动升级。到达等级上限仍可获取经验，突破时多余经验会按照一定比例转化。
    /// </details>
    public long Exp { get; private set; } = 0;

    /// <summary>
    /// 升级所需经验
    /// </summary>
    public long LevelUpExp => (long)(1000 * Math.Pow(1.8, Quality - 1) * Math.Pow(1.4, Level - 1));

    /// <summary>
    /// 当前品质最高经验
    /// </summary>
    public long MaxLevelUpExp => (long)(1000 * Math.Pow(1.8, Quality - 1) * Math.Pow(1.4, MaxLevel - 1));

    /// <summary>
    /// 配方回响个数。
    /// </summary>
    public int MemoryCount { get; set; } = 0;
    public int BreakMemoryCount => Math.Max(0, NextQuality - 2);
    public int MaxMemoryCount => 5;

    public bool CanBreakthrough1 => Quality < MaxQuality;
    public bool CanBreakthrough2 => Level >= MaxLevel;
    public bool CanBreakthrough3 => Exp >= MaxLevelUpExp;
    public bool CanBreakthrough4 => MemoryCount >= BreakMemoryCount;

    /// <summary>v
    /// 指示是否满足突破的前置条件
    /// </summary>
    public bool CanBreakthrough => CanBreakthrough1 && CanBreakthrough2 && CanBreakthrough3 && CanBreakthrough4;

    /// <summary>
    /// 添加经验
    /// </summary>
    public void AddExp(int exp) {
        lock (this) {
            // LogDebug($"Quality{Quality} Lv{Level} ({Exp} + {exp}/{LevelUpExp})");
            Exp += (int)(exp * ExpMultiRate);
            if (Exp >= LevelUpExp && !CanBreakthrough2) {
                Exp -= LevelUpExp;
                Level++;
                LogDebug($"Level Up! Quality{Quality} Lv{Level} ({Exp}/{LevelUpExp})");
            }
            TryBreakQuality();
        }
    }

    /// <summary>
    /// 突破配方品质
    /// </summary>
    /// <returns>是否突破成功</returns>
    public virtual void TryBreakQuality() {
        lock (this) {
            while (CanBreakthrough) {
                float successRate = 1.0f - (Quality - 1) * 0.15f;
                bool success = GetRandDouble() < successRate;
                if (success) {
                    Exp -= LevelUpExp;
                    Exp = (int)(Exp * 0.7f);
                    Level = 1;
                    //红到金是品质+2
                    if (Quality == 5) {
                        Quality += 2;
                    } else {
                        Quality++;
                    }
                    LogDebug($"Quality broke success! Quality{Quality} Lv{Level} ({Exp}/{LevelUpExp})");
                } else {
                    LogDebug($"Quality broke fail! Quality{Quality} Lv{Level} ({Exp}/{LevelUpExp})");
                    Exp -= LevelUpExp / 5;
                }
            }
        }
    }

    #endregion

    public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name}";
    public string TypeNameWC => TypeName.WithQualityColor(Quality);
    public string LvExp => $"Lv{Level} ({Exp}/{LevelUpExp})";
    public string LvExpWC => LvExp.WithQualityColor(Quality);


    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int outputMainCount = r.ReadInt32();
        for (int i = 0; i < outputMainCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            foreach (OutputInfo info in OutputMain) {
                if (info.OutputID == outputID) {
                    info.OutputTotalCount = outputTotalCount;
                    break;
                }
            }
        }
        int outputAppendCount = r.ReadInt32();
        for (int i = 0; i < outputAppendCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            foreach (OutputInfo info in OutputAppend) {
                if (info.OutputID == outputID) {
                    info.OutputTotalCount = outputTotalCount;
                    break;
                }
            }
        }
        Quality = Math.Min(r.ReadInt32(), 7);
        //配方变动时Quality可能变成6，先这样临时解决一下
        if (Quality == 6) {
            Quality = 7;
        }
        Level = Math.Min(r.ReadInt32(), Quality + 3);
        Exp = r.ReadInt64();
        AddExp(0);//触发升级、突破判断
        MemoryCount = r.ReadInt32();
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
        w.Write(MemoryCount);
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
        MemoryCount = 0;
    }

    #endregion
}
