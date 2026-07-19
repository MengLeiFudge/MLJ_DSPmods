using FE.Logic.Civilization.Analysis;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Progression;

namespace FE.Logic.DataCenter;

/// <summary>
/// 处理玩家和建筑发起的实体上传：解析数据转为进度，其他物品进入数据中心库存。
/// </summary>
public static class DataCenterUploadRouter {
    public static void Upload(int itemId, int count, int inc = 0, bool manual = false) {
        Upload(itemId, (long)count, inc, manual);
    }

    public static void Upload(int itemId, long count, long inc = 0, bool manual = false) {
        if (count <= 0) {
            return;
        }

        if (AnalysisService.TrySubmitDataItem(itemId, count, out _)) {
            return;
        }

        DataCenterInventory.AddItemToModData(itemId, count, inc, manual);
        if (FractionatorTowerCatalog.IsActiveFractionator(itemId)) {
            TechManager.CheckTechUnlockCondition(itemId);
        }
    }
}
