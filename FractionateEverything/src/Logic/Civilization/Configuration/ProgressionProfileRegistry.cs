using System.Collections.Generic;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Configuration;

/// <summary>
/// 在全部原型加载后构建当前存档采用的文明阶段配置。
/// </summary>
public static class ProgressionProfileRegistry {
    public static ProgressionProfile Current { get; private set; }

    public static void Initialize() {
        Current = new ProgressionProfile("vanilla-compatible", 1, new List<MatrixStageDefinition> {
            new("electromagnetic", "文明阶段-电磁", 0, I电磁矩阵, IFE电磁精华),
            new("energy", "文明阶段-能量", 1, I能量矩阵, IFE能量精华),
            new("structure", "文明阶段-结构", 2, I结构矩阵, IFE结构精华),
            new("information", "文明阶段-信息", 3, I信息矩阵, IFE信息精华),
            new("gravity", "文明阶段-引力", 4, I引力矩阵, IFE引力精华),
            new("universe", "文明阶段-宇宙", 5, I宇宙矩阵, IFE宇宙精华),
        });
    }
}
