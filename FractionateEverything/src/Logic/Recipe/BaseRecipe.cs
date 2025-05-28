using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类，所有分馏塔配方的基类
/// </summary>
public abstract class BaseRecipe {
    /// <summary>
    /// 配方ID
    /// </summary>
    public int InputID { get; set; }

    /// <summary>
    /// 配方名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 配方描述
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 配方等级（1-5）
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 配方星级/品质（突破后增加）
    /// </summary>
    public int Star { get; set; }

    /// <summary>
    /// 累计使用次数
    /// </summary>
    public long UsageCount { get; set; }

    /// <summary>
    /// 基础成功率
    /// </summary>
    public float BaseSuccessRate { get; set; }

    /// <summary>
    /// 经验值
    /// </summary>
    public long Experience { get; set; }

    /// <summary>
    /// 解锁状态
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// 下一级所需经验
    /// </summary>
    public long NextLevelExperience => CalculateNextLevelExperience();

    /// <summary>
    /// 特殊属性
    /// </summary>
    public Dictionary<string, object> SpecialAttributes { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// 配方类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 计算下一级所需经验
    /// </summary>
    protected virtual long CalculateNextLevelExperience() {
        // 基础计算公式，可在子类中重写
        return (long)(1000 * Math.Pow(2, Level - 1) * (Star + 1));
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

    /// <summary>
    /// 获取当前成功率（考虑各种加成）
    /// </summary>
    public virtual float GetCurrentSuccessRate(float proliferatorBonus = 0) {
        // 基础成功率 + 增产剂加成
        return Math.Min(BaseSuccessRate + proliferatorBonus, 1.0f);
    }

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public virtual void Export(BinaryWriter w) {
        // 写入基本属性
        w.Write(InputID);
        w.Write(Name ?? string.Empty);
        w.Write(Description ?? string.Empty);
        w.Write(Level);
        w.Write(Star);
        w.Write(UsageCount);
        w.Write(BaseSuccessRate);
        w.Write(Experience);
        w.Write(IsUnlocked);

        // 写入特殊属性
        w.Write(SpecialAttributes.Count);
        foreach (var pair in SpecialAttributes) {
            w.Write(pair.Key);
            if (pair.Value == null) {
                w.Write(0);// 类型标记：null
            } else if (pair.Value is int intValue) {
                w.Write(1);// 类型标记：int
                w.Write(intValue);
            } else if (pair.Value is float floatValue) {
                w.Write(2);// 类型标记：float
                w.Write(floatValue);
            } else if (pair.Value is bool boolValue) {
                w.Write(3);// 类型标记：bool
                w.Write(boolValue);
            } else if (pair.Value is string stringValue) {
                w.Write(4);// 类型标记：string
                w.Write(stringValue);
            } else {
                w.Write(0);// 不支持的类型，写入为null
            }
        }

        // 子类特定数据由重写的方法处理
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public virtual void Import(BinaryReader r) {
        // 读取基本属性
        InputID = r.ReadInt32();
        Name = r.ReadString();
        Description = r.ReadString();
        Level = r.ReadInt32();
        Star = r.ReadInt32();
        UsageCount = r.ReadInt64();
        BaseSuccessRate = r.ReadSingle();
        Experience = r.ReadInt64();
        IsUnlocked = r.ReadBoolean();

        // 读取特殊属性
        int specialAttrCount = r.ReadInt32();
        SpecialAttributes.Clear();
        for (int i = 0; i < specialAttrCount; i++) {
            string key = r.ReadString();
            int valueType = r.ReadInt32();
            object value = null;

            switch (valueType) {
                case 1:// int
                    value = r.ReadInt32();
                    break;
                case 2:// float
                    value = r.ReadSingle();
                    break;
                case 3:// bool
                    value = r.ReadBoolean();
                    break;
                case 4:// string
                    value = r.ReadString();
                    break;
                // 默认值为null
            }

            SpecialAttributes[key] = value;
        }

        // 子类特定数据由重写的方法处理
    }
}
