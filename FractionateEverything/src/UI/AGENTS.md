# UI — Unity 界面层

UI 层包含窗口基础设施、可复用控件、声明式布局、FE 主面板和通用控件 patch。业务状态与结算归 `Logic/*`，UI 只读取快照和调用明确命令。

## Structure

```
UI/
├── Foundation/   # 窗口生命周期、基类、RectTransform 工具
├── Controls/     # 可复用控件，一类一文件
├── Layout/       # Grid DSL 与布局运行时
├── MainPanel/
│   ├── Shell/    # MessageBox / Analysis 两种窗口壳
│   ├── Theme/    # 页面尺寸、页头、内容区、页脚和视觉素材
│   ├── MainWindow.cs
│   ├── MainWindowPageRegistry.cs
│   ├── Civilization/        # 文明研究、遗物探索
│   ├── CoreOperate/         # 配方图鉴与历史兼容页面钩子
│   ├── ResourceInteraction/ # 亚空间仓储
│   ├── Archive/             # 开发日记
│   └── Setting/             # 杂项设置
└── Patches/Common/          # 与业务域无关的通用控件 patch
```

## Dual Panel Contract

- `FEMainPanelType = None / Legacy / Analysis`；Legacy 是 MessageBox 外壳，Analysis 是分析外壳。
- 选择态用 `SelectedMainPanelType`，打开态用 `OpenedMainPanelType`。
- 切换统一走 `SwitchMainPanelFrom` / `SwitchSelectedMainPanelAndOpen`。
- 跨页导航统一走 `MainWindow.NavigateToPage(category, tabIndex)`。
- 两种外壳共享 `MainWindowPageRegistry` 和同一页面实现，不复制业务页面。

## Page Rules

- 新页面只在 `MainWindowPageRegistry.allPages` 注册；未注册旧页面不得作为隐藏入口保留。
- `UpdateUI()` 首先检查页面根节点是否存在且可见，避免离屏刷新。
- 页面只调用文明、亚空间和配方域的只读快照/公开命令，不直接修改静态集合。
- `UI/MainPanel/*` 富文本颜色统一用 `RichTextUtils`，禁止散落硬编码颜色。
- 业务页面默认一个页面一个文件；共用布局下沉到 `Layout` / `Theme`，规则计算下沉到 `Logic`。
- 卡片内部的动态长内容使用无边框滚动区，滚动条按溢出状态自动隐藏；不得在内容卡内再嵌套装饰卡片。

## Layout Baseline

Analysis 中央黑色区域固定 1102 x 787；四边 10 px 间距；统一设计画布 1082 x 767，左上原点 `(10, 10)`。MessageBox 代理页面也按同一设计尺寸创建根节点。

## Patch Ownership

- 分馏塔窗口与 brief info：`Logic/Fractionation/Fractionators/Presentation`。
- 图标、物品/信号选择器：`Logic/Items/Presentation`。
- 物流站窗口：`Logic/Station`。
- `UI/Patches/Common` 只保留真正跨业务的控件兼容。
