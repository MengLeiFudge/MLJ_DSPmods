![Fractionate Everything](https://s2.loli.net/2024/04/08/LtlNkxZD4jmdbFX.jpg)

> “走自己的路，不要随波逐流。” —— 重氢
> "Don't just follow the path. Make your own trail." - Deuterium

# 万物分馏 Fractionate Everything

万物分馏将分馏扩展为一套需要探索、解析和供养的古文明生命工业。首次生产电磁矩阵后，你会从出生星上的首个遗物开始，建立宇宙通用的分馏亚空间，研究未知物品方向，并用五种分馏塔搭建普通资源增殖、珍奇催化、生命塔繁育和工业置换产线。

Fractionate Everything turns fractionation into an old-civilization living industry built around exploration, analysis, and sustained automation. After producing your first Electromagnetic Matrix, recover the first relic, establish a galaxy-wide subspace inventory, discover unknown recipe directions, and operate five specialized fractionators.

## 主要内容 Features

### 五种分馏塔 Five Fractionators

- **基础矿物塔 Basic Mineral Tower**：自动识别普通自然资源。每轮有机会消耗 1 件并产出 2 件同种物品；失败物继续流出。
- **基础解析塔 Basic Analysis Tower**：消耗已经取得解析资格的物品，随机推进该方向中的配方或高级塔特质。普通无资格或已完成方向会原样流出；高级塔在不能解析时改为献祭。
- **高级矿物塔 Advanced Mineral Tower**：以少量珍奇矿物作催化物，把普通资源增殖为对应珍奇资源。
- **高级交互塔 Advanced Interaction Tower**：将通用原胚培育成三类高级塔，也能用高级塔模板繁育更多通用原胚。
- **高级转化塔 Advanced Conversion Tower**：重构燃料棒、采矿机和熔炉，并回收被替换的部件。

两座基础塔可在制造台制作、使用电网，并可通过正面端口向亚空间上传物品。三座高级塔需要先发现配方，再由通用原胚培育；高级塔不耗电，而是把六种矩阵作为食物。

Basic towers are craftable, grid-powered, single-input machines. Advanced towers are cultivated from Universal Tower Embryos, accept multiple inputs, and consume science matrices as nutrition instead of electricity.

### 生命塔供养 Living-Tower Nutrition

高级塔必须先在窗口中选择一张已发现配方。所有输入口都可以接收反应物或矩阵，塔会保留两轮底料，把多余或无关物品送回流动输出。

六种矩阵每件分别提供 `1 / 2 / 4 / 8 / 16 / 32` 营养，速度档依次为 `1.0 / 1.1 / 1.2 / 1.3 / 1.4 / 1.5`。每个等效加工轮消耗 0.02 营养，待机每秒消耗 0.002 营养；营养充足会修复耐久。完全断粮后，塔会按当前耐久比例越来越慢地继续加工，同时持续损失耐久，直至损毁。建造无人机不能维修生命塔，损毁会丢失塔内缓存，主动拆除则返还缓存物品。

正常运行时，产物和失败物会等待凑满当前全局堆叠后输出；切换配方后的无关物会优先排空，输入全部用完后也会释放不足整堆的尾料。

Advanced towers keep real input, food, product, and passthrough buffers. Their six matrix speed tiers range from 1.0x to 1.5x; each equivalent processing round consumes 0.02 nutrition and idling consumes 0.002 per second. A starving tower keeps processing at a speed set by its remaining durability while taking damage; dismantling returns cached items, while destruction does not.

### 解析研究 Analysis Research

探索点授予的是某种物品的**解析资格**，不会提前告诉你具体结果。把该物品送进基础解析塔后，成功判定会推进当前最低未完成层级；进度完成时，从该方向随机发现一项内容。

- 同一方向可以包含多张配方。
- 同一配方可以从多个相关方向发现。
- 已发现内容会从所有方向移除，不会重复。
- 配方中的普通产物会开放新的关联方向；高级塔方向不走这条捷径。
- 同类高级塔在亚空间库存中首次达到 1000 座后，永久开放对应解析方向。高级塔初始容量为 300 座，因此至少需要三级亚空间扩容。高级塔方向尚未开放或已经完成时，基础解析塔会改为献祭，并以每件 2% 的成功率从正面产出 Memory。
- 最近 60 秒内同类高级塔的实际献祭速率会提高该类高级塔的配方成功率：按 5% 分档，每秒更新，最高 `+75%`；三类塔分别计算。

文明研究页只显示已经取得资格的方向、真实进度和已发现内容。配方图鉴也在该页面中，不需要领取奖励或购买解锁。

Exploration unlocks item directions, not individual recipes. Analyze an eligible item to progress the lowest unfinished tier, then discover one unknown result from that direction. Results are globally unique. Advanced-Tower directions unlock permanently when 1,000 towers of that type are first stored; subsequent sacrifices can raise only that tower type's production success rate.

### 遗物探索 Relic Exploration

首个遗物会在首次生产电磁矩阵后出现在出生星，并提供一次自动导航。回收后获得：

- 基础矿物塔 x10
- 基础解析塔 x10
- 通用原胚 x20
- 石矿、铁矿和通用原胚解析资格
- 石矿与铁矿基础复制配方
- 分馏亚空间和主面板访问能力

后续信号会随航行能力扩展到出生星系和其他星系。普通信号只显示目标星球；接近目标星球后，面板才显示现场信号强度。探索点使用球形范围，固态星球的点贴近地表，气态巨星的点位于外层空间。它不会给出精确坐标、方向箭头或自动导航，信号也不会过期。

探索点可能提供新的解析资格，也可能提供目标星球实际拥有的资源或通用原胚。通用原胚固定占资源奖励的 10%；其余权重为火山/沙漠 `80% 矿脉 + 10% 流体`、海洋 `55% + 35%`、冰原 `60% + 30%`、气态巨星 `90% 气体`。缺失资源类别的权重转给该星球其他实际资源，原胚权重不变。奖励直接进入亚空间，并允许暂时超过常规容量。每次发现研究内容会提前下一次定时信号；解析 Frag 也可能立即生成额外信号。

The first relic appears after your first Electromagnetic Matrix and includes one guided navigation. Later signals reveal only a target planet and nearby signal strength. Search a spherical area near the planet; gas-giant signals remain reachable in outer space. Signals never expire and may contain research eligibility or physical resources.

### 黑雾补给 Dark-Fog Drops

地面和太空黑雾均有机会分别掉落 Frag 和通用原胚。Frag 送入基础解析塔后可能生成探索信号；通用原胚用于高级塔培育和繁育。

黑雾不是主线门槛。关闭战斗时，定时探索信号和资源点仍能提供完整进度与持续原胚来源。

Ground and space Dark Fog can independently drop Frag and Universal Tower Embryos. Combat accelerates progress but is never required for the complete progression.

### 分馏亚空间 Fractionation Subspace

亚空间是一套全宇宙共享库存，每种物品拥有独立容量。初始容量为每种物品 10 组，成长科技可提高到 100 组。

访问方式：

- `Shift + F` 打开 FE 主面板，在“亚空间仓储”中手动存取。
- 两座基础塔在正面输入、两侧不连接时上传物品；容量不足时整批拒收，不会丢货。
- 行星和星际物流交互站自动存取，并保留槽位模式、阈值、增产点、复制粘贴和蓝图设置。
- 高级塔与其他物品一样正常进入亚空间；同类库存首次达到 1000 座时永久开放对应解析方向，需要献祭时再把该塔送入基础解析塔。

物流交互站按物品价值与实际搬运数量消耗站内电量。能量不足时只传输可支付的数量；亚空间存取能效科技最多把耗能降低一半。

Subspace is a galaxy-wide inventory with independent per-item limits. Access it manually, upload through the front port of either basic tower, or automate transfers with Planetary and Interstellar Interaction Stations.

### 确定性成长科技 Deterministic Growth

原版科技树第二页包含以下 FE 成长链：

- **分馏堆叠成长**：全局堆叠提高到 20，同时按 `4 / 当前堆叠` 缩短全部原版配方耗时。
- **亚空间扩容**：每级增加所有物品的独立容量。
- **亚空间存取能效**：降低物流交互站传输耗能。
- **探索信号增幅**：缩短定时信号周期并提高 Frag 掉落率。
- **三类高级塔特质**：降低催化损耗、提高原胚繁育产出、提高置换回收量。

第一次完整体验生命工业时，堆叠成长从原版集装物流阶段接入。某个当前格式的存档取得三类高级塔并达到 20 堆叠后，未来新存档会采用更早开始的五档堆叠路线，并从首遗物获得物流补给。

The second technology page provides known, matrix-funded upgrades for stacking, subspace capacity and efficiency, exploration, and each advanced tower's core trait.

## 高级配方 Advanced Recipes

当前高级目录刻意保持精简：

- 光栅石、分形硅石、金伯利矿石、可燃冰、刺笋结晶和单极磁石催化增殖。
- 三种高级塔的原胚培育与原胚繁育。
- 液氢燃料棒到氘核燃料棒、氘核燃料棒到反物质燃料棒的置换回收。
- 采矿机到大型采矿机、电弧熔炉到位面熔炉的置换重构。

所有高级配方的基础成功率都是 20%，再乘以同类高级塔最近一分钟献祭形成的加成。失败轮的普通反应物会从流动输出返回，催化物只在成功轮按低概率消耗。多个产物按实际积压公平输出，不会因为一种副产物量少而永久饿死。

The initial advanced catalog focuses on rare-resource catalysis, tower cultivation and breeding, two fuel replacements, and two high-value machine reconstructions. Failed ordinary inputs pass through; low-consumption catalysts remain until actually consumed.

## 操作 Controls

| 操作 | 默认按键 |
| --- | --- |
| 打开/关闭 FE 主面板 | `Shift + F` |
| 在两种主面板外壳间切换 | `Ctrl + Shift + F`，面板打开时 |
| 查看教程 | `G` |

按键可在游戏设置中修改。主面板页面为“文明研究”“遗物探索”“亚空间仓储”“开发日记”和“杂项设置”。高级塔配方在原版分馏塔窗口中选择。

Controls are configurable in the game settings. Advanced recipes are selected directly in the standard fractionator window.

## 联机 Multiplayer

万物分馏支持 Nebula。研究、探索、亚空间库存、高级塔反应、营养与耐久均由主机结算；中途加入玩家会收到当前状态。客户端认领探索点时，主机使用该连接对应玩家的真实宇宙坐标验证球形距离。

多人游戏要求主机与客户端使用相同 FE 版本。Nebula 客户端不能直接从高级塔窗口手动取出产物，请使用传送带输出。

Nebula multiplayer is host-authoritative for research, exploration, subspace storage, and advanced-tower state. All participants must use the same FE version.

## 存档兼容 Save Compatibility

当前 FE 自有存档格式版本为 11。

- FE 2.x 存档会迁移能够对应的塔、建筑、模型、配方和蓝图物理引用。
- 2.x 的协议、成就、远古科技点、塔等级及其他 FE 自有进度不会转换为当前研究进度。
- 过渡存档版本 10 不执行 2.x 物理迁移，也不会被解释为当前格式状态。
- 建议升级前保留原存档备份。

The current FE save format is version 11. Physical references from FE 2.x are migrated where a stable counterpart exists, but removed progression systems are intentionally not converted. Keep a backup before upgrading an existing save.

## 安装 Installation

推荐使用 [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) 或兼容的 Thunderstore 模组管理器安装并启用 **FractionateEverything**。管理器会安装清单中的依赖，包括 BepInEx、LDBTool、DSPModSave、CommonAPI、BuildBarTool、ModFixerOne、ErrorAnalyzer 和 NebulaMultiplayerModApi。

Use [r2modman](https://dsp.thunderstore.io/package/ebkr/r2modman/) or another Thunderstore-compatible manager, then install and enable **FractionateEverything**. Required dependencies are declared in the package manifest and should be installed automatically.

## 反馈 Feedback

遇到错误时，请附上游戏版本、FE 版本、已启用模组列表、复现步骤和 ErrorAnalyzer 日志：

- [GitHub Issues](https://github.com/MengLeiFudge/MLJ_DSPmods/issues/new)
- [万物分馏反馈群](https://qm.qq.com/q/zzicz6j9zW)

For bug reports, include the game version, FE version, enabled mods, reproduction steps, and the ErrorAnalyzer log.

## 致谢 Credits

- jinxOAO：FractionateUniverse 启发了按功能区分分馏塔的最初方向。
- Awbugl：协助解决模组开发问题，FE 的部分加载与兼容路径参考了 Genesis Book。
- Starfi5h：协助处理模组开发中的复杂兼容与错误问题。
- soarqin：FE 2.0 的 UI 组件基础来自其封装。
- L、飞鸿、聆汐及交流群测试玩家：长期提供实际游玩反馈、数值建议和错误报告。
- 所有使用和反馈万物分馏的玩家。

Special thanks to jinxOAO, Awbugl, Starfi5h, soarqin, L, 飞鸿, 聆汐, the community testers, and everyone who has played and reported issues for Fractionate Everything.
