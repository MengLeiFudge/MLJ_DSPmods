using CommonAPI.Systems;
using UnityEngine;

using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class StationManager
{
    public static void AddStations()
    {
        // 行星物流运输站配方
        var stationRecipe = LDB.recipes.Select(R行星内物流运输站);
        // 行星物流运输站物品
        var stationItem = LDB.items.Select(I行星内物流运输站);
        // 行星物流运输站模型
        var stationModel = LDB.models.Select(M行星内物流运输站);
        
        // TODO 替换资产
        var iconPath = stationItem.IconPath;
        var prefabPath = stationModel.PrefabPath;
        
        var rgb = Color.HSVToRGB(0.6174f, 0.6842f, 0.9686f);
        // 注册物品
        var itemProto = ProtoRegistry.RegisterItem(
            IFE交互物流塔, 
            "交互物流塔", 
            """
            供应 = 从数据中心下载到塔里，然后提供出去
            需求 = 需求物品到塔里，然后上传数据中心
            仓储 = 维持数目为上限的一半
            """, 
            iconPath, 
            FractionateEverything.tab分馏 * 1000 + 308, 
            stationItem.StackSize, 
            stationItem.Type, 
            ProtoRegistry.GetDefaultIconDesc(rgb, Color.gray)
            );
        // 添加新物品到建筑栏
        BuildBarTool.BuildBarTool.SetBuildBar(itemProto, 5, itemProto.GridIndex % 10, true);
        // 注册配方
        var recipeProto = ProtoRegistry.RegisterRecipe(
            RFE交互物流塔, 
            stationRecipe.Type, 
            60, 
            [IFE交互塔, I行星内物流运输站],
            [1, 1],
            [IFE交互物流塔],
            [1], 
            itemProto.Description, 
            TFE分馏数据中心, 
            itemProto.GridIndex, 
            itemProto.Name, 
            itemProto.IconPath
            );
        recipeProto.IconPath = "";
        recipeProto.NonProductive = true;
        
        ProtoRegistry.RegisterModel(
            MFE交互物流塔, 
            itemProto, 
            prefabPath, 
            null, 
            stationItem.DescFields, 
            0
            ).HpMax = stationModel.HpMax;
    }
}