using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class GachaExchangeRate {
    public static readonly (int matrixId, int matrixCost, int ticketId, int ticketCount)[] MatrixRates = [
        (I电磁矩阵, 20, IFE普通抽卡券, 1),
        (I能量矩阵, 10, IFE普通抽卡券, 1),
        (I结构矩阵, 5, IFE普通抽卡券, 1),
        (I信息矩阵, 2, IFE普通抽卡券, 1),
        (I引力矩阵, 1, IFE普通抽卡券, 1),
        (I结构矩阵, 50, IFE精选抽卡券, 1),
        (I信息矩阵, 20, IFE精选抽卡券, 1),
        (I引力矩阵, 8, IFE精选抽卡券, 1),
        (I宇宙矩阵, 2, IFE精选抽卡券, 1),
    ];

    public static readonly (int shardCost, int ticketId, int ticketCount)[] ShardRates = [
        (10, IFE普通抽卡券, 1),
        (50, IFE普通抽卡券, 1),
        (200, IFE普通抽卡券, 1),
        (500, IFE精选抽卡券, 1),
        (1000, IFE精选抽卡券, 1),
        (2000, IFE精选抽卡券, 1),
    ];
}
