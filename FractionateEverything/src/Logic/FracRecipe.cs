using System.Collections.Generic;
using System.IO;

namespace FractionateEverything.Logic;

/// <summary>
/// 配方基类。
/// 游戏本身分馏配方无法满足多产物等要求，且刚好分馏配方
/// </summary>
public class FracRecipe {
    #region 构造与基础数据

    /// <summary>
    /// 配方类型。
    /// </summary>
    public readonly FracRecipeType type;
    /// <summary>
    /// 配方的原料ID。
    /// </summary>
    public readonly int inputID;
    /// <summary>
    /// idx0表示损毁id（固定-1），idx1表示主产物id，idx2及之后表示副产物id。
    /// </summary>
    public readonly List<int> outputID;
    /// <summary>
    /// idx0表示损毁概率，idx1表示主产物概率，idx2及之后表示副产物。
    /// </summary>
    public readonly List<float> outputRatio;
    /// <summary>
    /// idx0表示损毁数目（固定-1），idx1表示主产物数目，idx2及之后表示副产物数目。
    /// </summary>
    public readonly List<int> outputNum;
    /// <summary>
    /// 配方的主要输出。
    /// </summary>
    public int mainOutput => outputID[1];

    /// <summary>
    /// 损毁概率。
    /// </summary>
    public float destroyRatio => outputRatio[0];

    /// <summary>
    /// 构造一个有多种输出的分馏配方。
    /// </summary>
    public FracRecipe(FracRecipeType type, int inputID,
        List<int> outputID, List<float> outputRatio, List<int> outputNum, float destroyRatio) {
        this.type = type;
        this.inputID = inputID;
        this.outputID = [-1, ..outputID];
        this.outputRatio = [destroyRatio, ..outputRatio];
        this.outputNum = [-1, ..outputNum];
    }

    /// <summary>
    /// 构造一个有单一输出的分馏配方。
    /// </summary>
    public FracRecipe(FracRecipeType type, int inputID,
        int outputID, float outputRatio, int outputNum, float destroyRatio) {
        this.type = type;
        this.inputID = inputID;
        this.outputID = [-1, outputID];
        this.outputRatio = [destroyRatio, outputRatio];
        this.outputNum = [-1, outputNum];
    }

    /// <summary>
    /// 为该配方新增一种目标产物。目前仅可用于构造分馏配方，因为相关数据不会保存在moddsv文件中。
    /// </summary>
    public FracRecipe AddProduct(int id, float ratio, int num) {
        outputID.Add(id);
        outputRatio.Add(ratio);
        outputNum.Add(num);
        return this;
    }

    #endregion

    #region 解锁与升级

    /// <summary>
    /// 解锁该配方需要的物品ID与对应的数目。
    /// 特别的，如果itemID为矩阵，则配方需要在对应商店抽取。
    /// </summary>
    public Dictionary<int, int> unlockItemDic = [];


    /// <summary>
    /// 指示该配方是否已解锁。可由FracRecipeManager的对应方法来解锁配方。
    /// </summary>
    private bool unlocked = false;

    /// <summary>
    /// 将一个配方设为已解锁状态（无论之前是否已经解锁）。
    /// </summary>
    public FracRecipe Unlock() {
        unlocked = true;
        return this;
    }

    public bool IsUnlocked => unlocked || GameMain.sandboxToolsEnabled;

    //todo: 先添加等级带来的固定提升，再添加等级给的点数的随机提升
    // public List<float> outputRatioFix;
    // public List<int> outputNumFix;

    #endregion

    #region 数据读写（主要存储解锁与升级的数据）

    public void Import(BinaryReader r) {
        unlocked = r.ReadBoolean();
        while (r.ReadInt32() != int.MaxValue) { }
    }

    public void Export(BinaryWriter w) {
        w.Write(unlocked);
        w.Write(int.MaxValue);
    }

    #endregion
}
