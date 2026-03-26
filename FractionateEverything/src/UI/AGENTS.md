# UI — Unity UI Layer

34 files, ~8k lines。View 仍是静态类架构，但主界面已升级为**双主面板并行**：Legacy(`MyConfigWindow`) + Analysis(`MyAnalysisWindow`)。

## Structure

```
UI/
├── Components/   # 通用组件；含 MyAnalysisWindow
├── Patches/      # UI Harmony 补丁（图标、控件兼容）
└── View/
    ├── MainWindow.cs          # 双面板总控（打开/关闭/切换/导航/保存）
    ├── MainWindowPageRegistry.cs # 页面注册中心（分类、过滤、Analysis 开关）
    └── GetItemRecipe/         # 抽奖/商店重构区（TicketRaffle + LimitedTimeStore）
```

## 双主面板契约（必须遵守）

- 枚举：`FEMainPanelType = None/Legacy/Analysis`
- 选中态：`SelectedMainPanelType`
- 打开态：`OpenedMainPanelType`
- 切换入口：`SwitchMainPanelFrom` / `SwitchSelectedMainPanelAndOpen`
- 跨页跳转统一走：`MainWindow.NavigateToPage(category, tabIndex)`
- 面板共享态统一走：`MainWindow.SharedPanelState`

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

## View 最小接口（静态类）

```csharp
AddTranslations();
LoadConfig(ConfigFile);
CreateUI(MyConfigWindow, RectTransform);
UpdateUI();
Import/Export/IntoOtherSave();
```

## 关键约束

- `UpdateUI()` 必须先做可见性/面板态判断（避免离屏刷新）
- 文本颜色在 `UI/View/*` 禁止硬编码，统一走 `RichTextUtils` 常量 + `WithColor`
- 新页面优先接入 `MainWindowPageRegistry`，不要再在 `MainWindow` 手写大段分类分发逻辑

## Patches Folder

- `FEFractionatorWindow.cs`：分馏塔窗口布局复制与改造
- `IconSetPatch.cs`：mod 图标注入
- `UIRecipeEntryPatch.cs` / `UIComboBoxPatch.cs` / `UIButtonPatch.cs`：控件兼容

UI 补丁只处理界面层；游戏状态逻辑放 `Logic/Manager/`。
