# MLJ_DSPmods Codebase Analysis

**Date**: March 4, 2026  
**Repository**: MLJ_DSPmods (Dyson Sphere Program Mods)  
**Primary Language**: C# (.NET 4.7.2)  
**Framework**: BepInEx 5.4.17 + Unity 2022.3.53

---

## 1. Project Structure Overview

### Root Directory Layout
```
MLJ_DSPmods/
├── FractionateEverything/          # Main mod (万物分馏)
│   ├── src/
│   │   ├── Logic/
│   │   │   ├── Building/           # 7 building definitions
│   │   │   ├── Manager/            # 8 manager classes
│   │   │   ├── Recipe/             # Recipe system
│   │   │   └── UI/                 # User interface
│   │   ├── Compatibility/          # Mod compatibility layer
│   │   └── Utils/                  # Utility functions
│   ├── Assets/                     # Game assets
│   ├── Properties/                 # Assembly metadata
│   └── FractionateEverything.csproj
├── GetDspData/                     # Data export tool
├── AfterBuildEvent/                # Build automation
├── lib/                            # Custom DLLs
│   ├── BuildBarTool.dll
│   ├── DSP_Battle-publicized.dll
│   └── ProjectGenesis-publicized.dll
├── .sisyphus/                      # AI agent framework
│   ├── plans/                      # Development plans
│   └── notepads/                   # Learning records
├── MLJ_DSPmods.sln                 # Solution file
├── DefaultPath.props                # Build configuration
└── AGENTS.md                        # Development guide
```

### Building Definitions (7 classes)
1. **ConversionTower** - Item conversion (1A → XA + YB + ZC)
2. **InteractionTower** - Fractionator proto cultivation
3. **InterstellarInteractionStation** - Interstellar logistics
4. **MineralReplicationTower** - Mineral duplication
5. **PlanetaryInteractionStation** - Planetary logistics
6. **PointAggregateTower** - Production point aggregation
7. **RecycleTower** - Item recycling

### Manager Classes (8 classes)
1. **BuildingManager** - Building registration & translations
2. **ItemManager** - Item ID constants & management
3. **ProcessManager** - Game logic patches & processing
4. **RecipeManager** - Recipe registration & management
5. **RuneManager** - Rune/trait system
6. **StationManager** - Station logic
7. **TechManager** - Technology system
8. **TutorialManager** - Tutorial system

---

## 2. Code Style & Naming Conventions

### Naming Rules (Strict Adherence)

| Element | Convention | Example |
|---------|-----------|---------|
| **Classes** | PascalCase | `ConversionTower`, `ProcessManager` |
| **Methods** | PascalCase | `GetOutputs()`, `UpdateHpAndEnergy()` |
| **Properties** | PascalCase | `EnableVoidSpray`, `MaxStack`, `Level` |
| **Private Fields** | camelCase | `item`, `recipe`, `model` |
| **Static Fields** | PascalCase | `Level`, `color`, `SuccessBoost` |
| **Constants** | UPPER_SNAKE_CASE | `IFE转化塔`, `RFE转化塔` (Chinese allowed) |
| **Local Variables** | camelCase | `fluidInputCount`, `outputList`, `seed` |
| **Namespaces** | PascalCase | `FE.Logic.Building`, `FE.Logic.Recipe` |

### Import Organization (Strict Order)
```csharp
// 1. System namespaces (alphabetical)
using System;
using System.Collections.Generic;
using System.IO;

// 2. Third-party libraries (alphabetical)
using BuildBarTool;
using CommonAPI.Systems;
using HarmonyLib;
using NebulaAPI;
using UnityEngine;

// 3. Internal namespaces (alphabetical, FE prefix)
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;

// 4. Static imports (last)
using static FE.FractionateEverything;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;
```

### Formatting Standards
- **Indentation**: 4 spaces (no tabs)
- **Braces**: Allman style (opening brace on new line)
- **Line Length**: No strict limit, but keep readable
- **Switch Expressions**: Preferred for property getters
- **Array Initializers**: Use `[]` syntax (C# 12)
- **String Interpolation**: Preferred over concatenation
- **Null Checks**: Use `if (LDB.items.Exist(outputId))`
- **Early Returns**: For validation failures

---

## 3. File Structure Pattern

### Building Class Template
```csharp
using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;
using static FE.Logic.Manager.ProcessManager;

namespace FE.Logic.Building;

/// <summary>
/// Building class summary (Chinese comments acceptable)
/// </summary>
public static class BuildingName {
    // Private fields (camelCase)
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    
    // Static color field
    public static Color color = new(0.7f, 0.6f, 0.8f);
    
    // Level system
    public static int Level = 0;
    public static bool EnableFeature => Level >= 3;
    
    // Level-based properties (switch expressions)
    public static int MaxStack => Level switch {
        < 9 => 1,
        _ => 4,
    };
    
    public static float EnergyRatio => Level switch {
        < 1 => 1.0f,
        < 4 => 0.95f,
        _ => 0.5f,
    };
    
    // Energy properties
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }
    
    // Required methods
    public static void AddTranslations() { }
    public static void Create() { }
    public static void SetMaterial() { }
    public static void UpdateHpAndEnergy() { }
}
```

### Key Patterns in Buildings

**Level System**:
- All buildings have a `Level` static property (0-based)
- `EnableXxx` boolean properties gate features
- Switch expressions determine level-based values

**Required Methods**:
1. `AddTranslations()` - Register Chinese/English names
2. `Create()` - Register item, recipe, model
3. `SetMaterial()` - Configure visual materials
4. `UpdateHpAndEnergy()` - Adjust stats based on level

**Material Setup**:
```csharp
public static void SetMaterial() {
    Material m_main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
    Material m_black = model.prefabDesc.lodMaterials[0][1];
    // ... configure LOD materials
    model.prefabDesc.materials = [m_main, m_black];
    model.prefabDesc.lodMaterials = [
        [m_main, m_black, m_glass, m_glass1],
        [m_lod, m_black, m_glass, m_glass1],
        // ...
    ];
}
```

---

## 4. Recipe System Architecture

### BaseRecipe Class (Abstract)
```csharp
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRatio,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    
    // Abstract property
    public abstract ERecipe RecipeType { get; }
    
    // Core properties
    public int InputID => inputID;
    public int MatrixID = 0;
    public float SuccessRatio => baseSuccessRatio;
    public float DestroyRatio => Level switch { /* ... */ };
    
    // Output lists
    public List<OutputInfo> OutputMain => outputMain;
    public List<OutputInfo> OutputAppend => outputAppend;
    
    // Probability properties
    public float RemainInputRatio => Level * 0.08f;
    public float DoubleOutputRatio => Level * 0.05f;
    
    // Core method (can be overridden)
    public virtual void GetOutputs(
        ref uint seed,
        float pointsBonus,
        float successBoost,
        int fluidInputIncAvg,
        ref int fluidInputInc,
        out int inputChange,
        out List<ProductOutputInfo> outputs) { }
}
```

### Recipe Implementation Pattern
```csharp
public class ConversionRecipe : BaseRecipe {
    public override ERecipe RecipeType => ERecipe.Conversion;
    
    public static void CreateAll() {
        // Create recipe chains
        CreateChain([[I配送运输机], [I物流运输机], [I星际物流运输船]]);
        CreateChain([[I电磁矩阵], [I能量矩阵], [I结构矩阵]]);
    }
    
    private static void CreateChain(List<List<int>> itemChain) {
        // Implementation
    }
}
```

### Recipe Types
1. **ConversionRecipe** - Item conversion chains
2. **MineralCopyRecipe** - Mineral duplication
3. **BuildingTrainRecipe** - Building training
4. **ERecipe** - Energy recipes
5. (Others in Logic/Recipe/)

### Output System
- **OutputMain**: Primary outputs (one selected per success)
- **OutputAppend**: Secondary outputs (each independently judged)
- **Probability-based**: Success ratio, destroy ratio, double output
- **Proliferation Points**: `fluidInputInc` tracking

---

## 5. Build Configuration

### Project File (FractionateEverything.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net472</TargetFramework>
        <BepInExPluginGuid>com.menglei.dsp.fe</BepInExPluginGuid>
        <Version>2.3.0</Version>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <LangVersion>latest</LangVersion>
        <RootNamespace>FE</RootNamespace>
    </PropertyGroup>
    
    <ItemGroup>
        <PackageReference Include="BepInEx.Core" Version="5.4.17"/>
        <PackageReference Include="UnityEngine.Modules" Version="2022.3.53"/>
        <PackageReference Include="DysonSphereProgram.Modding.CommonAPI" Version="*-*"/>
        <PackageReference Include="DysonSphereProgram.GameLibs" Version="*-*"/>
        <Reference Include="BuildBarTool">
            <HintPath>..\lib\BuildBarTool.dll</HintPath>
        </Reference>
    </ItemGroup>
</Project>
```

### Key Build Settings
- **Target Framework**: `net472` (Unity compatibility)
- **Unsafe Blocks**: Enabled (`AllowUnsafeBlocks: true`)
- **Language Version**: Latest C# features
- **Root Namespace**: `FE` (FractionateEverything)
- **Platform Target**: x64

### Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| BepInEx.Core | 5.4.17 | Modding framework |
| UnityEngine.Modules | 2022.3.53 | Unity engine |
| CommonAPI | *-* | DSP modding API |
| GameLibs | *-* | Game libraries (publicized) |
| BuildBarTool | Custom | Build bar UI |
| DSP_Battle | Custom | Battle system |
| ProjectGenesis | Custom | Genesis Book compatibility |

### Build Process
1. Compiles to `net472` (Unity 2022.3 compatible)
2. Post-build: Creates ZIP package (Release only)
3. Includes: DLL, icon.png, manifest.json, README.md, CHANGELOG.md

---

## 6. Harmony Patching Pattern

### Patch Structure
```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
    // Postfix logic
}
```

### Naming Convention
- `[ClassName]_[MethodName]_[PatchType]`
- Example: `FractionatorComponent_Import_Postfix`

### Common Patch Types
- **Postfix**: Runs after original method
- **Prefix**: Runs before original method
- **Transpiler**: Modifies IL code

---

## 7. Compatibility Layer

### Location
`FractionateEverything/src/Compatibility/`

### Purpose
- Detect and adapt to other mods
- Conditional feature enabling
- Cross-mod integration

### Example Pattern
```csharp
public static class OrbitalRing {
    public static bool Enable { get; private set; }
    
    public static void Init() {
        // Check if mod is loaded
        Enable = /* detection logic */;
    }
}
```

### Known Compatibility Checks
- **OrbitalRing** - Orbital Ring mod
- **GenesisBook** - Genesis Book mod
- **SmelterMiner** - Smelter/Miner mod
- **CustomCreateBirthStar** - Custom birth star mod

---

## 8. Configuration Files

### DefaultPath.props
- Configures game library paths
- Used by build system
- Example: `DefaultPath.props.example` provided

### No EditorConfig/GlobalConfig
- ✗ No `.editorconfig` file found
- ✗ No `.globalconfig` file found
- ✗ No `.cursorrules` file found
- ✗ No `.cursor/rules/` directory found
- ✗ No `.github/copilot-instructions.md` file found

**Implication**: Code style is enforced through AGENTS.md documentation and team discipline.

---

## 9. Documentation & Comments

### XML Documentation
- Used for public APIs
- Format: `/// <summary>` blocks
- Example:
```csharp
/// <summary>
/// Building class summary
/// </summary>
public static class BuildingName { }
```

### Inline Comments
- Chinese comments are acceptable (Chinese mod)
- Keep concise and relevant
- Document complex algorithms
- Avoid console spam in logging

### Documentation Files
- **AGENTS.md** - Development guide (comprehensive)
- **README.md** - Project overview
- **CHANGELOG.md** - Version history
- **TODO.md** - Task tracking

---

## 10. Testing & Verification

### Current Testing Approach
- ✗ No unit test framework configured
- Manual testing through gameplay
- Build verification is primary quality gate

### Verification Commands
```bash
# Build verification
dotnet build FractionateEverything/FractionateEverything.csproj

# Expected output
# Build succeeded. 0 Warning(s). 0 Error(s).
```

### Quality Gates
1. **Compilation**: Must succeed with 0 errors
2. **Warnings**: Should be 0 (or documented)
3. **Gameplay**: Manual testing in-game
4. **Compatibility**: Test with known mods

---

## 11. Common Pitfalls to Avoid

### Critical Don'ts
1. ❌ **Don't modify `BaseRecipe.GetOutputs`** - Shared by all recipe types
2. ❌ **Don't touch `buffBonus1/2/3`** - Reserved for future use
3. ❌ **Don't add unnecessary Harmony patches** - Use existing code paths
4. ❌ **Don't skip build verification** - Must succeed with 0 errors
5. ❌ **Don't break existing patterns** - Consistency is critical

### Common Mistakes
- Incorrect import ordering
- Inconsistent naming conventions
- Missing XML documentation
- Hardcoded values instead of properties
- Not using switch expressions for level-based logic

---

## 12. Development Workflow

### Adding a New Building
1. Create static class in `Logic/Building/`
2. Follow ConversionTower/InteractionTower pattern
3. Implement: `Level`, `EnableXxx`, `Create()`, `SetMaterial()`, `UpdateHpAndEnergy()`
4. Register in `BuildingManager.AddFractionators()`
5. Add translations in `BuildingManager.AddTranslations()`

### Adding a New Recipe Type
1. Create class inheriting from `BaseRecipe` in `Logic/Recipe/`
2. Override `GetOutputs()` method
3. Implement `RecipeType` property
4. Register in appropriate manager
5. Add to `CreateAll()` method

### Modifying Game Logic
1. Use Harmony patches for game method interception
2. Place patches in appropriate manager classes
3. Follow postfix/prefix convention
4. Test thoroughly in-game
5. Verify build succeeds

---

## 13. AI Agent Framework Integration

### .sisyphus Directory
- **plans/** - Development plans (e.g., `building-enhancement.md`)
- **notepads/** - Learning records and issues
- **evidence/** - Supporting documentation

### Agent Workflow
1. Read existing plans in `.sisyphus/plans/`
2. Follow structured task breakdown
3. Record learnings in notepad files
4. Verify all changes compile successfully
5. Update plan checkboxes when tasks complete

### Framework: oh-my-opencode
- Coordinates multiple AI agents
- Tracks development progress
- Maintains learning history

---

## 14. Key Statistics

| Metric | Value |
|--------|-------|
| **Target Framework** | .NET 4.7.2 |
| **Language Version** | Latest C# |
| **Main Mod Version** | 2.3.0 |
| **Building Classes** | 7 |
| **Manager Classes** | 8 |
| **Recipe Types** | 5+ |
| **Dependencies** | 7 major |
| **Unsafe Blocks** | Enabled |
| **Test Framework** | None (manual) |

---

## 15. Quick Reference

### File Locations
- **Buildings**: `FractionateEverything/src/Logic/Building/`
- **Recipes**: `FractionateEverything/src/Logic/Recipe/`
- **Managers**: `FractionateEverything/src/Logic/Manager/`
- **UI**: `FractionateEverything/src/Logic/UI/`
- **Compatibility**: `FractionateEverything/src/Compatibility/`
- **Utils**: `FractionateEverything/src/Utils/`

### Build Commands
```bash
# Build specific project
dotnet build FractionateEverything/FractionateEverything.csproj

# Build solution
dotnet build MLJ_DSPmods.sln

# Release build
dotnet build -c Release FractionateEverything/FractionateEverything.csproj
```

### Key Constants (ItemManager)
- `IFE转化塔` - Conversion Tower item ID
- `RFE转化塔` - Conversion Tower recipe ID
- `MFE转化塔` - Conversion Tower model ID
- Pattern: `I` (item), `R` (recipe), `M` (model) prefix

---

## Summary

This is a **well-structured, professionally-maintained C# mod** for Dyson Sphere Program with:
- ✅ Consistent naming conventions
- ✅ Clear architectural patterns
- ✅ Comprehensive documentation (AGENTS.md)
- ✅ Modular design (Buildings, Recipes, Managers)
- ✅ Compatibility layer for other mods
- ✅ AI-assisted development framework
- ✅ Build automation and packaging

**Code Quality**: High - follows established patterns, minimal technical debt, well-documented.

