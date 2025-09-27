namespace GetDspData.Utils;

public enum ERecipeType_DSP {
    None = 0,
    Smelt = 1,
    Chemical = 2,
    Refine = 3,
    Assemble = 4,
    Particle = 5,
    Exchange = 6,
    PhotonStore = 7,
    Fractionate = 8,
    Research = 15,
    Custom = 20,
    星际组装厂 = 21,
}

public enum ERecipeType_GB {
    None = 0,
    Smelt = 1,
    Chemical = 2,
    Refine = 3,
    Assemble = 4,
    Particle = 5,
    Exchange = 6,
    PhotonStore = 7,
    Fractionate = 8,
    标准制造 = 9,
    高精度加工 = 10,
    矿物处理 = 11,
    所有制造 = 12,// 4 + 9 + 10
    垃圾回收 = 14,
    Research = 15,
    高分子化工 = 16,
    所有化工 = 17,// 2 + 3 + 16
    复合制造 = 18,// 4 + 9
    所有熔炉 = 19,// 1 + 11
    Custom = 20,
    星际组装厂 = 21,
}

public enum ERecipeType_OR {
    None = 0,
    Smelt = 1,
    Chemical = 2,
    Refine = 3,
    Assemble = 4,
    Particle = 5,
    Exchange = 6,
    PhotonStore = 7,
    Fractionate = 8,
    太空船坞 = 9,
    粒子打印 = 10,// 4 + 10
    等离子熔炼 = 11,// 1 + 11
    物质重组 = 12,// 4 + 10 + 12
    生物化工 = 14,
    Research = 15,
    高分子化工 = 16,
    所有化工 = 17,// 2 + 3 + 16
    复合制造 = 18,// 4 + 9
    所有熔炉 = 19,// 1 + 11
    Custom = 20,
    星际组装厂 = 21,
}
