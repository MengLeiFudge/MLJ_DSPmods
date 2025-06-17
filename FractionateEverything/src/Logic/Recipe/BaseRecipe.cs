using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.LogUtils;
using Random = System.Random;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类，所有分馏塔配方的基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRate,
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
    public int InputID { get; } = inputID;

    /// <summary>
    /// 配方基础成功率
    /// </summary>
    public float BaseSuccessRate { get; } = baseSuccessRate;

    /// <summary>
    /// 带有突破和等级加成的成功率
    /// </summary>
    public float SuccessRate => BaseSuccessRate * (1 + Quality * 0.2f + Level * (0.1f + Quality * 0.02f));

    /// <summary>
    /// 配方损毁率
    /// </summary>
    public float DestroyRate => (0.2f - BaseSuccessRate)
                                * (0.5f + (float)Math.Log10(itemValueDic[InputID] + 1) / 5.0f)
                                * (1 - Quality * 0.2f - Level * (0.1f + Quality * 0.02f));

    /// <summary>
    /// 配方主产物信息，概率之和必须为100%。
    /// 当判定成功时，必定输出且仅输出其中一项。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputMain { get; set; } = outputMain;

    /// <summary>
    /// 配方额外输出产物信息。
    /// 当判定成功时，该列表内每一项分别判定是否成功。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputAppend { get; set; } = outputAppend;

    /// <summary>
    /// 获取某次输出的执行结果
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="successRatePlus">增产剂对成功率的加成</param>
    /// <returns>损毁返回null，无变化反馈空字典，成功返回输出产物</returns>
    public virtual Dictionary<int, int> GetOutputs(ref uint seed, float successRatePlus) {
        seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
        if (seed / 2147483646.0 < DestroyRate) {
            AddExp((int)Math.Ceiling(Math.Log10(1 + itemValueDic[OutputMain[0].OutputID]) * 0.5));
            return null;
        }
        Dictionary<int, int> dic = [];
        seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
        if (seed / 2147483646.0 >= SuccessRate * successRatePlus) {
            return dic;
        }
        //主输出判定
        seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
        double ratio = seed / 2147483646.0;
        float ratioMain = 0.0f;//用于累计概率
        foreach (var outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRate;
            if (ratio <= ratioMain) {
                //整数部分必定输出，小数部分根据概率判定确定是否输出
                int count = (int)Math.Ceiling(outputInfo.OutputCount - 0.0001f);
                float leftCount = outputInfo.OutputCount - count;
                if (leftCount > 0.0001f) {
                    seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
                    if (seed / 2147483646.0 < leftCount) {
                        count++;
                    }
                }
                //由于此处必定是第一个key，所以直接添加
                dic[outputInfo.OutputID] = count;
                outputInfo.OutputTotalCount += count;
                AddExp((int)Math.Ceiling(Math.Log10(1 + itemValueDic[outputInfo.OutputID]) * count));
                break;
            }
        }
        //附加输出判定
        foreach (var outputInfo in OutputAppend) {
            seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
            if (seed / 2147483646.0 <= outputInfo.SuccessRate) {
                int count = (int)Math.Ceiling(outputInfo.OutputCount - 0.0001f);
                float leftCount = outputInfo.OutputCount - count;
                if (leftCount > 0.0001f) {
                    seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
                    if (seed / 2147483646.0 < leftCount) {
                        count++;
                    }
                }
                if (dic.TryGetValue(outputInfo.OutputID, out int currentValue)) {
                    dic[outputInfo.OutputID] = currentValue + count;
                } else {
                    dic.Add(outputInfo.OutputID, count);
                }
                outputInfo.OutputTotalCount += count;
                AddExp((int)Math.Ceiling(Math.Log10(1 + itemValueDic[outputInfo.OutputID]) * count));
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

    /// <summary>
    /// 品质对应的颜色数组
    /// </summary>
    public static readonly Color[] QualityColors = [
        new(0.65f, 0.65f, 0.65f),// 0 - 灰色 - 未解锁
        new(0.90f, 0.90f, 0.90f),// 1 - 白色 - 稍微灰一点以减少刺眼
        new(0.50f, 0.85f, 0.50f),// 2 - 绿色 - 更深的绿色
        new(0.45f, 0.65f, 0.95f),// 3 - 蓝色 - 更深的蓝色
        new(0.75f, 0.45f, 0.85f),// 4 - 紫色 - 更深的紫色
        new(0.95f, 0.45f, 0.45f),// 5 - 红色 - 更深的红色
        new(0.65f, 0.65f, 0.65f),// 6 - 未使用
        new(0.95f, 0.85f, 0.40f),// 7 - 金色 - 更深的金色
    ];

    /// <summary>
    /// 获取当前品质对应的颜色
    /// </summary>
    public Color QualityColor =>
        Quality >= 0 && Quality < QualityColors.Length ? QualityColors[Quality] : QualityColors[0];

    /// <summary>
    /// 配方等级
    /// </summary>
    /// <details>
    /// 未解锁时为0。解锁之后，最低为1，最高为3 + Quality。
    /// </details>
    public int Level { get; set; } = 0;

    /// <summary>
    /// 经验值
    /// </summary>
    /// <details>
    /// 达到下一级所需经验会自动升级。到达等级上限仍可获取经验，突破时多余经验会按照一定比例转化。
    /// </details>
    public long Experience { get; set; } = 0;

    /// <summary>
    /// 下一级所需经验
    /// </summary>
    public long NextLevelExperience => (long)(1000 * Math.Pow(1.8, Quality - 1) * Math.Pow(1.4, Level - 1));

    /// <summary>
    /// 指示是否满足突破的前置等级条件
    /// </summary>
    public bool CanBreakthrough => Level >= 3 + Quality && Experience >= NextLevelExperience;

    /// <summary>
    /// 添加经验
    /// </summary>
    public void AddExp(long exp) {
        // LogDebug($"Quality{Quality} Lv{Level} ({Experience} + {exp}/{NextLevelExperience})");
        Experience += exp;
        if (!CanBreakthrough && Experience >= NextLevelExperience) {
            Experience -= NextLevelExperience;
            Level++;
            LogDebug($"Level Up! Quality{Quality} Lv{Level} ({Experience}/{NextLevelExperience})");
        }
        if (CanBreakthrough) {
            TryBreakQuality();
        }
    }

    private static Random random = new();

    /// <summary>
    /// 配方回响个数。
    /// </summary>
    public int MemoryCount { get; set; } = 0;

    /// <summary>
    /// 突破配方品质
    /// </summary>
    /// <returns>是否突破成功</returns>
    public virtual bool TryBreakQuality() {
        if (!CanBreakthrough) {
            return false;
        }
        float successRate = 1.0f - (Quality - 1) * 0.1f;
        bool success = random.NextDouble() < successRate;
        if (success) {
            Experience -= NextLevelExperience;
            Experience = (int)(Experience * 0.7f);
            Level = 1;
            Quality++;
            //红到金是品质+2
            if (Quality == 6) {
                Quality++;
            }
            AddExp(0);
            LogDebug($"Quality broke success! Quality{Quality} Lv{Level} ({Experience}/{NextLevelExperience})");
            return true;
        } else {
            AddExp(-NextLevelExperience / 10);
            LogDebug($"Quality broke fail! Quality{Quality} Lv{Level} ({Experience}/{NextLevelExperience})");
            return false;
        }
    }

    #endregion

    #region IModCanSave

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public virtual void Export(BinaryWriter w) {
        int byteCount = 4 + (4 + OutputMain.Count * (4 + 4)) + (4 + OutputAppend.Count * (4 + 4)) + 4 + 4 + 8;
        w.Write(byteCount);
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
        w.Write(Experience);
        w.Write(MemoryCount);

        // 子类特定数据由重写的方法处理
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public virtual void Import(BinaryReader r) {
        int byteCount = r.ReadInt32();
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
        Quality = r.ReadInt32();
        Level = r.ReadInt32();
        Experience = r.ReadInt64();
        AddExp(0);
        MemoryCount = r.ReadInt32();

        // 子类特定数据由重写的方法处理
    }

    #endregion
}
