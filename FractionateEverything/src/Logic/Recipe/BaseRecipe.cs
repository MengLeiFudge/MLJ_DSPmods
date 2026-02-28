using System;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility;
using FE.Logic.Manager;
using NebulaAPI;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRate,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name}";
    public string TypeNameWC => TypeName.WithColor(MatrixID - I电磁矩阵);
    public string LvExp => $"{"回响".Translate()} {Echo}    {"等级".Translate()} {Level} "
                           + (Level >= 20 ? "(MAX)" : $"({Exp:F0}/{CurrLevelMaxExp})");
    public string LvExpWC => LvExp.WithColor(MatrixID - I电磁矩阵);

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
    /// 配方进度，最小值为1，最大值为3
    /// </summary>
    public float Progress => 1 + 2f * (float)Math.Log(0.1f * (Math.Min(51, Level + Echo) - 1) + 1, 6);
    public bool FullUpgrade => Level + Echo >= 51;
    /// <summary>
    /// 配方成功率
    /// </summary>
    public float SuccessRate => baseSuccessRate * Progress;
    /// <summary>
    /// 配方损毁率，强制5%以使增产剂对分馏效果有明显提升
    /// </summary>
    public float DestroyRate => 0.05f;

    /// <summary>
    /// 主产物信息，概率之和必须为100%。
    /// 当判定成功时，必定输出且仅输出其中一项。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputMain => outputMain;

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public virtual float MainOutputCountInc => (Progress - 1) / 4;

    /// <summary>
    /// 副产物信息。
    /// 当判定成功时，该列表内每一项分别判定是否成功。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputAppend => outputAppend;

    /// <summary>
    /// 附加产物概率增幅
    /// </summary>
    public virtual float AppendOutputRatioInc => (Progress - 1) / 4;

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="quality">输入物品的品质，0到5</param>
    /// <param name="seed">随机数种子</param>
    /// <param name="pointsBonus">增产剂加成</param>
    /// <param name="successRateBonus">配方成功率加成</param>
    /// <param name="mainOutputCountBonus">主产物数目加成</param>
    /// <param name="appendOutputRateBonus">副产物概率加成</param>
    /// <returns>损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</returns>
    public virtual List<ProductOutputInfo> GetOutputs(byte quality, ref uint seed, float pointsBonus,
        float successRateBonus, float mainOutputCountBonus, float appendOutputRateBonus) {
        //损毁
        if (GetRandDouble(ref seed) < DestroyRate) {
            RewardExp(1);
            return null;
        }
        //无变化
        if (GetRandDouble(ref seed) >= SuccessRate * (1 + pointsBonus) * (1 + successRateBonus)) {
            return ProcessManager.emptyOutputs;
        }
        //成功产出
        List<ProductOutputInfo> list = [];
        //主输出判定，由于主输出概率之和为100%，所以必定输出且只会输出其中一个
        double ratio = GetRandDouble(ref seed);
        float ratioMain = 0.0f;//用于累计概率
        int qualityMultiplier = (int)Math.Pow(10, quality);
        foreach (var outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRate;
            if (ratio <= ratioMain) {
                //整数部分必定输出，小数部分根据概率判定确定是否输出
                float countAvg = outputInfo.OutputCount * (1 + MainOutputCountInc) * (1 + mainOutputCountBonus);
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(true, outputInfo.OutputID * qualityMultiplier, countReal));
                outputInfo.OutputTotalCount += countReal;
                RewardExp(countReal);
                break;
            }
        }
        //附加输出判定，每一项依次判定，互不影响
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed)
                <= outputInfo.SuccessRate * (1 + AppendOutputRatioInc) * (1 + appendOutputRateBonus)) {
                float countAvg = outputInfo.OutputCount;
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(false, outputInfo.OutputID * qualityMultiplier, countReal));
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
    /// 回响，第二次抽到此配方后每次+1
    /// </summary>
    public int Echo { get; set; } = 0;
    /// <summary>
    /// 等级，初始为0，抽到之后变为1，之后只能通过经验升级
    /// </summary>
    public int Level { get; set; } = 0;
    /// <summary>
    /// 经验值
    /// </summary>
    public float Exp { get; private set; } = 0;
    /// <summary>
    /// 升级所需经验
    /// </summary>
    public int CurrLevelMaxExp => (int)(1000 * Math.Pow(2, Level - 1));
    /// <summary>
    /// 是否未解锁
    /// </summary>
    public bool Locked => Level <= 0;
    /// <summary>
    /// 是否已解锁
    /// </summary>
    public bool Unlocked => Level > 0;
    /// <summary>
    /// 回响是否已达上限
    /// </summary>
    public bool IsMaxEcho => Echo >= 40;
    /// <summary>
    /// 等级是否已达上限
    /// </summary>
    public bool IsMaxLevel => Level >= 20;

    /// <summary>
    /// 通过某种方式（例如抽奖，科技奖励等）获取到该配方。
    /// 如果配方未解锁，则解锁此配方；如果已解锁，则回响数目+1，并检查是否可突破。
    /// </summary>
    public void RewardEcho(bool manual = false) {
        lock (this) {
            if (Locked) {
                Level = 1;
            } else if (!IsMaxEcho) {
                Echo++;
            } else {
                return;
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 1));
        }
    }

    /// <summary>
    /// 沙盒模式修改回响
    /// </summary>
    public void ChangeEchoTo(int targetEcho, bool manual = false) {
        lock (this) {
            Echo = Math.Max(0, Math.Min(40, targetEcho));
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 2,
                targetEcho));
        }
    }

    /// <summary>
    /// 添加经验，同时触发升级与突破
    /// </summary>
    public void RewardExp(float exp, bool manual = false) {
        lock (this) {
            if (IsMaxLevel) {
                return;
            }
            Exp += exp;
            if (Exp >= CurrLevelMaxExp) {
                Exp -= CurrLevelMaxExp;
                Level++;
                if (IsMaxLevel) {
                    Exp = 0;
                }
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 3,
                exp));
        }
    }

    /// <summary>
    /// 沙盒模式修改等级
    /// </summary>
    public void ChangeLevelTo(int targetLevel, bool manual = false) {
        lock (this) {
            Level = Math.Max(0, Math.Min(20, targetLevel));
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 4,
                targetLevel));
        }
    }

    #endregion

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
        Echo = Math.Max(0, Math.Min(40, r.ReadInt32()));
        Level = Math.Max(0, Math.Min(20, r.ReadInt32()));
        Exp = Math.Max(0, r.ReadSingle());
        RewardExp(0);//触发升级、突破判断
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
        w.Write(Echo);
        w.Write(Level);
        w.Write(Exp);
        // 子类特定数据由重写的方法处理
    }

    public virtual void IntoOtherSave() {
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
        Echo = 0;
        Level = 0;
        Exp = 0;
    }

    #endregion
}
