using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class GachaExchangeRate {
    public static readonly (int matrixId, int matrixCost, int ticketId, int ticketCount)[] MatrixRates = [
        (I电磁矩阵, 2, IFE普通抽卡券, 1),
        (I能量矩阵, 1, IFE普通抽卡券, 1),
        (I结构矩阵, 1, IFE普通抽卡券, 2),
        (I信息矩阵, 1, IFE普通抽卡券, 4),
        (I引力矩阵, 1, IFE普通抽卡券, 8),
        (I结构矩阵, 4, IFE精选抽卡券, 1),
        (I信息矩阵, 2, IFE精选抽卡券, 1),
        (I引力矩阵, 1, IFE精选抽卡券, 1),
        (I宇宙矩阵, 1, IFE精选抽卡券, 2),
    ];

    public static readonly (int shardCost, int ticketId, int ticketCount)[] ShardRates = [
        (20, IFE普通抽卡券, 1),
        (100, IFE精选抽卡券, 1),
    ];
}
