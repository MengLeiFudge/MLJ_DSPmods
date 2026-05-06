# Compatibility — Mod Integration

兼容层负责外部 mod 检测、兼容开关和外部 mod 专用 patch。每个外部 mod 的兼容逻辑应集中在一个文件或一个明确子域。

## Structure

```
Compatibility/
├── CheckPlugins.cs     # 兼容层入口：BepInDependency、检测、提示
├── Mods/               # 通用第三方 mod 兼容
├── Nebula/             # Nebula 联机同步 packet 和 processor
└── DarkFog/            # They Come From Void 兼容
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
- 联机同步放 `Nebula`。
- They Come From Void / 黑雾外部兼容放 `DarkFog`；FE 自己的黑雾逻辑放 `Logic/DarkFog`。
