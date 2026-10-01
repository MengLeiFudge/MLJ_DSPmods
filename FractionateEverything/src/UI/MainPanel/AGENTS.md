# UI/MainPanel — FE 功能面板

主面板按玩家任务组织页面。根层只维护窗口状态、路由、注册表与稳定保存块；业务规则必须留在 `Logic/*`。

## Active Pages

| 分类 | 页面 | 数据来源 |
| --- | --- | --- |
| 生命工业 | 文明研究 | `ResearchManager` |
| 生命工业 | 遗物探索 | `ExplorationManager` |
| 生命工业 | 配方图鉴 | `RecipeManager`、`ResearchManager` |
| 资源管理 | 亚空间仓储 | `DataCenterInventory` |
| 开发档案 | 开发日记 | 静态档案内容 |
| 系统设置 | 杂项设置 | BepInEx 配置与当前交互设置 |

`CoreOperate/FracRecipeOperate.cs` 是完整配方目录的页面所有者；`VanillaRecipeOperate.cs` 只保留兼容配置/存档钩子，不注册为页面。旧协议恢复、远古科技树、恢复手册、成就和原版配方强化页不得重新注册。

## Entry And Routing

- `MainWindow.cs`：双外壳状态机、快捷键、保存块 `MainPanelSelection` / `PinnedExplorationSignals`、跨页导航。
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

## Presentation Rules

- 文明研究页突出完整研究目录的已探索/已解析总进度，展示承载物品共享槽，以及科技与特质档案；尚未探索的档案名称必须显示为问号。
- 配方图鉴按类型列出当前目录中的全部稳定配方槽位，显示总体与分类发现进度；未发现槽位不得披露名称、输入、产物或运行参数。
- 遗物探索页按稳定 ID 显示 `关键信号{ID}` / `信号{ID}`；只有关键信号公开目标星球，普通信号不得泄露恒星系、星球、内容或坐标。
- 左侧探索 HUD 固定一个不可取消的主线槽和四个普通槽；使用原版 HUD 普通文字材质，不创建黑色面板或发光文字。普通槽顺序属于本地 UI 偏好，不按强度排序，也不进入主机文明状态。
- 探索页同时显示已解锁的星球/遗物量化强度，提供普通信号固定操作；最终成长级可提供玩家主动点击的原版位置标注，但不得启动导航或移动。
- 亚空间仓储页显示真实库存、容量和满状态，不生成奖励或修改研究。
- 解析发现与探索回收使用逻辑域排队的 `UIRealtimeTip`，页面不自行重复提示。
- 页面动态长文本使用带裁剪和自动隐藏右侧滚动条的滚动区，内容高度按实际文本或目录条目扩展，不用固定容器裁掉动态文本。
- `UpdateUI` 先检查可见性；文本颜色走 `RichTextUtils`；跨页只走 `NavigateToPage`。
- 不加入协议购买、成就领奖、直接坐标文本、自动导航、移动命令或高级塔缓存详情。
