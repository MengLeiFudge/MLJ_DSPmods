using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace VanillaCurveSim;

/// <summary>输出现行FE机制的输入条件、逐批结果与期望，原版时间线保持独立。</summary>
internal static class ComparisonReporter {
    public static string Write(string solutionDir, SimulationComparisonReport report, FeDataSet data) {
        string outputDir = Path.Combine(solutionDir, "gamedata", "curve-sim");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "fe-impact-report.json"),
            JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(true));
        string markdownPath = Path.Combine(outputDir, "fe-impact-report.md");
        File.WriteAllText(markdownPath, BuildMarkdown(report, data), new UTF8Encoding(true));
        return markdownPath;
    }

    private static string BuildMarkdown(SimulationComparisonReport report, FeDataSet data) {
        var sb = new StringBuilder("# FE 现行框架模拟\r\n\r\n");
        sb.AppendLine($"生成时间：{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}；种子：{report.Options.Seed}；每项工况：{report.Options.DurationSeconds}秒；60 tick/秒。");
        sb.AppendLine();
        sb.AppendLine("## 口径与边界");
        sb.AppendLine();
        sb.AppendLine("生产按游戏导出的现行配方逐批结算。假设配方已解锁、主料满缓存、催化剂足量、满供电满耐久、食物不断供，产物与失败物可及时排出。每张配方单独运行一座塔，结果不能相加为一座塔的产能。回流须真实回接，催化剂持有不等于每轮消费。食物增产点只延长续航，基本矿物输入不喷涂。所选堆叠、特质与献祭资格是工况前提，不是免费科技。");
        sb.AppendLine();
        sb.AppendLine("研究以已取得线索、损毁样本能补充且成功样本能回接为前提；固定样本价值网格用于比较价值与预算，不代表某种物品的实际估值。扩容另设六座解析塔各供一种矩阵，共用各档60件缓存。源点献祭假设已开放献祭且所有当前阶段资料恢复；50源点只标记具备每分钟恢复一项传说的资格，不凭空增加传说配方。");
        sb.AppendLine();
        sb.AppendLine("这是机制与供需模拟，不是完整星图工厂仿真。未模拟探索路线、建造、运输争用、堵塞、断粮耐久、科技排队和丰碑在轨生命周期；不推导FE通关时间或综合加速指数。生产表给出所需流量，可据此核对传送带及供料能力。矩阵与物品估值来自当前导出快照，不会启动游戏更新数据。");
        sb.AppendLine();
        sb.AppendLine("## 原版进度基线");
        sb.AppendLine();
        sb.AppendLine("| 策略 | 阶段 | 原版时长（分钟） |");
        sb.AppendLine("| --- | --- | ---: |");
        foreach (StrategySimulationResult baseline in report.BaselineResults) {
            foreach (PhaseSummary phase in baseline.PhaseSummaries) {
                sb.AppendLine($"| {(baseline.Strategy == PlayerStrategyKind.Speedrun ? "速通" : "常规")} | {phase.Phase} | {N((phase.PhaseEndSeconds - phase.StartSeconds) / 60)} |");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## 单塔生产");
        foreach (FeScenarioConfig scenario in report.Options.Scenarios) {
            var (energy, speed) = FeRules.Food(data, scenario);
            sb.AppendLine();
            sb.AppendLine($"### {Escape(scenario.Name)}");
            sb.AppendLine();
            sb.AppendLine($"堆叠{scenario.Stack}；食物{data.Name(scenario.FoodItemId)}，增产点{scenario.FoodInc}；特质{scenario.TraitLevel}级；源点经过{N(scenario.MemoryPassesPerSecond)}件/秒。高级塔食物能量{N(energy)}/件，速度倍率{N(speed)}，待机食物期望{N(90 / energy)}件/分钟。高级塔自身电耗为0，食物仍须由工厂生产。");
            sb.AppendLine();
            sb.AppendLine("下表为整段工况累计件数；方括号中的成功次数是按实际批次和当分钟概率累加的期望。食物点燃包括末尾尚未耗尽的一件；净物料估值不含食物，不能作为通关速度或经济收益评级。");
            sb.AppendLine();
            sb.AppendLine("| 配方 | 尝试轮/秒 | 成功次数[期望] | 实际消耗 | 失败回流 | 实际产物 | 营养/点燃食物 |");
            sb.AppendLine("| --- | ---: | ---: | --- | --- | --- | ---: |");
            foreach (FeProductionResult row in report.Production.Where(r => r.Scenario == scenario.Name)) {
                sb.AppendLine($"| {Escape(row.RecipeName)} | {N(row.RoundsPerSecond)} | {row.Successes}[{N(row.ExpectedSuccesses)}] | {Amounts(row.Consumed, data)} | {Amounts(row.Returned, data)} | {Amounts(row.Produced, data)} | {row.WorkNutrition}/{row.FoodIgnited} |");
            }
            sb.AppendLine();
            sb.AppendLine("催化剂及辅料的最低持有量、末尾食物能量、逐物品净消耗均保存在同名JSON中。基础矿物产物包含被成功转化的原投入，净增量为产物减实际消费。");
        }
        sb.AppendLine();
        sb.AppendLine("## 样本研究");
        sb.AppendLine();
        sb.AppendLine("阶段0为开局，1～6为蓝至白。理论耗时按预算/(每秒经过数×成功率)，不含缺料和探索；样本只有损毁计入消费。实际未完成项报告剩余共享槽，不把模拟截止当作完成时间。特质预算与白阶段同为20000次成功，取得线索仍需各自千塔资格。");
        sb.AppendLine();
        sb.AppendLine("| 内容 | 堆叠 | 样本价值 | 理论分钟 | 实际运行分钟 | 完成项 | 未完成槽 | 成功返还 | 损毁消费 |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (FeResearchResult row in report.Research) {
            sb.AppendLine($"| {row.Content} | {row.Stack} | {N(row.SampleValue)} | {N(row.ExpectedSeconds / 60)} | {N(row.ElapsedSeconds / 60)} | {row.CompletedContents}/{row.ContentCount} | {N(row.Progress / 100d)}% | {row.ReturnedSamples} | {row.DestroyedSamples} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 源点阶段恢复");
        sb.AppendLine();
        sb.AppendLine("各行独立运行，仅当前最低未恢复阶段开放；成功率2%，失败回流且不损毁。所需源点是实际成功消费，经过次数不能当作资源成本。");
        sb.AppendLine();
        sb.AppendLine("| 阶段 | 堆叠 | 消费/需求 | 经过次数 | 理论分钟 | 实际运行分钟 | 完成 |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (FeRecoveryResult row in report.Recovery) {
            sb.AppendLine($"| {row.Stage} | {row.Stack} | {row.ConsumedMemory}/{row.RequiredMemory} | {row.Passes} | {N(row.ExpectedSeconds / 60)} | {N(row.ElapsedSeconds / 60)} | {(row.ConsumedMemory == row.RequiredMemory ? "是" : "否")} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 完整分钟献祭");
        sb.AppendLine();
        sb.AppendLine("源点每次经过按8%被收取。本分钟消费只作用于下一分钟；末行的下一分钟增益尚未用于本次生产统计。停止供料后的首分钟仍使用上一窗口增益，再下一分钟归零。");
        sb.AppendLine();
        sb.AppendLine("| 工况 | 分钟 | 源点实际消费 | 本分钟增益 | 下分钟增益 | 满足传说恢复预算 |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var pair in report.Offerings.Where(p => p.Value.Any(m => m.ConsumedMemory > 0))) {
            foreach (FeOfferingMinute row in pair.Value) {
                sb.AppendLine($"| {Escape(pair.Key)} | {row.Minute} | {row.ConsumedMemory} | {N(row.AppliedBoost * 100)}% | {N(row.NextBoost * 100)}% | {(row.LegendaryRecoveryEligible ? "是" : "否")} |");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## 六档扩容与余料源点");
        sb.AppendLine();
        sb.AppendLine("通信已恢复，每档一座解析塔。被处理矩阵先进入60件缓存，余下部分才按本档预算结算源点；未处理部分回流。每游戏秒各档最多消费1件，增加塔数不会增加本档消费上限。没有加入溢出物品价值或终极仓储。");
        sb.AppendLine();
        sb.AppendLine("| 堆叠 | 矩阵 | 已处理 | 扩容消费 | 缓存 | 剩余处理件数 | 产出源点 | 容量增量 |");
        sb.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var pair in report.ExpansionByStack) {
            foreach (FeExpansionResult row in pair.Value) {
                sb.AppendLine($"| {pair.Key} | {data.Name(row.MatrixId)} | {row.Processed} | {row.ExpansionConsumed} | {row.Buffered} | {row.Processed - row.ExpansionConsumed - row.Buffered} | {row.MemoryProduced} | {N(row.CapacityIncrement)} |");
            }
            sb.AppendLine();
            sb.AppendLine($"{pair.Key}层六档总容量倍率：{N(1 + pair.Value.Sum(r => r.CapacityIncrement))}。");
            sb.AppendLine();
            if (pair.Key != report.ExpansionByStack.Keys.Last()) {
                sb.AppendLine("| 堆叠 | 矩阵 | 已处理 | 扩容消费 | 缓存 | 剩余处理件数 | 产出源点 | 容量增量 |");
                sb.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            }
        }
        sb.AppendLine("## 数据来源");
        sb.AppendLine();
        sb.AppendLine("配方与估值使用以下快照。转化数值同时与编译链接的ConversionSpec核对；共享随机数使用FE的RandomUtils。其余规则对应GAME_DESIGN_SPEC第3、4、6章及相应运行代码，改变规则后须同步更新离线公式。");
        sb.AppendLine();
        foreach (var pair in report.SourceSha256) { sb.AppendLine($"- `{pair.Key}`：`{pair.Value}`"); }
        return sb.ToString();
    }

    private static string Amounts(IReadOnlyDictionary<int, long> amounts, FeDataSet data) {
        string[] values = amounts.Where(p => p.Value > 0).Select(p => $"{Escape(data.Name(p.Key))}×{p.Value}").ToArray();
        return values.Length == 0 ? "—" : string.Join("、", values);
    }
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
