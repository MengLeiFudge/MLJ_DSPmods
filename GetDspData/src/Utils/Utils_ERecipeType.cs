namespace GetDspData.Utils;

public enum Utils_ERecipeType {
    None = 0,
    Smelt = 1,
    Chemical = 2,
    Refine = 3,
    Assemble = 4,
    Particle = 5,
    Exchange = 6,
    PhotonStore = 7,
    Fractionate = 8,
    GB标准制造 = 9,
    OR太空船坞 = 9,
    GB高精度加工 = 10,
    OR粒子打印 = 10,// 4 + 10
    GB矿物处理 = 11,
    OR等离子熔炼 = 11,// 1 + 11
    GB所有制造 = 12,// 4 + 9 + 10
    OR物质重组 = 12,// 4 + 10 + 12
    GB垃圾回收 = 14,
    OR生物化工 = 14,
    Research = 15,
    GB高分子化工 = 16,
    GB所有化工 = 17,// 2 + 3 + 16
    GB复合制造 = 18,// 4 + 9
    GB所有熔炉 = 19,// 1 + 11
    GBCustom = 20,
    MS星际组装厂 = 21,
}
