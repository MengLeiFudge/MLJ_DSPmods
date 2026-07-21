using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Technology;

/// <summary>
/// 标识固定塔型科技节点可投影到运行时的工程能力。
/// </summary>
public enum AncientTechEffectType {
    FluidOutputStacking,
    ProductOutputStacking,
    FractionationForever,
    MainOutputLock,
    ByproductDiscard,
}

/// <summary>
/// 定义科技树中的一个固定节点及其运行投影效果。
/// </summary>
public sealed class AncientTechNodeDefinition(
    string nodeKey,
    string displayNameKey,
    ERecipe towerType,
    int cost,
    AncientTechEffectType effectType,
    string prerequisiteNodeKey = null,
    bool runtimeImplemented = true) {
    public string NodeKey { get; } = nodeKey;
    public string DisplayNameKey { get; } = displayNameKey;
    public ERecipe TowerType { get; } = towerType;
    public int Cost { get; } = cost;
    public AncientTechEffectType EffectType { get; } = effectType;
    public string PrerequisiteNodeKey { get; } = prerequisiteNodeKey;
    public bool RuntimeImplemented { get; } = runtimeImplemented;
}
