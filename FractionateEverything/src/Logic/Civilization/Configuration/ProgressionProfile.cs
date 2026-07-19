using System.Collections.Generic;

namespace FE.Logic.Civilization.Configuration;

/// <summary>
/// 保存当前模组组合使用的不可变矩阵阶段轴。
/// </summary>
public sealed class ProgressionProfile(
    string profileId,
    int version,
    IReadOnlyList<MatrixStageDefinition> stages) {
    private readonly Dictionary<string, MatrixStageDefinition> stagesByKey = BuildStageKeyIndex(stages);
    private readonly Dictionary<int, MatrixStageDefinition> stagesByMatrix = BuildItemIndex(stages, false);
    private readonly Dictionary<int, MatrixStageDefinition> stagesByAnalysisData = BuildItemIndex(stages, true);

    public string ProfileId { get; } = profileId;
    public int Version { get; } = version;
    public IReadOnlyList<MatrixStageDefinition> Stages { get; } = stages;

    public MatrixStageDefinition GetStage(string stageKey) =>
        stageKey != null && stagesByKey.TryGetValue(stageKey, out MatrixStageDefinition stage) ? stage : null;

    public MatrixStageDefinition GetStageByMatrixItem(int matrixItemId) =>
        stagesByMatrix.TryGetValue(matrixItemId, out MatrixStageDefinition stage) ? stage : null;

    public MatrixStageDefinition GetStageByAnalysisDataItem(int itemId) =>
        stagesByAnalysisData.TryGetValue(itemId, out MatrixStageDefinition stage) ? stage : null;

    public MatrixStageDefinition GetStageByOrder(int order) {
        if (order < 0 || order >= Stages.Count) {
            return null;
        }
        return Stages[order];
    }

    private static Dictionary<string, MatrixStageDefinition> BuildStageKeyIndex(
        IReadOnlyList<MatrixStageDefinition> stages) {
        Dictionary<string, MatrixStageDefinition> result = [];
        foreach (MatrixStageDefinition stage in stages) {
            result[stage.StageKey] = stage;
        }
        return result;
    }

    private static Dictionary<int, MatrixStageDefinition> BuildItemIndex(
        IReadOnlyList<MatrixStageDefinition> stages, bool useAnalysisData) {
        Dictionary<int, MatrixStageDefinition> result = [];
        foreach (MatrixStageDefinition stage in stages) {
            result[useAnalysisData ? stage.AnalysisDataItemId : stage.MatrixItemId] = stage;
        }
        return result;
    }
}
