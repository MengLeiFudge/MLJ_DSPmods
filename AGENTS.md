# AGENTS.md - MLJ_DSPmods Development Guide

This document provides essential information for AI agents working on this Dyson Sphere Program mod repository.

## Project Overview

This repository contains multiple DSP mods:
- **FractionateEverything** - Main mod with fractionation mechanics
- **GetDspData** - Data export tool for mod developers
- **AfterBuildEvent** - Build automation tool

## Build System

### Prerequisites
- .NET SDK 8.0+
- Dyson Sphere Program game libraries
- BepInEx modding framework

### Build Commands
```bash
# Build specific project
dotnet build FractionateEverything/FractionateEverything.csproj

# Build solution (all projects)
dotnet build MLJ_DSPmods.sln

# Build Release configuration
dotnet build -c Release FractionateEverything/FractionateEverything.csproj
```

### Key Build Files
- `MLJ_DSPmods.sln` - Main solution file
- `FractionateEverything/FractionateEverything.csproj` - Main mod project
- `GetDspData/GetDspData.csproj` - Data export tool
- `AfterBuildEvent/AfterBuildEvent.csproj` - Build automation tool
- `DefaultPath.props` - Path configuration for game libraries

### Dependencies
The project uses:
- **BepInEx.Core** (5.4.17) - Modding framework
- **UnityEngine.Modules** (2022.3.53) - Unity engine
- **DysonSphereProgram.Modding.CommonAPI** - DSP modding API
- **DysonSphereProgram.GameLibs** - Game libraries (publicized)
- Custom DLLs in `lib/` directory

### Build Process Notes
1. Target framework: `net472` (for Unity compatibility)
2. `AllowUnsafeBlocks: true` is enabled
3. Game libraries are publicized (methods made public) for modding access
4. The `AfterBuildEvent` project handles post-build automation

## Code Style Guidelines

### Project Structure
```
FractionateEverything/
├── src/
│   ├── Logic/           # Core game logic
│   │   ├── Building/    # Building definitions
│   │   ├── Manager/     # Game state managers
│   │   ├── Recipe/      # Recipe implementations
│   │   └── UI/          # User interface
│   ├── Compatibility/   # Mod compatibility
│   └── Utils/          # Utility classes
├── Assets/             # Game assets
└── Properties/         # Assembly info
```

### Naming Conventions
- **Classes**: `PascalCase` (e.g., `ProcessManager`, `ConversionRecipe`)
- **Methods**: `PascalCase` (e.g., `GetOutputs`, `UpdateHpAndEnergy`)
- **Properties**: `PascalCase` (e.g., `EnableVoidSpray`, `MaxStack`)
- **Fields**: `camelCase` for private fields (e.g., `item`, `recipe`, `model`)
- **Static fields**: `PascalCase` (e.g., `Level`, `color`)
- **Constants**: `UPPER_SNAKE_CASE` (e.g., `IFE转化塔`, `RFE转化塔`)
- **Local variables**: `camelCase` (e.g., `fluidInputCount`, `outputList`)

### Import Organization
```csharp
// System namespaces first
using System;
using System.Collections.Generic;
using System.IO;

// Third-party libraries
using HarmonyLib;
using NebulaAPI;
using UnityEngine;

// Internal namespaces (alphabetical)
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;

// Static imports last
using static FE.FractionateEverything;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;
```

### File Structure Pattern
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
/// Building class summary
/// </summary>
public static class BuildingName {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.7f, 0.6f, 0.8f);
    
    public static int Level = 0;
    public static bool EnableFeature => Level >= 3;
    
    public static int MaxStack => Level switch {
        < 9 => 1,
        _ => 4,
    };
    
    // ... rest of implementation
}
```

### Formatting Rules
- **Indentation**: 4 spaces (no tabs)
- **Braces**: Allman style (braces on new line)
- **Line length**: No strict limit, but keep readable
- **Switch expressions**: Use when appropriate for property getters
- **Array initializers**: Use `[]` syntax (C# 12)
- **String interpolation**: Preferred over concatenation

### Error Handling
- Minimal try-catch usage (Unity/DSP handles most errors)
- Use null checks: `if (LDB.items.Exist(outputId))`
- Use early returns for validation failures
- Log errors when appropriate (but avoid console spam)

### Unity/DSP-Specific Patterns

#### Harmony Patches
```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
    // Postfix logic
}
```

#### Static Building Classes
All building definitions are static classes with:
- `Level` static property
- `EnableXxx` boolean properties based on level
- Switch expressions for level-based values
- `Create()`, `SetMaterial()`, `UpdateHpAndEnergy()` methods

#### Extension Methods
```csharp
public static int GetLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory) {
    // Extension method logic
}
```

#### Recipe System
- Inherit from `BaseRecipe`
- Override `GetOutputs` method for custom logic
- Use `OutputInfo` and `ProductOutputInfo` classes
- Handle `fluidInputInc` for proliferation points

### Documentation
- Use XML documentation comments for public APIs
- Chinese comments are acceptable (this is a Chinese mod)
- Keep comments concise and relevant
- Document complex algorithms or game mechanics

## Testing

### Current State
- No unit test framework configured
- Testing is manual through game play
- Build verification is primary quality gate

### Verification Commands
```bash
# Build verification
dotnet build FractionateEverything/FractionateEverything.csproj

# Check for compilation errors
# Expected: "Build succeeded. 0 Warning(s). 0 Error(s)."
```

## Development Workflow

### Mod Structure
1. **Building Definitions**: Static classes in `Logic/Building/`
2. **Recipe Logic**: Classes inheriting from `BaseRecipe` in `Logic/Recipe/`
3. **Managers**: Static classes in `Logic/Manager/` for game state
4. **UI Components**: In `Logic/UI/` for user interfaces
5. **Compatibility**: In `Compatibility/` for other mod integration

### Common Tasks

#### Adding a New Building
1. Create static class in `Logic/Building/`
2. Follow existing pattern (Level, EnableXxx properties, Create, SetMaterial, UpdateHpAndEnergy)
3. Register in `BuildingManager.AddFractionators()`
4. Add translations in `BuildingManager.AddTranslations()`

#### Adding a New Recipe Type
1. Create class inheriting from `BaseRecipe` in `Logic/Recipe/`
2. Override `GetOutputs` method
3. Implement `RecipeType` property
4. Register in appropriate manager

#### Modifying Game Logic
1. Use Harmony patches for game method interception
2. Place patches in appropriate manager classes
3. Follow postfix/prefix convention
4. Test thoroughly in-game

### Git Practices
- Commit messages in English
- Use conventional commit style: `feat:`, `fix:`, `refactor:`, etc.
- Atomic commits focused on single changes
- Reference issue numbers when applicable

## AI Agent Notes

This repository uses **oh-my-opencode** framework for AI-assisted development:
- `.sisyphus/` directory contains agent plans and learnings
- Plans are in `.sisyphus/plans/` (e.g., `building-enhancement.md`)
- Learnings are recorded in `.sisyphus/notepads/`

### Agent Workflow
1. Read existing plans in `.sisyphus/plans/`
2. Follow the structured task breakdown
3. Record learnings in appropriate notepad files
4. Verify all changes compile successfully
5. Update plan checkboxes when tasks complete

### Common Pitfalls to Avoid
1. **Don't modify `BaseRecipe.GetOutputs`** - it's shared by all recipe types
2. **Don't touch `buffBonus1/2/3`** - they're reserved for future use
3. **Don't add unnecessary Harmony patches** - use existing code paths when possible
4. **Always verify build** - `dotnet build` must succeed with 0 errors
5. **Follow existing patterns** - consistency is critical for maintainability

## Resources

- **DSP Modding Docs**: CommonAPI documentation
- **BepInEx**: Modding framework documentation
- **HarmonyX**: Patching library for .NET
- **Unity**: Game engine documentation (2022.3 LTS)

---

*Last updated: March 2026*  
*For AI agents working on MLJ_DSPmods repository*