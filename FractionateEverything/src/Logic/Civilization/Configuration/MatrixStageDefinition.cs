namespace FE.Logic.Civilization.Configuration;

/// <summary>
/// 定义一个稳定的文明解析阶段，以及该阶段使用的矩阵和实体解析数据。
/// </summary>
public sealed class MatrixStageDefinition(
    string stageKey,
    string displayNameKey,
    int order,
    int matrixItemId,
    int analysisDataItemId) {
    public string StageKey { get; } = stageKey;
    public string DisplayNameKey { get; } = displayNameKey;
    public int Order { get; } = order;
    public int MatrixItemId { get; } = matrixItemId;
    public int AnalysisDataItemId { get; } = analysisDataItemId;
}
