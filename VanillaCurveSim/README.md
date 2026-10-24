# VanillaCurveSim

独立的 Windows 控制台程序。保留原版常规/速通进度基线，并对当前 FE 配方、样本解析、阶段恢复、分钟献祭和亚空间扩容进行离线计算。

模拟器没有测试入口。默认启动直接计算并写出结果。

## 运行

在仓库根目录构建，然后运行：

```powershell
MSBuild.exe VanillaCurveSim/VanillaCurveSim.csproj /t:Build /p:Configuration=Debug
.\VanillaCurveSim\bin\Debug\VanillaCurveSim.exe
```

从 WSL 调用时，使用根目录 AGENTS.md 指定的 Windows MSBuild，运行 Windows EXE。输出固定为 `bin\Debug`。

默认使用5个独立工况，每项600游戏秒，随机种子97。每张已解锁配方单独运行一座塔；所有主料、催化剂和食物充足，回流及产物排出不堵塞。高堆叠和特质是已研究条件，不是模拟器自动发放的收益。

结果写入：

- `gamedata/curve-sim/vanilla-strategy-report.md` 与 `.json`：原版基线。
- `gamedata/curve-sim/fe-impact-report.md` 与 `.json`：现行 FE 机制结果。JSON 的 `SchemaVersion` 为2。

原版基线仍沿用自身的阶段生产、科研和电力估算。FE报告输出单塔物料与供养需求、研究完成度、源点实际消费和容量增长，不将这些数值强行折算成通关时间。

## 自定义工况

```powershell
.\VanillaCurveSim\bin\Debug\VanillaCurveSim.exe --fe-scenario VanillaCurveSim\scenario.example.json
```

| 字段 | 含义 |
| --- | --- |
| DurationSeconds | 每项模拟的游戏秒数；60～3600，须为整分钟 |
| Seed | 批量期望取整使用的随机种子 |
| Scenarios | 1～20个独立生产工况；名称不得重复 |
| Stack | 实际货物平均集装数，1～20；满缓存、满供电 |
| FoodItemId | 已导出且有有效估值的矩阵食物ID |
| FoodInc | 当前食物每件增产点，0～10；只增加能量，不加速 |
| TraitLevel | 三种高级塔各自特质的已研究等级，0～6 |
| MemoryPassesPerSecond | 独立解析塔每秒经过的源点数，0～30×Stack；8%实际消费后，按完整分钟向下一窗口提供增益 |

献祭工况假设全部当前阶段资料已恢复且千塔献祭已开放。每分钟至少50源点只表示达到恢复传说的投入条件，模拟器不生成额外解锁记录。默认首分钟没有献祭增益。

研究工况对每种堆叠分别计算阶段0～6、样本价值1/100/10000，以及黄紫两条线索共用同一样本的情形。全部线索均视为已经取得；成功样本返还，损毁样本持续补给。预算与价值网格用于解释机制，不能当作某张稀有配方的实际探索时间。未在时限内完成时，报告保留进度并明确标记未完成。

扩容另设六座解析塔，各自供给一种主线矩阵。每档共享60件缓存、每秒最多消费1件；已处理但未接纳的矩阵才结算源点。独立实验的库存和产量不相加成一套工厂。

## 数据与规则来源

默认读取已存在的游戏导出，不启动游戏：

- `gamedata/Vanilla/gameData.csv`、`gamedata/calc json/Vanilla.json`：原版基线。
- 原版基线还沿用 `gamedata/DecompiledSource/ProjectOrbitalRing/ProjectOrbitalRing.data.items_vanilla.json` 的堆叠资料。
- `gamedata/calc json/FractionateEverything.json`：当前合法来源目录中的生产配方。
- `gamedata/FE/gameData.csv`、`itemValue.csv`：物品名称、矩阵资格与估值。

FE数据必须包含稳定配方ID、每轮参与数量、独立消耗概率与食物标记。导出须为零特质基础数值；缺失字段、非法数值或与共用 `ConversionSpec` 冲突时停止并报错，不回退到旧模型。报告记录FE输入快照的SHA256，复现时同时保留参数和快照。

`FeRules` 对应 `GAME_DESIGN_SPEC.md` 第3、4、6章及当前运行代码。转化规格和随机数发生器与FE编译同一份源文件。其他数值变化时须同步离线公式。没有模拟探索路线、建造、物流争用、输出堵塞、断粮耐久、科技队列及丰碑在轨生命周期；丰碑配方的制造投入产出已包含在生产目录中。

独立转化物料核算仍可运行：

```powershell
.\VanillaCurveSim\bin\Debug\VanillaCurveSim.exe --conversion-balance
```

该入口输出 `VanillaCurveSim/Reports/ConversionBalance.md`，使用与FE共用的纯数值规格，不依赖完整FE快照。

## 代码归属

| 文件 | 职责 |
| --- | --- |
| Program.cs | 参数、数据加载与报告流程 |
| DataLoader.cs / Simulator.cs / ReportWriter.cs | 原版进度基线 |
| FeDataLoader.cs | 当前导出字段校验、估值和来源指纹 |
| FeModels.cs / FeRules.cs | 离线输入、结果与明确的机制公式 |
| FeProductionSimulator.cs | 单塔逐tick生产与供养账本 |
| FeResearchSimulator.cs | 共享样本槽、阶段恢复、分钟献祭及扩容 |
| FeScenarioSimulator.cs | 组织独立工况 |
| ComparisonReporter.cs | 当前机制报告 |
| ConversionBalance.cs | 共用转化规格的期望物料核算 |
