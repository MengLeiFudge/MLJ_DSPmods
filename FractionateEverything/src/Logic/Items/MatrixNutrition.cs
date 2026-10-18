using System;
using System.Collections.Generic;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>所有生命塔共享的矩阵营养目录，资格来自原型，能量与速度来自唯一物品价值。</summary>
internal static class MatrixNutrition {
    private static readonly Dictionary<int, (double Energy, float Speed)> Foods = [];
    /// <summary>当前目录中最大的速度倍率，供网络输入边界校验。</summary>
    public static float MaximumSpeed { get; private set; } = 1f;

    /// <summary>在最终估值完成后建立只读营养目录，热路径不再查类型或记录日志。</summary>
    public static void Initialize() {
        Foods.Clear();
        MaximumSpeed = 1f;
        double baseline = ItemManager.itemValue[I电磁矩阵];
        if (!ValidValue(baseline)) { throw new InvalidOperationException("电磁矩阵缺少有效营养估值。"); }
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item == null || item.Type != EItemType.Matrix && item.ID != I黑雾矩阵) { continue; }
            double value = item.ID > 0 && item.ID < ItemManager.itemValue.Length ? ItemManager.itemValue[item.ID] : 0d;
            if (!ValidValue(value)) {
                LogWarning($"矩阵{item.ID}缺少有限正估值，不能用于生命塔供养。");
                continue;
            }
            double ratio = value / baseline;
            double energy = 5.25d * Math.Sqrt(ratio);
            float speed = (float)Math.Pow(ratio, 0.15d);
            Foods.Add(item.ID, (energy, speed));
            MaximumSpeed = Math.Max(MaximumSpeed, speed);
        }
    }

    /// <summary>当前原型与估值是否共同允许作为食物。</summary>
    public static bool IsFood(int itemId) => Foods.ContainsKey(itemId);

    /// <summary>返回单件未增产矩阵的能量，无效物品为零。</summary>
    public static double GetEnergy(int itemId) => Foods.TryGetValue(itemId, out var food) ? food.Energy : 0d;

    /// <summary>返回处理速度倍率；空食物槽使用基础速度。</summary>
    public static float GetSpeed(int itemId) => Foods.TryGetValue(itemId, out var food) ? food.Speed : 1f;

    private static bool ValidValue(double value) => value > 0d && value < ItemManager.maxValue
        && !double.IsNaN(value) && !double.IsInfinity(value);
}
