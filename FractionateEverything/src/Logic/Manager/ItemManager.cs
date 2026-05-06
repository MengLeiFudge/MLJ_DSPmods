using System.IO;
using FE.Logic.DataCenter;

namespace FE.Logic.Manager;

/// <summary>
/// 迁移期门面：物品原型/价值/阶段逻辑已归入 Logic/Items，
/// 数据中心库存与存档已归入 Logic/DataCenter。旧调用面逐步迁走前由这里转发。
/// </summary>
public static class ItemManager {
    public const float maxValue = Items.ItemManager.maxValue;

    public static readonly float[] itemValue = Items.ItemManager.itemValue;
    public static readonly int[] MainProgressMatrixIds = Items.ItemManager.MainProgressMatrixIds;
    public static readonly int[] itemToMatrix = Items.ItemManager.itemToMatrix;

    public static int[] needs {
        get => Items.ItemManager.needs;
        set => Items.ItemManager.needs = value;
    }

    public static long[] centerItemCount => DataCenterInventory.centerItemCount;
    public static long[] centerItemInc => DataCenterInventory.centerItemInc;

    public static int leftInc {
        get => DataCenterInventory.leftInc;
        set => DataCenterInventory.leftInc = value;
    }

    public static long ManualExtractCount {
        get => DataCenterInventory.ManualExtractCount;
        set => DataCenterInventory.ManualExtractCount = value;
    }

    public static long ManualUploadCount {
        get => DataCenterInventory.ManualUploadCount;
        set => DataCenterInventory.ManualUploadCount = value;
    }

    public static void AddTranslations() => Items.ItemManager.AddTranslations();
    public static void AddCoreItemsAndPrototypes() => Items.ItemManager.AddCoreItemsAndPrototypes();
    public static void CalculateItemValues() => Items.ItemManager.CalculateItemValues();
    public static int GetMatrixStageIndex(int matrixId) => Items.ItemManager.GetMatrixStageIndex(matrixId);
    public static int GetCurrentProgressMatrixId() => Items.ItemManager.GetCurrentProgressMatrixId();
    public static int GetCurrentProgressStageIndex() => Items.ItemManager.GetCurrentProgressStageIndex();

    public static float GetStageDecayFactor(int sourceMatrixId) =>
        Items.ItemManager.GetStageDecayFactor(sourceMatrixId);

    public static int GetRectificationBaseFragmentYield(int matrixId) =>
        Items.ItemManager.GetRectificationBaseFragmentYield(matrixId);

    public static int GetRectificationFragmentYield(int matrixId, float ratio = 1f) =>
        Items.ItemManager.GetRectificationFragmentYield(matrixId, ratio);

    public static void ClassifyItemsToMatrix() => Items.ItemManager.ClassifyItemsToMatrix();
    public static int GetTechTopMatrixID(TechProto tech) => Items.ItemManager.GetTechTopMatrixID(tech);

    public static void Export(BinaryWriter w) => DataCenterInventory.Export(w);
    public static void Import(BinaryReader r) => DataCenterInventory.Import(r);
    public static void IntoOtherSave() => DataCenterInventory.IntoOtherSave();
}
