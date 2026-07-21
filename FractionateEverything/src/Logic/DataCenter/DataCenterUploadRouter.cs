using FE.Compatibility.Nebula;
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

        if (manual && AnalysisService.IsAnalysisDataItem(itemId)
            && NebulaMultiplayerModAPI.RequestAnalysisDataUpload(itemId, count)) {
            return;
        }

        if (AnalysisService.TrySubmitDataItem(itemId, count, out int generatedOpportunities)) {
            // 自动上传只在形成新检索机会时发包，避免逐件广播；手动上传立即刷新客户端页面。
            if (manual || generatedOpportunities > 0) {
                NebulaMultiplayerModAPI.BroadcastCivilizationState();
            }
            return;
        }

        DataCenterInventory.AddItemToModData(itemId, count, inc, manual);
        if (FractionatorTowerCatalog.IsActiveFractionator(itemId)) {
            TechManager.CheckTechUnlockCondition(itemId);
        }
    }
}
