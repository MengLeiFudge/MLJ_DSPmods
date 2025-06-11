using System;
using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.ItemManager;

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
    public int InputID { get; private set; } = inputID;

    /// <summary>
    /// 配方基础成功率，暂定最高80%。平时计算不使用此值。
    /// </summary>
    public float BaseSuccessRate { get; private set; } = baseSuccessRate;

    /// <summary>
    /// 配方损毁率，由基础成功率计算得到
    /// </summary>
    public float DestroyRate => CalculateDestroyRate();

    /// <summary>
    /// 计算配方损毁率，可在子类中重写
    /// </summary>
    public virtual float CalculateDestroyRate() {
        //假设每秒处理x份原料（x范围1/12到1/3，原料价值越高x越低），成功率为p（BaseSuccessRate），那么1s就会成功px，损毁(1-p)x。
        //假设每秒通过y份原料（以无堆叠最高带子速率为准，例如原版为1800），损毁率为q，那么1s就会损毁qy。
        //(1-p)x=qy，q=(1-p)x/y
        return (1 - BaseSuccessRate) * (1.0f / (float)Math.Log(itemValueDic[InputID])) / MaxBeltSpeed;
    }

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
            return null;
        }
        Dictionary<int, int> dic = [];
        seed = (uint)((ulong)(seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
        if (seed / 2147483646.0 >= BaseSuccessRate * successRatePlus) {
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
            }
        }
        return dic;
    }

    #endregion

    #region 配方解锁

    /// <summary>
    /// 解锁状态
    /// </summary>
    public bool IsUnlocked { get; set; } = false;

    #endregion

    #region 配方等级与星级

    /// <summary>
    /// 配方等级（1-5）
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// 配方星级/品质（突破后增加）
    /// </summary>
    public int Star { get; set; } = 1;

    /// <summary>
    /// 经验值，成功输出+10，不成功+1
    /// </summary>
    public long Experience { get; set; } = 0;

    /// <summary>
    /// 下一级所需经验
    /// </summary>
    public long NextLevelExperience => CalculateNextLevelExperience();

    /// <summary>
    /// 计算下一级所需经验，可在子类中重写
    /// </summary>
    protected virtual long CalculateNextLevelExperience() {
        return (long)(1000 * Math.Pow(Star + 2, Level));
    }

    /// <summary>
    /// 添加经验
    /// </summary>
    public virtual void AddExp(int exp) {
        Experience += exp;
    }

    /// <summary>
    /// 添加经验
    /// </summary>
    public virtual void AddExp(bool success) {
        double exp = itemValueDic[InputID];
        if (!success) {
            exp *= 0.1;
        }
        Experience += (int)Math.Ceiling(exp);
    }

    /// <summary>
    /// 添加经验
    /// </summary>
    /// <param name="amount">经验值</param>
    /// <returns>是否升级</returns>
    public virtual bool AddExperience(long amount) {
        if (Level >= 5 && Star >= 6)// 最高等级和星级
            return false;

        Experience += amount;

        if (Experience >= NextLevelExperience) {
            LevelUp();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 升级配方
    /// </summary>
    protected virtual void LevelUp() {
        if (Level < 5) {
            Level++;
            Experience = 0;
            // 升级后提升基础成功率
            BaseSuccessRate *= 1.2f;
        }
    }

    /// <summary>
    /// 突破配方
    /// </summary>
    /// <param name="resonanceCount">使用的配方回响数量</param>
    /// <returns>是否突破成功</returns>
    public virtual bool Breakthrough(int resonanceCount) {
        if (Level < 5 || Star >= 6)// 星级上限为6（白、绿、蓝、紫、红、金）
            return false;

        // 计算突破成功率，星级越高成功率越低
        float successRate = 1.0f - Star * 0.1f;

        // 回响越多，成功率越高
        successRate += resonanceCount * 0.05f;

        // 随机判断是否突破成功
        bool success = new Random().NextDouble() < successRate;

        if (success) {
            Star++;
            Level = 1;// 重置等级
            Experience = 0;
            BaseSuccessRate *= 1.5f;// 突破后大幅提升基础成功率
        }

        return success;
    }

    #endregion

    #region IModCanSave

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public virtual void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(InputID);
        w.Write(BaseSuccessRate);
        w.Write(OutputMain.Count);
        foreach (OutputInfo info in OutputMain) {
            w.Write(info.SuccessRate);
            w.Write(info.OutputID);
            w.Write(info.OutputCount);
            w.Write(info.OutputTotalCount);
        }
        w.Write(OutputAppend.Count);
        foreach (OutputInfo info in OutputAppend) {
            w.Write(info.SuccessRate);
            w.Write(info.OutputID);
            w.Write(info.OutputCount);
            w.Write(info.OutputTotalCount);
        }
        w.Write(IsUnlocked);
        w.Write(Level);
        w.Write(Star);
        w.Write(Experience);

        // 子类特定数据由重写的方法处理
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
        InputID = r.ReadInt32();
        BaseSuccessRate = r.ReadSingle();
        OutputMain.Clear();
        int outputMainCount = r.ReadInt32();
        for (int i = 0; i < outputMainCount; i++) {
            var info = new OutputInfo(r.ReadSingle(), r.ReadInt32(), r.ReadInt32());
            info.OutputTotalCount = r.ReadInt32();
            OutputMain.Add(info);
        }
        OutputAppend.Clear();
        int outputAppendCount = r.ReadInt32();
        for (int i = 0; i < outputAppendCount; i++) {
            var info = new OutputInfo(r.ReadSingle(), r.ReadInt32(), r.ReadInt32());
            info.OutputTotalCount = r.ReadInt32();
            OutputAppend.Add(info);
        }
        IsUnlocked = r.ReadBoolean();
        Level = r.ReadInt32();
        Star = r.ReadInt32();
        Experience = r.ReadInt64();

        // 子类特定数据由重写的方法处理
    }

    #endregion
}
