# Logic/Recipe — Recipe Hierarchy

10 files. `BaseRecipe` is the abstract root; never modify it directly.

## Class Hierarchy

```
BaseRecipe (abstract)
├── BuildingTrainRecipe   — 原胚 → 分馏塔
├── MineralCopyRecipe     — 1A → 2A (矿物复制)
├── PointAggregateRecipe  — 增产点数聚集 (overrides GetOutputs)
├── ConversionRecipe      — 1A → X·B + Y·C (转化，单路锁定)
└── RectificationRecipe  — 精馏 (物品→奖券/精华/核心，Level越高副产物概率越高)

VanillaRecipe             — NOT a BaseRecipe subclass; wraps vanilla recipe upgrades
ERecipe (enum)            — recipe type enum + extension methods
OutputInfo                — probability/count descriptor for one output slot
ProductOutputInfo         — runtime output result from GetOutputs()
```

## BaseRecipe Interface (do not alter)

```csharp
public abstract class BaseRecipe {
    public abstract ERecipe RecipeType { get; }
    public int InputID { get; }                 // input item ID
    public int MatrixID { get; }                // unlock cost matrix ID
    public int Level { get; set; }              // -1 = locked, 0+ = unlocked
    public bool IsMaxLevel { get; }
    public bool Locked => Level < 0;
    public List<OutputInfo> OutputMain   { get; }  // primary outputs
    public List<OutputInfo> OutputAppend { get; }  // side products

    public virtual void GetOutputs(ref int inputCount, float fluidInputInc,
        out List<ProductOutputInfo> outputs, out int outputInc);
    public virtual void Import(BinaryReader r);  // block format: ("OutputMain", "OutputAppend", "Meta")
    public virtual void Export(BinaryWriter w);  // block format: same tags; subclasses may add extra blocks
}
```

## Adding a New Recipe Type

1. Create `XxxRecipe.cs` inheriting `BaseRecipe`
2. Override `RecipeType` → return new `ERecipe` value (add to `ERecipe.cs`)
3. Override `GetOutputs()` only if default distribution is wrong
4. Add `public static void CreateAll()` — instantiate and register via `RecipeManager.AddRecipe()`
5. Call `XxxRecipe.CreateAll()` from `RecipeManager.AddFracRecipes()`

## Key Rules

- `BaseRecipe.GetOutputs()` is the shared hot path — never modify it; subclass instead
- `fluidInputInc` must be passed through to `outputInc` (proliferator point flow)
- `OutputMain` vs `OutputAppend`: primary product → `OutputMain`, byproduct → `OutputAppend`
- `ConversionRecipe.CurrentLockedOutputId` is static (not thread-safe); don't parallelize recipe ticks
- `RectificationRecipe` overrides `GetOutputs()` to add level-dependent essence/core byproducts
