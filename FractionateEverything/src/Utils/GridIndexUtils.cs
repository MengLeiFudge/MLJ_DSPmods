using FE.Compatibility;

namespace FE.Utils;

public static partial class Utils {
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

    public static bool GridIndexValid(this ItemProto proto) {
        return IsGridIndexValid(proto.GridIndex);
    }

    public static bool GridIndexValid(this RecipeProto proto) {
        return IsGridIndexValid(proto.GridIndex);
    }
}
