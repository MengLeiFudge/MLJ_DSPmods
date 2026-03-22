using FE.Compatibility;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 校验网格索引是否处于当前游戏版本可用范围。
    /// </summary>
    /// <param name="gridIndex">待校验网格索引</param>
    /// <returns>有效返回 true</returns>
    public static bool IsGridIndexValid(int gridIndex) {
        if (GenesisBook.Enable) {
            return gridIndex % 1000 / 100 >= 1
                   && gridIndex % 1000 / 100 <= 7
                   && gridIndex % 100 >= 1
                   && gridIndex % 100 <= 17;
        } else {
            return gridIndex % 1000 / 100 >= 1
                   && gridIndex % 1000 / 100 <= 8
                   && gridIndex % 100 >= 1
                   && gridIndex % 100 <= 14;
        }
    }

    /// <summary>
    /// 校验物品原型的网格索引是否合法。
    /// </summary>
    /// <param name="proto">物品原型</param>
    public static bool GridIndexValid(this ItemProto proto) {
        return IsGridIndexValid(proto.GridIndex);
    }

    /// <summary>
    /// 校验配方原型的网格索引是否合法。
    /// </summary>
    /// <param name="proto">配方原型</param>
    public static bool GridIndexValid(this RecipeProto proto) {
        return IsGridIndexValid(proto.GridIndex);
    }
}
