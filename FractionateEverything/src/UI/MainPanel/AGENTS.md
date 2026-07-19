# UI/MainPanel — 功能面板层

当前主面板按玩家可见功能组织业务子域。根层只放总控、路由、契约与共享状态；窗口外壳放 `Shell/`，主题骨架放 `Theme/`，业务目录里默认 **一个页面一个 `.cs` 文件**。核心变化：**双主面板架构**和**文明解析主分类**。

## 入口与路由

- `MainWindow.cs`：
  - 面板状态机（`SelectedMainPanelType` / `OpenedMainPanelType`）
  - MessageBox 与 Analysis 的打开/关闭/切换
  - 保存块 `MainPanelSelection`
  - 跨页导航 `NavigateToPage(category, index)`
- `MainWindowPageRegistry.cs`：
  - 分类顺序 `categoryOrder`
  - 页面注册 `allPages`
  - 面板过滤 `IsEnabledFor(panelType, sandboxMode)`
- `Shell/`：
  - `MessageBox/`：消息框风格主面板窗口壳。
  - `Analysis/`：分析面板风格主面板窗口壳。
- `Theme/`：
  - 主面板页面设计尺寸、页头、卡片、页脚和圆角视觉素材。

## 子目录索引（仅列变化敏感域）

| Dir | 文件数 | 关注点 |
|---|---:|---|
| `Shell/` | 6 | MessageBox / Analysis 两种主面板窗口壳 |
| `Theme/` | 2 | 主面板页面视觉骨架与圆角素材 |
| `Civilization/` | 4 | 文明总览、协议恢复、远古科技树、文明成就 |
| `CoreOperate/` | 2 | 分馏配方与原版配方 |
| `ProgressTask/` | 1 | 恢复手册 |
| `Archive/` | 2 | 配方图鉴、开发日志 |
| `ResourceInteraction/` | 1 | 物品交互 |
| `Setting/` | 1 | 面板风格等杂项设置 |

## 新约定（必须）

1. 页面可见性由 `MainWindowPageRegistry` 控制，避免在 `MainWindow` 分散 if/else。
2. 跨页面跳转统一使用 `MainWindow.NavigateToPage`；不得重新注册已删除的抽取、市场或线性成长页面。
3. `UpdateUI` 必须先判断页面可见性 + 当前主面板类型。
4. `UI/MainPanel/*` 颜色文本禁止硬编码，统一 `RichTextUtils`。
5. 业务页面默认一个页面一个 `.cs` 文件；不要把同一个页面继续拆成 `*.Layout.cs`、`*.State.cs`、`*.Translations.cs` 这类 partial 文件。
6. 页面过大时，优先把可复用的规则、计算、格式化、数据模型下沉到对应 `Logic/*` 功能域、`UI/Controls`、`UI/Layout` 或 `UI/MainPanel/Theme`，不要用 partial 文件掩盖页面承担过多职责。
7. 允许独立成文件的只有面板壳/路由/契约和跨页面复用组件，不保留未注册的旧业务页面作为参考。

## 反模式

- 继续把新页面写成 `MainWindow` 内硬编码注册（应放 `MainWindowPageRegistry`）
- 在子页面直接访问另一窗口实例（应通过 `MainWindow` 导航/状态接口）
- 在离屏页面持续刷新 UI（性能浪费 + 状态错乱）
- 为了“看起来分层”把单个页面拆成多个 partial 文件
