# UI/View — 功能面板层

当前 28 个 `.cs` 文件：根层 `MainWindow`/`MainWindowPageRegistry` + 7 个业务子域。核心变化：**双主面板架构**与**抽奖/商店重构拆分**已落地。

## 入口与路由

- `MainWindow.cs`：
  - 面板状态机（`SelectedMainPanelType` / `OpenedMainPanelType`）
  - Legacy 与 Analysis 的打开/关闭/切换
  - 保存块 `MainPanelSelection`
  - 跨页导航 `NavigateToPage(category, index)`
- `MainWindowPageRegistry.cs`：
  - 分类顺序 `categoryOrder`
  - 页面注册 `allPages`
  - 面板过滤 `IsEnabledFor(panelType, sandboxMode)`

## 子目录索引（仅列变化敏感域）

| Dir | 文件数 | 关注点 |
|---|---:|---|
| `GetItemRecipe/` | 9 | 抽奖 + 商店 + 动画/兑换子组件；本轮重构核心 |
| `CoreOperate/` | 3 | 配方/建筑操作主面板 |
| `ProgressSystem/` | 4 | 任务/成就/开发日志 |
| `Setting/` | 3 | 面板风格切换按钮与沙盒开关 |

## 新约定（必须）

1. 页面可见性由 `MainWindowPageRegistry` 控制，避免在 `MainWindow` 分散 if/else。
2. 跨“抽奖↔商店”跳转统一使用 `MainWindow.NavigateToPage`。
3. 需要跨面板共享的数据（如抽卡总次数）统一进 `MainWindow.SharedPanelState`。
4. `UpdateUI` 必须先判断页面可见性 + 当前主面板类型。
5. `UI/View/*` 颜色文本禁止硬编码，统一 `RichTextUtils`。

## 反模式

- 继续把新页面写成 `MainWindow` 内硬编码注册（应放 `MainWindowPageRegistry`）
- 在子页面直接访问另一窗口实例（应通过 `MainWindow` 导航/状态接口）
- 在离屏页面持续刷新 UI（性能浪费 + 状态错乱）
