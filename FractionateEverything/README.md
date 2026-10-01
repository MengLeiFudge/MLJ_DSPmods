![Fractionate Everything](https://s2.loli.net/2024/04/08/LtlNkxZD4jmdbFX.jpg)

> “走自己的路，不要随波逐流。” —— 重氢
> "Don't just follow the path. Make your own trail." - Deuterium

# 万物分馏 Fractionate Everything

万物分馏将分馏扩展为一套需要探索、解析和供养的古文明生命工业。首次生产电磁矩阵后，你会从出生星上的首个遗物开始，建立宇宙通用的分馏亚空间，在实地信号中发现具体研究内容，并用五种分馏塔搭建普通资源增殖、珍奇催化、生命塔繁育和工业置换产线。

Fractionate Everything turns fractionation into an old-civilization living industry built around exploration, analysis, and sustained automation. After producing your first Electromagnetic Matrix, recover the first relic, establish a galaxy-wide subspace inventory, discover concrete research contents in the field, and operate five specialized fractionators.

## 主要内容 Features

### 五种分馏塔 Five Fractionators

- **基础矿物塔 Basic Mineral Tower**：自动识别普通自然资源。每轮有机会消耗 1 件并产出 2 件同种物品；失败物继续流出。
- **基础解析塔 Basic Analysis Tower**：消耗已经探索到具体内容的物品，推进该物品唯一的共享解析槽；槽满时从已探索、未解析的内容中选出一项。没有待解析内容的普通物品会原样流出，高级塔则改为献祭。
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

探索点每次会发现一项**具体内容**，并确定承载它的解析物品。把该物品送进基础解析塔后，会推进这个物品唯一的共享进度槽；槽满时，只从该物品已经探索且尚未解析的内容中选择。

- 只有一个候选时结果确定；多个候选同时等待时才随机选择。
- 同一内容可能在目录中关联多个物品，但一次探索只指定一个承载物品，不会一次开放整个池。
- 配方或塔特质解析完成后立即可用；科技方向解析完成后，整条同名等级行同时显现，再正常消耗矩阵研究。
- Memory 承载普通科技方向。三类高级塔只承载各自的特质。
- 同类高级塔在亚空间库存中首次达到 1000 座后，对应特质才可进入探索候选；之后仍须探索和解析。高级塔没有待解析特质时，基础解析塔会改为献祭，并以每件 2% 的成功率从正面产出 Memory。
- 最近 60 秒内同类高级塔的实际献祭速率会提高该类高级塔的配方成功率：按 5% 分档，每秒更新，最高 `+75%`；三类塔分别计算。

文明研究页会在顶部显示完整研究目录的“已探索/已解析”数量和进度，左侧列出承载物品的真实共享槽，右侧列出科技与特质档案；尚未探索的档案只显示问号。全部稳定配方恢复到独立“配方图鉴”页面，按类别列出完整目录和发现进度，未发现配方不会提前泄露名称、输入或产物。

Each signal reveals one concrete result and its carrier item. Every item has one shared analysis slot; filling it resolves one explored pending result. Technology directions reveal an entire upgrade row, but every level still requires normal matrix research. Results are globally unique, and Advanced-Tower traits require both the 1,000-tower exploration threshold and a later concrete discovery.

### 遗物探索 Relic Exploration

首次生产电磁矩阵会触发一次强烈脉冲，解锁遗物探测器，并在出生星生成“关键信号1”。左侧 HUD 会持续显示无方向的遗物强度；FE 不会替你启动导航、设置位置标记或控制机甲。找到并回收首遗物后获得：

- 基础矿物塔 x10
- 基础解析塔 x10
- 通用原胚 x20
- 石矿与铁矿基础复制配方
- 分馏亚空间和主面板访问能力

取得曲速飞行能力后，出生星系另一颗星球会出现第二个关键信号并直接公开星球名称；回收后解锁星球探测器，普通未知广播才会开始由定时网络或 Frag 产生。

普通广播统一命名为“信号{编号}”，只给出量化后的百分比强度，不公开目标星系、星球、方向、距离、内容或坐标。星球探测器用于寻找宿主星球，进入目标星球后固定为 100%；遗物探测器在目标星球外为 0%，进入后才用于寻找具体点位。两个读数共用“探索信号增幅”的 `20% / 10% / 5% / 2% / 1% / 0.5%` 精度成长。

左侧 HUD 第 1 行固定当前关键信号，另外 4 行由普通信号占用；它只使用接近原版目标列表的纯文字，不带黑色背景或发光字体。新信号只在有空槽时自动固定，满槽时不会替换；全部信号仍保留在探索面板，可手动固定或取消。成长科技最终级会在面板中开放“标注位置”，由玩家点击后复用原版目的地标记和指引线，但不会创建移动命令。

探索点使用球形认领范围，固态星球的点贴近地表，气态巨星的点位于外层空间，且所有信号都不会过期。点位可能提供一项具体配方、塔特质或科技方向，也可能提供宿主星球实际拥有的资源或通用原胚；研究内容和承载物品只在抵达后揭示。通用原胚固定占资源奖励的 10%；其余权重为火山/沙漠 `80% 矿脉 + 10% 流体`、海洋 `55% + 35%`、冰原 `60% + 30%`、气态巨星 `90% 气体`。缺失资源类别的权重转给该星球其他实际资源，原胚权重不变。奖励直接进入亚空间，并允许暂时超过常规容量。

The first Electromagnetic Matrix unlocks a directionless Relic Detector and creates Key Signal 1 on the birth planet; FE never starts navigation or controls the mecha. Warp capability later reveals Key Signal 2 on another planet in the birth system, and recovering it unlocks the Planet Detector. Ordinary broadcasts reveal only quantized planet and relic strength percentages. The left HUD keeps one mandatory key signal plus four player-pinned ordinary signals. At the final amplification level, players may explicitly create a vanilla location marker without issuing any movement command.

### 黑雾补给 Dark-Fog Drops

地面和太空黑雾均有机会分别掉落 Frag 和通用原胚。星球探测器恢复后，Frag 送入基础解析塔可能生成普通广播；恢复前 Frag 会原样流出。通用原胚用于高级塔培育和繁育。

黑雾不是主线门槛。关闭战斗时，定时探索信号和资源点仍能提供完整进度与持续原胚来源。

Ground and space Dark Fog can independently drop Frag and Universal Tower Embryos. Combat accelerates progress but is never required for the complete progression.

### 分馏亚空间 Fractionation Subspace

亚空间是一套全宇宙共享库存，每种物品拥有独立容量。初始容量为每种物品 10 组；亚空间扩容每级增加 10 组，最高可以研究到 10000 级。

访问方式：

- `Shift + F` 打开 FE 主面板，在“亚空间仓储”中手动存取。
- 两座基础塔在正面输入、两侧不连接时上传物品；容量不足时整批拒收，不会丢货。
- 行星和星际物流交互站自动存取，并保留槽位模式、阈值、增产点、复制粘贴和蓝图设置。
- 高级塔与其他物品一样正常进入亚空间；同类库存首次达到 1000 座时只让对应特质进入探索候选，需要解析或献祭时再把该塔送入基础解析塔。

物流交互站按物品价值与实际搬运数量消耗站内电量。能量不足时只传输可支付的数量；六级亚空间存取能效最多把耗能降低 60%。

Subspace is a galaxy-wide inventory with independent per-item limits. Access it manually, upload through the front port of either basic tower, or automate transfers with Planetary and Interstellar Interaction Stations.

### 确定性成长科技 Deterministic Growth

两项物流交互站解锁科技位于原版科技树第一页。第二页只显示已经完成探索与解析的成长行：

- **超级压缩技术**：全局堆叠提高到 20，同时按 `4 / 当前堆叠` 缩短全部原版配方耗时。普通档由原版集装物流提供 4 堆叠，再研究 8/12/16/20 四级；已玩过档研究 4/8/12/16/20 五级，不增加 24 堆叠档。
- **亚空间扩容**：前八级逐步投入累计矩阵；第九级起只消耗宇宙矩阵，最高 10000 级，每级增加所有物品 10 组容量。
- **亚空间存取能效**和**三类高级塔特质**：完整六级。前五级逐层累计电磁至引力矩阵，第六级只消耗宇宙矩阵。
- **探索信号增幅**：同样为完整六级，同时缩短普通信号周期、提高 Frag 掉落并把显示精度从 20% 逐级提高到 0.5%；第六级开放玩家主动位置标注。

解析一个科技方向后会同时显示整条同名等级行，但不会免费完成任何一级。每级仍按原版科技研究；行内连线只连接前一级，不从其他科技页拉出悬空长线。沙盒“一键解锁科技”沿用原版资格，可直接解锁并显示尚未解析的 FE 有限科技；普通模式仍须先完成探索解析。原版不会一键升满的长期扩容节点只在前置解锁后显示。

第一次完整体验生命工业时，超级压缩技术从原版集装物流阶段接入。某个当前格式的存档取得三类高级塔并达到 20 堆叠后，未来新存档会采用更早开始的五档路线，并从首遗物获得物流补给。

Planetary and Interstellar Subspace Interaction unlocks stay on the first technology page. Growth rows appear on page two only after their concrete direction is explored and analysed. Finite rows follow vanilla level-chain and cumulative-matrix conventions; Subspace Capacity continues as a white-matrix upgrade through level 10,000.

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

按键可在游戏设置中修改。主面板页面为“文明研究”“遗物探索”“配方图鉴”“亚空间仓储”“开发日记”和“杂项设置”。文明研究集中显示总体研究进度与科技/特质档案，配方图鉴显示全部配方槽位和发现进度；高级塔配方仍在原版分馏塔窗口中选择。

Controls are configurable in the game settings. Advanced recipes are selected directly in the standard fractionator window.

## 联机 Multiplayer

万物分馏支持 Nebula。研究、探索、探测器状态、亚空间库存、高级塔反应、营养与耐久均由主机结算；中途加入玩家会收到当前状态。客户端认领探索点时，主机使用该连接对应玩家的真实宇宙坐标验证球形距离。普通信号的四个 HUD 固定槽属于各玩家本地 UI 偏好，不会互相覆盖。

多人游戏要求主机与客户端使用相同 FE 版本。Nebula 客户端不能直接从高级塔窗口手动取出产物，请使用传送带输出。

Nebula multiplayer is host-authoritative for research, exploration, subspace storage, and advanced-tower state. All participants must use the same FE version.

## 存档兼容 Save Compatibility

当前 FE 自有存档格式版本为 12，并明确兼容读取版本 11。

- 版本 11 的旧物品资格池会迁移为具体内容到承载物品的映射；已经开始或完成的独立成长科技会迁移到同 ID 的原版等级链，并保持对应科技行可见。
- FE 2.x 存档会迁移能够对应的塔、建筑、模型、配方和蓝图物理引用。
- 2.x 的协议、成就、远古科技点、塔等级及其他 FE 自有进度不会转换为当前研究进度。
- 过渡存档版本 10 不执行 2.x 物理迁移，也不会被解释为当前格式状态。
- 建议升级前保留原存档备份。

The current FE save format is version 12 and includes an explicit version-11 migration for research contents and technology levels. Physical references from FE 2.x are migrated where a stable counterpart exists, but removed progression systems are intentionally not converted. Keep a backup before upgrading an existing save.

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
