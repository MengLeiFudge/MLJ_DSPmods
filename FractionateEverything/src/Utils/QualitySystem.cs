using System;
using UnityEngine;

namespace FE.Utils;

public static partial class Utils {
    /*
     * 物品ID包含了品质，万及万以上的部分就是品质级别
     * 例如1001表示普通品质（0级品质）的铁矿，101001表示传奇品质10级品质）的铁矿
     * 品质等级目前有10级，0->蓝糖->1->红糖->2->黄糖->3->紫糖->5->绿糖->7->白糖->10
     */

    /// <summary>
    /// 品质等级列表
    /// </summary>
    public static byte[] qualityList = [0, 1, 2, 3, 5, 7, 10];

    public static byte CurrMaxQuality {
        get {
            //获取当前解锁的最高级矩阵
            int matrixID;
            if (GameMain.history.ItemUnlocked(I宇宙矩阵)) {
                matrixID = I宇宙矩阵;
            } else if (GameMain.history.ItemUnlocked(I引力矩阵)) {
                matrixID = I引力矩阵;
            } else if (GameMain.history.ItemUnlocked(I信息矩阵)) {
                matrixID = I信息矩阵;
            } else if (GameMain.history.ItemUnlocked(I结构矩阵)) {
                matrixID = I结构矩阵;
            } else if (GameMain.history.ItemUnlocked(I能量矩阵)) {
                matrixID = I能量矩阵;
            } else if (GameMain.history.ItemUnlocked(I电磁矩阵)) {
                matrixID = I电磁矩阵;
            } else {
                matrixID = I电磁矩阵 - 1;
            }
            //根据矩阵返回最高品质
            return qualityList[matrixID - I电磁矩阵 + 1];
        }
    }

    /// <summary>
    /// 每个品质等级的ID偏移量
    /// </summary>
    private const int QualityIdOffset = 10000;

    /// <summary>
    /// 根据给定的物品ID（无论任何品质）和品质，返回对应品质物品的ID
    /// </summary>
    public static int GetQualityItemId(int itemId, byte quality = 0) {
        int baseId = itemId % QualityIdOffset;
        return baseId + quality * QualityIdOffset;
    }

    /// <summary>
    /// 根据给定的物品ID（无论任何品质），返回该物品的品质
    /// </summary>
    public static byte GetQuality(int itemId) {
        return (byte)(itemId / QualityIdOffset);
    }

    /// <summary>
    /// 获取品质对应的名�?
    /// </summary>
    public static string GetQualityName(byte quality) {
        return quality switch {
            0 => "普通".Translate(),
            1 => "精良".Translate(),
            2 => "稀有".Translate(),
            3 => "罕见".Translate(),
            5 => "史诗".Translate(),
            7 => "传说".Translate(),
            10 => "神话".Translate(),
            _ => "未知".Translate(),
        };
    }

    /// <summary>
    /// 获取品质颜色（用于UI显示）
    /// </summary>
    /// <param name="quality">品质等级</param>
    /// <returns>品质对应的颜色</returns>
    public static Color GetQualityColor(int quality) {
        return quality switch {
            0 => new(1.0f, 1.0f, 1.0f),// 白色 - 普通
            1 => new(0.0f, 1.0f, 0.0f),// 绿色 - 精良
            2 => new(0.0f, 0.5f, 1.0f),// 蓝色 - 稀有
            3 => new(0.6f, 0.0f, 1.0f),// 紫色 - 罕见
            5 => new(1.0f, 0.0f, 0.0f),// 红色 - 史诗
            7 => new(1.0f, 0.5f, 0.0f),// 橙色 - 传说
            10 => new(1.0f, 1.0f, 0.0f),// 金色 - 神话
            _ => new(0.5f, 0.5f, 0.5f),// 灰色 - 未知
        };
    }

    /// <summary>
    /// 按照指定的品质提升率，返回处理后的品质
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="itemId">当前物品ID</param>
    /// <returns>品质提升后的物品ID</returns>
    public static int DetermineQualityIncrease(ref uint seed, int itemId, double rate = 0.124) {
        double rand = GetRandDouble(ref seed);
        byte currentQuality = GetQuality(itemId);
        int currentIndex = Array.IndexOf(qualityList, currentQuality);
        if (currentIndex < 0) {
            return itemId;
        }
        int increase;
        if (rand < rate / 1000000) {
            increase = 6;
        } else if (rand < rate / 100000) {
            increase = 5;
        } else if (rand < rate / 10000) {
            increase = 4;
        } else if (rand < rate / 1000) {
            increase = 3;
        } else if (rand < rate / 100) {
            increase = 2;
        } else if (rand < rate / 10) {
            increase = 1;
        } else {
            increase = 0;
        }
        int newQualityIndex = Math.Min(qualityList.Length - 1, currentIndex + increase);
        byte newQuality = Math.Min(CurrMaxQuality, qualityList[newQualityIndex]);
        return GetQualityItemId(itemId, newQuality);
    }
}
