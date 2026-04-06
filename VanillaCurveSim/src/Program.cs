using System;
using System.Collections.Generic;
using System.IO;

namespace VanillaCurveSim;

internal static class Program {
    private static int Main(string[] args) {
        try {
            string solutionDir = ResolveSolutionDir();
            SimulatorSelfCheck.RunAll();
            GameDataSet dataSet = DataLoader.Load(solutionDir);
            var simulator = new VanillaCurveSimulator(dataSet);
            var baselineResults = simulator.RunAll();
            string baselineMarkdownPath = ReportWriter.Write(solutionDir, baselineResults);

            var feScenarioSimulator = new FeScenarioSimulator();
            IReadOnlyList<FractionationScenarioResult> treatmentResults =
                feScenarioSimulator.BuildTreatments(baselineResults);
            var comparisonReport = new SimulationComparisonReport();
            comparisonReport.BaselineResults.AddRange(baselineResults);
            comparisonReport.TreatmentResults.AddRange(treatmentResults);
            string comparisonMarkdownPath = ComparisonReporter.Write(solutionDir, comparisonReport);

            Console.WriteLine("Vanilla curve simulation finished.");
            Console.WriteLine($"Items: {dataSet.ItemsById.Count}");
            Console.WriteLine($"Recipes: {dataSet.RecipesById.Count}");
            Console.WriteLine($"Techs: {dataSet.TechsById.Count}");
            Console.WriteLine($"Baseline Report: {baselineMarkdownPath}");
            Console.WriteLine($"FE Impact Report: {comparisonMarkdownPath}");
            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    // 运行时从 bin/win/<Config> 回溯到 solution 根目录。
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
