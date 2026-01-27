using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FE.Utils;

/// <summary>
/// 品质系统工具类
/// 用于管理物品的品质等级和ID转换
/// 品质系统规则：
/// - 基础物品ID范围：1000-10000
/// - 二级品质：基础ID + 10000
/// - 三级品质：基础ID + 20000
/// - 四级品质：基础ID + 30000
/// - 五级品质：基础ID + 40000
/// </summary>
public static class QualitySystem {
    /// <summary>
    /// 品质等级常量
    /// </summary>
    public const int MinQuality = 1;
    public const int MaxQuality = 5;

    /// <summary>
    /// 基础物品ID范围
    /// </summary>
    public const int BaseItemIdMin = 1000;
    public const int BaseItemIdMax = 10000;

    /// <summary>
    /// 每个品质等级的ID偏移量
    /// </summary>
    public const int QualityIdOffset = 10000;

    /// <summary>
    /// 根据基础物品ID和品质等级计算品质物品ID
    /// </summary>
    /// <param name="baseItemId">基础物品ID（1000-10000）</param>
    /// <param name="quality">品质等级（1-5）</param>
    /// <returns>品质物品ID，如果参数无效则返回-1</returns>
    public static int GetQualityItemId(int baseItemId, int quality) {
        if (!IsValidBaseItemId(baseItemId)) {
            return -1;
        }

        if (quality < MinQuality || quality > MaxQuality) {
            return -1;
        }

        // 一级品质就是基础物品本身
        if (quality == 1) {
            return baseItemId;
        }

        return baseItemId + (quality - 1) * QualityIdOffset;
    }

    /// <summary>
    /// 从物品ID提取品质等级
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>品质等级（1-5），如果不是有效的品质物品ID则返回-1</returns>
    public static int GetQualityLevel(int itemId) {
        int baseId = GetBaseItemId(itemId);
        if (!IsValidBaseItemId(baseId)) {
            return -1;
        }

        int offset = itemId - baseId;
        if (offset % QualityIdOffset != 0) {
            return -1;
        }

        int quality = (offset / QualityIdOffset) + 1;
        if (quality < MinQuality || quality > MaxQuality) {
            return -1;
        }

        return quality;
    }

    /// <summary>
    /// 从品质物品ID获取基础物品ID
    /// </summary>
    /// <param name="itemId">物品ID（可以是基础ID或品质ID）</param>
    /// <returns>基础物品ID，如果无法解析则返回-1</returns>
    public static int GetBaseItemId(int itemId) {
        // 如果本身就是基础物品ID
        if (IsValidBaseItemId(itemId)) {
            return itemId;
        }

        // 尝试计算基础ID
        int baseId = itemId % QualityIdOffset;
        if (baseId < BaseItemIdMin) {
            baseId += BaseItemIdMin;
        }

        // 验证计算出的基础ID是否有效
        if (IsValidBaseItemId(baseId)) {
            // 验证原始ID是否可以由这个基础ID生成
            int quality = GetQualityLevel(itemId);
            if (quality >= MinQuality && quality <= MaxQuality) {
                return baseId;
            }
        }

        return -1;
    }

    /// <summary>
    /// 判断ID是否为有效的基础物品ID
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>如果在1000-10000范围内返回true，否则返回false</returns>
    public static bool IsValidBaseItemId(int itemId) {
        return itemId >= BaseItemIdMin && itemId < BaseItemIdMax;
    }

    /// <summary>
    /// 判断ID是否为品质物品ID（二级及以上）
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <returns>如果是二级及以上品质物品返回true，否则返回false</returns>
    public static bool IsQualityItem(int itemId) {
        int quality = GetQualityLevel(itemId);
        return quality >= 2 && quality <= MaxQuality;
    }

    /// <summary>
    /// 获取指定基础物品的所有品质等级ID
    /// </summary>
    /// <param name="baseItemId">基础物品ID</param>
    /// <returns>包含所有品质等级ID的数组（索引0对应1级，索引4对应5级），如果基础ID无效则返回null</returns>
    public static int[] GetAllQualityIds(int baseItemId) {
        if (!IsValidBaseItemId(baseItemId)) {
            return null;
        }

        int[] qualityIds = new int[MaxQuality];
        for (int quality = MinQuality; quality <= MaxQuality; quality++) {
            qualityIds[quality - 1] = GetQualityItemId(baseItemId, quality);
        }

        return qualityIds;
    }

    /// <summary>
    /// 批量生成品质物品ID映射
    /// </summary>
    /// <param name="baseItemIds">基础物品ID数组</param>
    /// <param name="targetQuality">目标品质等级（2-5）</param>
    /// <returns>字典，键为基础物品ID，值为对应品质的物品ID</returns>
    public static Dictionary<int, int> BatchGenerateQualityIds(int[] baseItemIds, int targetQuality) {
        var result = new Dictionary<int, int>();

        if (targetQuality < 2 || targetQuality > MaxQuality) {
            return result;
        }

        foreach (int baseId in baseItemIds) {
            if (IsValidBaseItemId(baseId)) {
                int qualityId = GetQualityItemId(baseId, targetQuality);
                if (qualityId > 0) {
                    result[baseId] = qualityId;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 批量生成所有品质等级的物品ID映射
    /// </summary>
    /// <param name="baseItemIds">基础物品ID数组</param>
    /// <returns>字典，键为基础物品ID，值为包含所有品质等级ID的数组</returns>
    public static Dictionary<int, int[]> BatchGenerateAllQualityIds(int[] baseItemIds) {
        var result = new Dictionary<int, int[]>();

        foreach (int baseId in baseItemIds) {
            if (IsValidBaseItemId(baseId)) {
                int[] qualityIds = GetAllQualityIds(baseId);
                if (qualityIds != null) {
                    result[baseId] = qualityIds;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 从ProtoID类中提取所有基础物品ID
    /// 注意：这个方法需要通过反射或手动维护物品ID列表
    /// </summary>
    /// <returns>所有基础物品ID的列表</returns>
    public static List<int> GetAllBaseItemIds() {
        var baseItemIds = new List<int>();

        // 方法1：遍历1000-10000范围内的所有ID，检查游戏中是否存在该物品
        // 这需要在游戏运行时调用，通过LDB.items.Select(id)来验证

        // 方法2：通过反射从Utils类（ProtoID.cs）中提取所有以"I"开头的常量
        var utilsType = typeof(Utils);
        var fields = utilsType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var field in fields) {
            if (field.Name.StartsWith("I") && field.FieldType == typeof(int)) {
                if (field.GetValue(null) is int value && IsValidBaseItemId(value)) {
                    if (!baseItemIds.Contains(value)) {
                        baseItemIds.Add(value);
                    }
                }
            }
        }

        baseItemIds.Sort();
        return baseItemIds;
    }

    /// <summary>
    /// 获取品质名称（用于显示）
    /// </summary>
    /// <param name="quality">品质等级（1-5）</param>
    /// <returns>品质名称字符串</returns>
    public static string GetQualityName(int quality) {
        return quality switch {
            1 => "普通",
            2 => "优良",
            3 => "精良",
            4 => "史诗",
            5 => "传说",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取品质颜色（用于UI显示）
    /// </summary>
    /// <param name="quality">品质等级（1-5）</param>
    /// <returns>品质对应的颜色</returns>
    public static Color GetQualityColor(int quality) {
        return quality switch {
            1 => new Color(1.0f, 1.0f, 1.0f),// 白色 - 普通
            2 => new Color(0.0f, 1.0f, 0.0f),// 绿色 - 优良
            3 => new Color(0.0f, 0.5f, 1.0f),// 蓝色 - 精良
            4 => new Color(0.6f, 0.0f, 1.0f),// 紫色 - 史诗
            5 => new Color(1.0f, 0.5f, 0.0f),// 橙色 - 传说
            _ => new Color(0.5f, 0.5f, 0.5f)// 灰色 - 未知
        };
    }
}
