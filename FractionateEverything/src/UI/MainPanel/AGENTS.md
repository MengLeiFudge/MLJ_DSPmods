# UI/MainPanel — FE 功能面板

主面板按玩家任务组织页面。根层只维护窗口状态、路由、注册表与稳定保存块；业务规则必须留在 `Logic/*`。

## Active Pages

| 分类 | 页面 | 数据来源 |
| --- | --- | --- |
| 生命工业 | 文明研究 | `ResearchManager`、`RecipeManager` |
| 生命工业 | 遗物探索 | `ExplorationManager` |
| 资源管理 | 亚空间仓储 | `DataCenterInventory` |
| 开发档案 | 开发日记 | 静态档案内容 |
| 系统设置 | 杂项设置 | BepInEx 配置与现役交互设置 |

`CoreOperate/FracRecipeOperate.cs`、`VanillaRecipeOperate.cs` 的兼容配置/存档钩子可继续存在，但不注册为 3.0 页面。旧协议恢复、远古科技树、恢复手册、成就和独立配方图鉴不得重新注册。

## Entry And Routing

- `MainWindow.cs`：双外壳状态机、快捷键、保存块 `MainPanelSelection`、跨页导航。
- `MainWindowPageRegistry.cs`：分类顺序、唯一页面注册表和可见性过滤。
- `Shell/`：MessageBox 与 Analysis 外壳。
- `Theme/`：统一设计尺寸、页头、内容卡、页脚和圆角素材。

默认快捷键：`Shift + F` 开关面板；面板打开时 `Ctrl + Shift + F` 切换外壳。通信未建立前不打开。

## Page Contracts

静态页面按需实现：

```csharp
AddTranslations();
LoadConfig(ConfigFile);
CreateUI(MyWindow, RectTransform);
UpdateUI();
Import/Export/IntoOtherSave();
```

不是每个页面都必须拥有配置或存档；空钩子只用于保持统一接线。

## 3.0 Presentation Rules

- 文明研究页只列已取得资格的方向，并动态展示进度、已发现内容和合并后的配方图鉴。
- 遗物探索页对首遗物显示明确身份；其他点位统一显示“古文明信号”，只提供目标星球和同星现场强度。
- 亚空间仓储页显示真实库存、容量和满状态，不生成奖励或修改研究。
- 解析发现与探索回收使用逻辑域排队的 `UIRealtimeTip`，页面不自行重复提示。
- 页面内容高度随方向、配方和点位数量扩展，不用固定容器裁掉动态文本。
- `UpdateUI` 先检查可见性；文本颜色走 `RichTextUtils`；跨页只走 `NavigateToPage`。
- 不加入协议购买、成就领奖、精确探索坐标、导航按钮或高级塔缓存详情。
