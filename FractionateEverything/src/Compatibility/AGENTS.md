# Compatibility — Mod Integration

兼容层负责外部 mod 检测、兼容开关和外部 mod 专用 patch。每个外部 mod 的兼容逻辑应集中在一个文件或一个明确子域。

## Structure

```
Compatibility/
├── CheckPlugins.cs     # 兼容层入口：BepInDependency、检测、提示
├── LegacySave/         # FE 2.x 物理 Proto 引用迁移（LegacyProtoMigration.cs）
├── Mods/               # 通用第三方 mod 兼容，包括 They Come From Void
└── Nebula/             # Nebula 联机同步 packet 和 processor
```

## Pattern

```csharp
public static class GenesisBook {
    public static bool Enable { get; private set; }

    internal static void Check() {
        Enable = Chainloader.PluginInfos.ContainsKey(GUID);
    }
}
```

## Rules

- `CheckPlugins.Check()` 在 Awake 阶段设置 Enable 标记；不要在业务逻辑中重复读 `PluginInfos`。
- 普通外部 mod 放 `Mods`。
- 联机同步放 `Nebula`；文明状态、配方校准和检索货币使用统一快照包，单塔实例状态使用明确的增量 packet type。
- `LegacySave/LegacyProtoMigration.cs` 只迁移原版持久容器中 FE 2.x Item/Model/Recipe 的物理 ID 引用（旧蓝图与存档引用仍可解析）；FE 自有 version10 状态不映射旧 ID，物理兼容与状态迁移相互独立。
- They Come From Void 外部兼容放 `Mods/TheyComeFromVoid.cs`；FE 自己的黑雾逻辑放 `Logic/Civilization/DarkFog.cs`。
