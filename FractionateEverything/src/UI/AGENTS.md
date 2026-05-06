# UI — Unity UI Layer

34 files, ~8k lines。MainPanel 是 FE 主面板页面系统，页面仍是静态类架构；主界面已升级为**双主面板并行**：Legacy(`MyConfigWindow`) + Analysis(`MyAnalysisWindow`)。

## Structure

```
UI/
├── Components/   # 通用组件；含 MyAnalysisWindow
├── Patches/      # UI Harmony 补丁；最终只保留 Common 通用控件 patch
└── MainPanel/
    ├── MainWindow.cs          # 双面板总控（打开/关闭/切换/导航/保存）
    ├── MainWindowPageRegistry.cs # 页面注册中心（分类、过滤、Analysis 开关）
    └── DrawGrowth/            # 抽奖/商店/兑换系统（TicketRaffle + LimitedTimeStore + Gacha）
```

## 双主面板契约（必须遵守）

- 枚举：`FEMainPanelType = None/Legacy/Analysis`
- 选中态：`SelectedMainPanelType`
- 打开态：`OpenedMainPanelType`
- 切换入口：`SwitchMainPanelFrom` / `SwitchSelectedMainPanelAndOpen`
- 跨页跳转统一走：`MainWindow.NavigateToPage(category, tabIndex)`
- 面板共享态统一走：`MainWindow.SharedPanelState`（目前仅含抽奖总次数与成就页码）

## 页面注册规则

在 `MainWindowPageRegistry.allPages` 注册页面：

```csharp
new(category, subpage,
    createUI, updateUI,
    enabledInAnalysis: bool,
    sandboxOnly: bool,
    createUIInAnalysis: optional)
```

- Legacy 默认可见
- Analysis 仅 `enabledInAnalysis=true` 才可见
- Analysis 可选专用渲染：`createUIInAnalysis`

## MainPanel 页面最小接口（静态类）

```csharp
AddTranslations();
LoadConfig(ConfigFile);
CreateUI(MyConfigWindow, RectTransform);
UpdateUI();
Import/Export/IntoOtherSave();
```

## 关键约束

- `UpdateUI()` 必须先做可见性/面板态判断（避免离屏刷新）
- 文本颜色在 `UI/MainPanel/*` 禁止硬编码，统一走 `RichTextUtils` 常量 + `WithColor`
- 新页面优先接入 `MainWindowPageRegistry`，不要再在 `MainWindow` 手写大段分类分发逻辑

## Patches Folder

- `Patches/Common/`：只放与功能域无关的通用控件 patch，例如按钮、下拉框兼容。
- 分馏塔窗口、配方显示、图标注入、信号选择等 patch 不再定义为“主面板 UI”，应归入对应功能域的 `Presentation`：
  - 分馏塔窗口、分馏塔 brief info、分馏配方显示 -> `Logic/Fractionation/Presentation`
  - 物品图标、信号选择、信号标签选择 -> `Logic/Items/Presentation`

历史文件迁移前的归属参考：

- `FEFractionatorWindow*.cs`：模组分馏塔独立窗口 patch，按 partial 拆分：
  - `FEFractionatorWindow.cs`：共享字段、槽位模型、事件绑定、模组分馏塔判定
  - `FEFractionatorWindow.Layout.cs`：窗口复制、布局改造、尺寸调整、槽位创建
  - `FEFractionatorWindow.Lifecycle.cs`：`_OnOpen` / `_OnClose` / `_OnUpdate` 窗口生命周期 patch
  - `FEFractionatorWindow.Rendering.cs`：窗口刷新、状态文字、电力、产物槽渲染
  - `FEFractionatorWindow.Lock.cs`：转化塔单路锁定右键交互、锁图标、锁状态 UI
  - `FEFractionatorWindow.Inventory.cs`：`OnProductUIButtonClick` 拦截与手动取放物品
- `IconSetPatch.cs`：mod 图标注入
- `UIRecipeEntryPatch.cs`：配方显示 patch
- `UIComboBoxPatch.cs` / `UIButtonPatch.cs`：通用控件兼容

UI 补丁只处理界面层；游戏状态逻辑放对应 `Logic/*` 功能域。
