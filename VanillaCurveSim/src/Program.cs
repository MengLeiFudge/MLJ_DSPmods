using System;
using System.IO;
using System.Text;

namespace VanillaCurveSim;

internal static class Program {
    private static int Main(string[] args) {
        try {
            Console.OutputEncoding = new UTF8Encoding(false);
            string solutionDir = ResolveSolutionDir();
            if (args.Length == 1 && args[0] == "--conversion-balance") {
                Console.WriteLine(ConversionBalance.Write(solutionDir));
                return 0;
            }
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--fe-scenario")) {
                throw new ArgumentException("用法：VanillaCurveSim.exe [--fe-scenario 工况.json | --conversion-balance]");
            }
            FeDataSet feData = FeDataLoader.Load(solutionDir);
            FeSimulationOptions options = FeDataLoader.LoadOptions(args.Length == 2 ? args[1] : null, feData);
            GameDataSet dataSet = DataLoader.Load(solutionDir);
            var simulator = new VanillaCurveSimulator(dataSet);
            var baselineResults = simulator.RunAll();
            string baselineMarkdownPath = ReportWriter.Write(solutionDir, baselineResults);

            var feScenarioSimulator = new FeScenarioSimulator();
            SimulationComparisonReport comparisonReport = feScenarioSimulator.Run(feData, options, baselineResults);
            string comparisonMarkdownPath = ComparisonReporter.Write(solutionDir, comparisonReport, feData);

            Console.WriteLine("原版基线与FE现行框架模拟完成。");
            Console.WriteLine($"FE配方：{feData.Recipes.Count}；工况：{options.Scenarios.Count}；每项时长：{options.DurationSeconds}秒；种子：{options.Seed}");
            Console.WriteLine($"原版物品：{dataSet.ItemsById.Count}");
            Console.WriteLine($"原版配方：{dataSet.RecipesById.Count}");
            Console.WriteLine($"原版科技：{dataSet.TechsById.Count}");
            Console.WriteLine($"原版基线报告：{baselineMarkdownPath}");
            Console.WriteLine($"FE机制报告：{comparisonMarkdownPath}");
            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    // 运行时从 bin/Debug 回溯到 solution 根目录。
    private static string ResolveSolutionDir() {
        string currentDir = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(currentDir, "MLJ_DSPmods.sln"))) {
            return currentDir;
        }

        string dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 4 && dir != null; i++) {
            DirectoryInfo parent = Directory.GetParent(dir);
            dir = parent?.FullName;
            if (dir != null && File.Exists(Path.Combine(dir, "MLJ_DSPmods.sln"))) {
                return dir;
            }
        }

        if (dir == null) {
            throw new DirectoryNotFoundException("无法定位 solution 根目录。");
        }

        throw new DirectoryNotFoundException("无法定位 solution 根目录。");
    }
}
