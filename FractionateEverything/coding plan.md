# Coding Plan

## 目标

基于 `TODO.md` 中已确认的方向，将当前抽卡/成长/任务系统重构为：

- 常规模式：
  `当前阶段矩阵 + 残片`
  `开线池 + 原胚闭环池 + 成长池 + 流派聚焦层`
- 速通模式：
  `阶段奖励 + 当前阶段矩阵 + 少量残片`
  `阶段箱池 + 简化原胚池 + 简化成长池 + 简化流派聚焦层`

并同步完成：

- 原版配方增强改造
- 建筑成长改造
- 任务/成就/图鉴职责重组
- 精馏塔改造

## 不纳入 2.3 首版

- VIP 系统
- 符文系统
- 退火系统
- 实体奖券体系
- 通用增幅芯片
- 流派专属道具
- 抽卡类型自选

这些内容全部按“冻结 / 退出主架构 / 后续另行重做”处理。

## 实施顺序

### 阶段 0：基础清理与语义迁移

目标：

- 从代码中移除旧奖券、VIP、通用芯片对新系统的核心依赖
- 建立“当前阶段矩阵 + 残片”的新语义

主要文件：

- `Logic/Manager/ItemManager.cs`
- `Logic/Manager/TechManager.cs`
- `Logic/Manager/GachaPool.cs`
- `Logic/Manager/GachaManager.cs`
- `Logic/Manager/GachaService.cs`
- `UI/View/GetItemRecipe/TicketRaffle.cs`
- `UI/View/GetItemRecipe/TicketExchange.cs`
- `UI/View/GetItemRecipe/LimitedTimeStore.cs`
- `UI/View/Setting/VipFeatures.cs`

具体动作：

1. 删除或停用实体奖券的主流程入口。
2. 停用 `VipFeatures` 对抽卡/商店数值的影响。
3. 将旧的奖券兑换逻辑改造为：
   - 常规模式下的矩阵直耗 / 残片消耗
   - 速通模式下的阶段奖励 / 矩阵直耗
4. 清理 `增幅芯片` 作为建筑成长主材料的旧入口。

完成标准：

- 代码层不再把实体奖券视为 2.3 主资源
- `VipFeatures` 不再影响抽卡数值
- UI 文案不再误导玩家使用旧奖券体系

### 阶段 1：精馏塔重构

目标：

- 把精馏塔固定为：
  `矩阵 -> 残片`
  的稳定副资源压缩建筑

主要文件：

- `Logic/Building/RectificationTower.cs`
- `Logic/Recipe/RectificationRecipe.cs`
- `Logic/Manager/ProcessManager.cs`
- `Logic/Manager/ItemManager.cs`
- `UI/View/CoreOperate/BuildingOperate.cs`
- `UI/View/GetItemRecipe/*`

具体动作：

1. 调整 `RectificationRecipe`，让矩阵成为主输入对象。
2. 按矩阵层次实现残片倍率表。
3. 为黑雾矩阵单独提供支线接口逻辑。
4. 精馏塔成长只影响：
   - 吞吐
   - 能耗
   - 残片转化效率
   - 高低阶矩阵的边际收益差
5. 更新精馏塔相关 UI 说明。

完成标准：

- 精馏塔不再直接产奖券
- 矩阵层次残片效率生效
- 黑雾矩阵输入路径单独存在

### 阶段 2：开线池 / 原胚闭环池 / 成长池落地

目标：

- 把抽卡系统从旧四池语义重构为三池一层：
  `开线池 + 原胚闭环池 + 成长池 + 流派聚焦层`

主要文件：

- `Logic/Manager/GachaPool.cs`
- `Logic/Manager/GachaService.cs`
- `Logic/Manager/GachaManager.cs`
- `UI/View/GetItemRecipe/TicketRaffle.cs`
- `UI/View/GetItemRecipe/LimitedTimeStore.cs`
- `UI/View/GetItemRecipe/TicketExchange.cs`

具体动作：

1. 开线池：
   - 只承载 `MineralCopyRecipe`、`ConversionRecipe`、原版配方增强
   - 按 6 层矩阵推进
   - 用阶段箱式固定奖品作为主抽法
2. 原胚闭环池：
   - 常驻
   - 主要产出原胚、定向原胚、闭环相关奖励
3. 成长池：
   - 非随机
   - 承载建筑关键节点、阶段跃迁、原版配方增强、定向补差
4. 流派聚焦层：
   - 不做独立奖池
   - 只对以上三者做方向加权
5. 重写抽奖页、商店页、兑换页的名称、说明、跳转与文案。

完成标准：

- UI 中不再出现旧版 “配方/原胚/UP/限定” 语义残留
- 三池一层结构可完整跑通
- 开线池阶段感清晰

### 阶段 3：配方成长与建筑成长重构

目标：

- 让 `BaseRecipe` 和塔种成长适配新系统

主要文件：

- `Logic/Recipe/BaseRecipe.cs`
- `Logic/Recipe/VanillaRecipe.cs`
- `Logic/Manager/RecipeManager.cs`
- `Logic/Manager/BuildingManager.cs`
- `UI/View/CoreOperate/FracRecipeOperate.cs`
- `UI/View/CoreOperate/BuildingOperate.cs`

具体动作：

1. `BaseRecipe` 增加语义标记：
   - 生产型
   - 工具/解锁型
   - 特殊成长型
2. 重复抽取逻辑改造：
   - 不再统一 11 次完全体
   - 重复奖励更多转为回响 / 研究点 / 定向材料
3. 建筑成长：
   - 继续采用塔种全局成长
   - 普通等级靠塔种经验
   - 关键节点使用残片 + 当前阶段矩阵
4. 原版配方增强：
   - 采用“落后一层”的半定向增强

完成标准：

- `BaseRecipe` 不再用一套逻辑覆盖所有类型
- 建筑成长与成长池资源结构对齐
- 原版配方增强不再是终局鸡肋系统

### 阶段 4：任务 / 成就 / 图鉴系统重构

目标：

- 重写主线任务、循环任务、成就、图鉴职责
- 让成就系统成为全局增幅唯一入口

主要文件：

- `UI/View/ProgressSystem/MainTask.cs`
- `UI/View/ProgressSystem/RecurringTask.cs`
- `UI/View/ProgressSystem/Achievements.cs`
- `UI/View/Statistic/RecipeGallery.cs`
- `Logic/Manager/GachaGalleryBonusManager.cs`
- `Logic/Manager/TutorialManager.cs`

具体动作：

1. 主线任务改成新的 10 阶段结构。
2. 循环任务改成新的 5 类结构。
3. 图鉴加成迁成成就节点。
4. 成就页改为统一汇总：
   - 分馏成功率
   - 损毁减免
   - 翻倍概率
   - 工厂耗电
   - 物流效率
   - 当前发电阶段效率
5. 删除或替换旧的奖券/芯片/符文相关奖励。

完成标准：

- 任务、成就、图鉴之间职责边界清晰
- 图鉴不再直接发 buff
- 成就系统成为唯一长期被动总入口

### 阶段 5：黑雾支线整合

目标：

- 把黑雾内容作为独立战斗支线接入，而不是第七个主线矩阵池

主要文件：

- `Logic/Manager/ItemManager.cs`
- `Logic/Manager/RecipeManager.cs`
- `Logic/Manager/GachaService.cs`
- `UI/View/*`

具体动作：

1. 黑雾配方 / 科技 / 输入资源单独分类。
2. 黑雾矩阵通过精馏塔与成长池挂接。
3. 在信息 / 引力 / 宇宙阶段分别开放支线内容。

完成标准：

- 黑雾与主线阶段共存但不混层
- 黑雾矩阵有独立价值，不破坏主线经济

### 阶段 6：速通模式落地

目标：

- 实现独立于常规模式的速通抽卡体系

主要文件：

- `UI/View/MainWindowPageRegistry.cs`
- `UI/View/Setting/*`
- `Logic/Manager/*`
- `UI/View/GetItemRecipe/*`
- `UI/View/ProgressSystem/*`

具体动作：

1. 速通模式下切换到：
   - 阶段箱池
   - 简化原胚池
   - 简化成长池
   - 简化流派聚焦层
2. 速通模式资源改成：
   - 阶段奖励
   - 矩阵直耗
   - 少量残片
3. 支持肉鸽式主题倾向：
   - 选中的主题后续更强、更容易出现
   - 未选中的主题后续更弱、更不容易出现

完成标准：

- 常规模式 / 速通模式能明确切换
- 两者共享世界观但节奏明显不同

## 代码改造清单输出方式

后续真正开始动代码时，按以下方式拆分提交：

1. 先做资源语义迁移
2. 再做精馏塔
3. 再做三池一层
4. 再做配方 / 建筑成长
5. 再做任务 / 成就 / 图鉴
6. 最后做黑雾与速通模式

每一轮都要求：

- 代码改动闭环
- 主项目构建通过
- 独立 commit
