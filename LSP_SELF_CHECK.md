# C# LSP 故障记录与跨环境自检规范

## 1. 本次故障现象

- 触发接口：`lsp_diagnostics`
- 典型报错：`LSP request timeout (method: initialize)`
- 影响：C# 语言服务无法完成初始化，导致诊断、符号、跳转能力不可用或不稳定

## 2. 根本原因

本次问题是 **LSP 启动协议与启动参数不匹配** 导致的初始化层故障，具体表现为：

1. `csharp-ls` 在当前环境下默认未以 LSP 模式稳定启动（需要显式 `-lsp`）。
2. 客户端与服务端索引基准不一致风险（需要显式 `-z` 使用 zero-based）。
3. 仓库属于多项目 net472 Unity/BepInEx 方案，初始化开销大；若配置过重，容易在冷启动阶段超时。

> 说明：这是"初始化层"问题，优先级高于代码语义问题。初始化失败时，后续能力全部不可用。

## 3. 本次修复操作

### 3.1 仓库内配置修复

新增/调整以下文件：

- `omnisharp.json`

#### `omnisharp.json`（当前生效版本）

```json
{
  "msbuild": {
    "loadProjectsOnDemand": true,
    "enablePackageAutoRestore": false
  },
  "fileOptions": {
    "systemExcludeSearchPatterns": [
      "**/bin/**/*",
      "**/obj/**/*",
      "**/.vs/**/*",
      "**/packages/**/*",
      "**/Temp/**/*",
      "**/gamedata/**/*"
    ]
  },
  "RoslynExtensionsOptions": {
    "EnableAnalyzersSupport": false,
    "EnableImportCompletion": false,
    "EnableDecompilationSupport": false,
    "DiagnosticWorkersThreadCount": 1
  }
}
```

> 注：本项目额外排除了 `gamedata/` 目录（包含大量反编译源码），以减少索引开销。

### 3.2 启动器修复（环境级）

在本机将 `csharp-ls` 前置为 wrapper（优先于原二进制），强制补齐启动参数：

- 强制 LSP 模式：`-lsp`
- 强制 zero-based：`-z`
- 若未显式传入 source，则补 `-s "$PWD"`

本机路径示例：

- `/home/mlj/.opencode/bin/csharp-ls`

> 跨环境迁移时，不要求同路径，但要求同策略（参数约束一致）。

## 4. 修复后验证结果

本次已验证：

1. `lsp_diagnostics` 不再卡在 `initialize timeout`（初始化层已恢复）。
2. `lsp_symbols` 可返回 `FractionateEverything` 类的文档符号树。
3. `lsp_goto_definition` 可返回定义位置。
4. `dotnet build FractionateEverything/FractionateEverything.csproj` 成功（仅既有 warning）。

## 5. 跨环境自检 SOP（建议照抄执行）

### 步骤 A：启动参数自检

检查 `csharp-ls` 启动是否包含以下约束：

- 必须有 `-lsp`
- 必须有 `-z`
- 建议有 `-s <workspace>`

判定标准：任一缺失都可能导致初始化异常或协议不兼容。

### 步骤 B：仓库配置自检

确认以下文件存在且为有效 JSON：

- `omnisharp.json`

判定标准：

- 具备排除目录策略（`bin/obj/.vs/packages/Temp/gamedata`）
- 分析器相关能力默认关闭（先保可用性）
- 项目加载超时 >= 300 秒

### 步骤 C：能力验证（最小闭环）

按顺序执行（任一步失败即停止并排障）：

1. `lsp_diagnostics` 目标文件：`FractionateEverything/src/FractionateEverything.cs`
2. `lsp_symbols` 目标文件：`FractionateEverything/src/FractionateEverything.cs`
3. `lsp_goto_definition` 指向 `BaseUnityPlugin` 或其他类型

判定标准：

- 允许出现语义诊断错误（如引用未解析）
- 不允许出现 `initialize timeout`

### 步骤 D：编译一致性验证

执行：

```bash
dotnet build FractionateEverything/FractionateEverything.csproj
```

判定标准：

- 构建成功（允许现有 warning）
- 若构建成功但 LSP 大量误报，归类为"语义层误差"，不是"初始化故障"

## 6. 常见回退策略

如果再次出现初始化超时：

1. 先检查 wrapper 是否仍生效（PATH 优先级是否变化）。
2. 确认 `omnisharp.json` 未被改成激进配置。
3. 回到最小可用配置（本文件第 3 节）再重试。
4. 仅在初始化稳定后再逐项打开分析能力。

## 7. 结论

本次故障本质是 **语言服务启动契约不稳定**，不是业务代码错误。  
治理顺序必须是：**先修初始化，再看语义诊断准确性**。

---

*适用于: MLJ_DSPmods (net472, Unity/BepInEx mod)*
